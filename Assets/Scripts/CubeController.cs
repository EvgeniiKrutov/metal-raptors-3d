using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    [RequireComponent(typeof(Rigidbody))]
    public class CubeController : MonoBehaviour, IDamageable
    {
        public event Action OnCrashed;

        public event Action OnShotDown;

        public event Action OnDamaged;

        public event Action OnScraped;

        public float CurrentHealth { get; private set; }

        public float MaxHealth { get; private set; }

        public float Heading => _heading;

        public float AngularVelocity => _angularVelocity;

        public float EnginePitch { get; set; } = 1f;

        public float MaxTurnRate => _config != null
            ? _config.rotationSpeed * Mathf.Deg2Rad * _boost
            : 0f;

        public bool Boosting => _boostTarget > 1f;

        public bool BarrelRolling => _barrelLeft > 0f;

        public bool Evading => (BarrelRolling || _evadeGrace > 0f) && !_falling;

        public bool HighRevs => Boosting || BarrelRolling;

        public bool TurningBack => _turnSec > 0f;

        public float Facing => TurningBack
            ? _turnFrom * Mathf.Cos(_turnYaw)
            : Mathf.Cos(_heading) < 0f ? -1f : 1f;

        const float ExplosionSize = 60f;

        const float TurnMainShare = 0.6f;
        const float TurnSwingFrom = 0.35f;
        const float TurnOvershoot = 0.5825f;
        const float TurnPeakRate = 8.36f;
        const float TurnSlow = 0.6f;
        const float TurnSlowUntil = 0.75f;
        const float TurnBankLead = 0.05f;
        const float TurnRateProbe = 0.01f;
        const float TurnSettleShare = 0.25f;
        const float WindReturnSec = 1.2f;

        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;

        const float SmokeHealthThreshold = 30f;

        const float BoostResponse = 3.5f;
        const float BoostSnap = 0.001f;

        const float BarrelTurn = 360f;
        const float BarrelMinRate = 40f;

        const float GroundProbe = 500f;
        const float GroundSkim = 14f;

        const float HoverResponse = 8f;
        const float HoverBankDeg = 16f;
        const float HoverBankResponse = 5f;

        PlayerConfig _config;
        Rigidbody _rb;
        ShakeEffect _shake;
        SmokeTrail _smoke;
        PlaneFire _fire;
        Vector3? _engine;
        readonly PlaneRoll _roll = new PlaneRoll(false);

        float _heading;
        float _angularVelocity;
        float _speed;
        bool _active;
        bool _controlled = true;
        bool _falling;
        PlaneFall _fall;
        float _lastCollisionTime = -999f;

        float _minX, _maxX, _worldWidth, _ceilingY, _edgeMargin;

        float _boost = 1f;
        float _boostTarget = 1f;
        bool _cinematic;

        float _barrelAngle;
        float _barrelLeft;
        float _barrelRate;
        float _evadeGrace;
        float _barrelGrace;

        bool _hardWalls;
        float _wallMinX = float.NegativeInfinity;
        float _wallMaxX = float.PositiveInfinity;

        bool _headingSteering;
        float _targetHeading;

        bool _hover;
        float _hoverSpeed;
        float _hoverFloorY;
        Vector2 _hoverVelocity;
        Vector2 _hoverInput;
        float _bank;

        Vector2 _windDrift;
        float _windPitch;
        float _windRoll;

        ISolid _solid;
        float _solidRadius = PlaneScrapes.HitboxRadius;

        float _turnSec;
        float _turnAge;
        float _turnFrom = 1f;
        float _turnYaw;
        float _turnBank;
        float _turnZ;
        float _turnLift;
        Quaternion _turnLean = Quaternion.identity;
        float _windBlend = 1f;

        public void Initialize(PlayerConfig config, float startHeadingRad, float minX, float maxX,
            float ceilingY, float edgeMargin, bool hardWalls = false)
        {
            _hardWalls = hardWalls;
            _config     = config;
            _heading    = startHeadingRad;
            _minX       = minX;
            _maxX       = maxX;
            _worldWidth = maxX - minX;
            _ceilingY   = ceilingY;
            _edgeMargin = edgeMargin;

            CurrentHealth = Mathf.Max(1f, config.health);
            MaxHealth = CurrentHealth;
            _speed = config.flySpeed;

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;
            _rb.mass = Mathf.Max(0.0001f, config.mass);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _shake = GetComponentInChildren<ShakeEffect>();
            _smoke = gameObject.AddComponent<SmokeTrail>();

            ApplyRotation();
            _active = true;
        }

        public void MountEngine(Vector3? local)
        {
            _engine = local;
            if (local.HasValue && _smoke != null) _smoke.Mount = local.Value;
        }

        public bool BeginBarrelRoll(float rateDeg, float grace)
        {
            if (!_active || _falling || BarrelRolling) return false;

            _barrelRate = Mathf.Max(BarrelMinRate, rateDeg);
            _barrelLeft = BarrelTurn / _barrelRate;
            _barrelAngle = 0f;
            _barrelGrace = Mathf.Max(0f, grace);
            return true;
        }

        void AdvanceBarrelRoll(float dt)
        {
            if (!BarrelRolling)
            {
                _evadeGrace = Mathf.Max(0f, _evadeGrace - dt);
                return;
            }

            _barrelLeft -= dt;
            if (_barrelLeft <= 0f)
            {
                _barrelLeft = 0f;
                _barrelAngle = 0f;
                _evadeGrace = _barrelGrace;
                return;
            }
            _barrelAngle += _barrelRate * dt;
        }

        public void Restore(float headingRad, float health)
        {
            _heading = headingRad;
            _targetHeading = headingRad;
            _angularVelocity = 0f;
            _roll.Settle(headingRad);
            CurrentHealth = Mathf.Clamp(health, 1f, MaxHealth);
            if (CurrentHealth < SmokeHealthThreshold && _smoke != null) _smoke.Arm(ExplosionSize);
            ApplyRotation();
        }

        public void SetControlled(bool value) => _controlled = value;

        public void Warp(Vector3 position, float headingRad)
        {
            transform.position = position;
            if (_rb != null)
            {
                _rb.position = position;
                _rb.linearVelocity = Vector3.zero;
            }
            _heading = headingRad;
            _targetHeading = headingRad;
            _angularVelocity = 0f;
            _roll.Settle(headingRad);
            ApplyRotation();
        }

        public void BeginHover(float speed, float floorY)
        {
            _hover = true;
            _hoverSpeed = Mathf.Max(0f, speed);
            _hoverFloorY = floorY;
            _heading = 0f;
            _targetHeading = 0f;
            _angularVelocity = 0f;

            Vector3 v = _rb != null ? _rb.linearVelocity : Vector3.zero;
            _hoverVelocity = new Vector2(v.x, v.y);
        }

        public void SetHoverInput(Vector2 input) => _hoverInput = Vector2.ClampMagnitude(input, 1f);

        public void SetWind(Vector2 drift, float pitchDeg, float rollDeg)
        {
            if (_falling || !_active) return;

            _windDrift = drift;
            _windPitch = pitchDeg;
            _windRoll = rollDeg;
        }

        public bool Steerable => _active && _controlled && !_falling;

        public float TargetHeading => _targetHeading;

        public void EnableHeadingSteering()
        {
            _headingSteering = true;
            _targetHeading = _heading;
        }

        public void SetTargetHeading(float headingRad) => _targetHeading = headingRad;

        public bool HeadingSteering => _headingSteering;

        public void ReleaseHeading() => _headingSteering = false;

        public bool BeginTurnBack(float seconds, float bankDeg)
        {
            if (!_active || _falling || _hover || TurningBack || _rb == null) return false;

            _turnSec = Mathf.Max(0.1f, seconds);
            _turnAge = 0f;
            _turnFrom = Mathf.Cos(_heading) < 0f ? -1f : 1f;
            _turnYaw = 0f;
            _turnBank = bankDeg;
            _turnZ = _rb.position.z;
            _turnLift = _rb.linearVelocity.y;
            _turnLean = Quaternion.Inverse(Quaternion.Euler(0f, _turnFrom > 0f ? 0f : 180f, 0f))
                        * transform.rotation;
            _angularVelocity = 0f;
            _barrelLeft = 0f;
            _barrelAngle = 0f;
            _rb.constraints &= ~RigidbodyConstraints.FreezePositionZ;
            return true;
        }

        public void FlyLevel()
        {
            _headingSteering = true;
            _targetHeading = 0f;
            _controlled = true;
        }

        public void Stop()
        {
            _active = false;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
        }

        public void Sink(float speed, float driftKeep)
        {
            _active = false;
            _falling = false;
            _fall = null;
            if (_smoke != null) _smoke.Clear();
            if (_fire != null) _fire.Extinguish();
            if (_rb == null) return;

            _rb.useGravity = false;
            Vector3 v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(v.x * driftKeep, -speed, 0f);
            _rb.angularVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            if (!_active || _config == null) return;

            float dt = Time.fixedDeltaTime;

            if (_falling)
            {
                _fall.Step(_rb, dt);
                _heading = _fall.Heading;
                ApplyRotation();
                return;
            }

            if (_hover)
            {
                Hover(dt);
                return;
            }

            _boost = Mathf.Abs(_boostTarget - _boost) < BoostSnap
                ? _boostTarget
                : _boost + (_boostTarget - _boost) * (1f - Mathf.Exp(-BoostResponse * dt));

            if (TurningBack)
            {
                TurnBack(dt);
                return;
            }

            _windBlend = Mathf.MoveTowards(_windBlend, 1f, dt / WindReturnSec);

            float maxRate = MaxTurnRate;
            float desiredRate;
            bool steady;

            if (_headingSteering)
            {
                desiredRate = _controlled
                    ? FlightSteering.SteerToHeading(_heading, _targetHeading, maxRate,
                        _rb.mass / Mathf.Max(0.0001f, _config.turnResponsiveness), _angularVelocity)
                    : 0f;
                steady = PlaneRoll.Steady(desiredRate, maxRate);
            }
            else
            {
                var kb = _controlled ? Keyboard.current : null;
                bool left  = kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed);
                bool right = kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed);

                desiredRate = (left ? maxRate : 0f) - (right ? maxRate : 0f);
                steady = !left && !right;
            }

            if (!_hardWalls)
                desiredRate = FlightSteering.EdgeSteer(_rb.position.x, _heading,
                    _minX, _maxX, _edgeMargin, maxRate, desiredRate);

            float approach = 1f - Mathf.Exp(-(_config.turnResponsiveness / _rb.mass) * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
            AdvanceBarrelRoll(dt);
            if (!BarrelRolling) _roll.Tick(dt, _heading, steady, _config.rotationSpeed);
            ApplyRotation();

            _speed = CruiseSpeed;
            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _speed
                          + new Vector3(_windDrift.x, _windDrift.y, 0f) * _windBlend;

            Vector3 pos = _rb.position;

            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            if (_hardWalls && pos.x <= _wallMinX && vel.x < 0f) vel.x = 0f;
            if (_hardWalls && pos.x >= _wallMaxX && vel.x > 0f) vel.x = 0f;
            bool clamped = _solid != null
                           && _solid.Repel(ref pos, ref vel, _solidRadius);
            _rb.linearVelocity = vel;

            if (pos.y > _ceilingY) { pos.y = _ceilingY; clamped = true; }
            if (_hardWalls && pos.x < _wallMinX) { pos.x = _wallMinX; clamped = true; }
            if (_hardWalls && pos.x > _wallMaxX) { pos.x = _wallMaxX; clamped = true; }

            if (GroundUnder(pos, out float deck) && pos.y < deck)
            {
                if (!_cinematic) { Crash(); return; }

                pos.y = deck;
                clamped = true;
                if (vel.y < 0f)
                {
                    vel.y = 0f;
                    _rb.linearVelocity = vel;
                }
                Scrape();
            }

            if (clamped) _rb.position = pos;
        }

        void Hover(float dt)
        {
            Vector2 input = (_controlled ? HoverKeys() : Vector2.zero) + _hoverInput;
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector2 target = input * _hoverSpeed;
            _hoverVelocity += (target - _hoverVelocity) * (1f - Mathf.Exp(-HoverResponse * dt));

            Vector3 pos = _rb.position;
            bool clamped = false;

            if (pos.x <= _wallMinX && _hoverVelocity.x < 0f) _hoverVelocity.x = 0f;
            if (pos.x >= _wallMaxX && _hoverVelocity.x > 0f) _hoverVelocity.x = 0f;
            if (pos.y >= _ceilingY && _hoverVelocity.y > 0f) _hoverVelocity.y = 0f;
            if (pos.y <= _hoverFloorY && _hoverVelocity.y < 0f) _hoverVelocity.y = 0f;

            if (pos.x < _wallMinX) { pos.x = _wallMinX; clamped = true; }
            if (pos.x > _wallMaxX) { pos.x = _wallMaxX; clamped = true; }
            if (pos.y > _ceilingY) { pos.y = _ceilingY; clamped = true; }
            if (pos.y < _hoverFloorY) { pos.y = _hoverFloorY; clamped = true; }

            _rb.linearVelocity = new Vector3(_hoverVelocity.x, _hoverVelocity.y, 0f);
            if (clamped) _rb.position = pos;

            float lean = _hoverSpeed > 0f ? Mathf.Clamp(_hoverVelocity.y / _hoverSpeed, -1f, 1f) : 0f;
            _bank += (lean * HoverBankDeg - _bank) * (1f - Mathf.Exp(-HoverBankResponse * dt));
            _heading = 0f;
            AdvanceBarrelRoll(dt);
            ApplyRotation();
        }

        void TurnBack(float dt)
        {
            float before = _turnAge / _turnSec;
            _turnAge = Mathf.Min(_turnAge + dt, _turnSec);
            float p = _turnAge / _turnSec;
            float mid = (before + p) * 0.5f;

            float course = TurnYaw(mid);
            Vector3 velocity = new Vector3(_turnFrom * Mathf.Cos(course), 0f, Mathf.Sin(course))
                               * (CruiseSpeed * TurnPace(mid));
            velocity.y = _turnLift * TurnSettle(mid);
            _rb.linearVelocity = velocity;

            _turnYaw = TurnYaw(p);
            float lead = TurnBankLead * (1f - TurnSettle(p));
            float rate = TurnRate(Mathf.Min(1f, p + lead)) / TurnPeakRate;
            float bank = _turnFrom * _turnBank * Mathf.Clamp(rate, -1f, 1f);
            float yaw = _turnYaw * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, _turnFrom > 0f ? -yaw : 180f + yaw, 0f)
                               * Quaternion.Slerp(Quaternion.identity, _turnLean, TurnSettle(p))
                               * Quaternion.Euler(bank, 0f, 0f);

            if (_turnAge >= _turnSec) EndTurnBack();
        }

        void EndTurnBack()
        {
            _turnSec = 0f;
            _windBlend = 0f;
            _heading = _turnFrom > 0f ? Mathf.PI : 0f;
            _targetHeading = _heading;
            _angularVelocity = 0f;
            _roll.Settle(_heading);

            Vector3 pos = _rb.position;
            _rb.position = new Vector3(pos.x, pos.y, _turnZ);
            _rb.linearVelocity = new Vector3(Mathf.Cos(_heading) * CruiseSpeed, 0f, 0f);
            _rb.constraints |= RigidbodyConstraints.FreezePositionZ;
            ApplyRotation();
        }

        static float TurnYaw(float p)
        {
            float yaw = Mathf.SmoothStep(0f, Mathf.PI, p / TurnMainShare);
            if (p <= TurnSwingFrom) return yaw;

            float swing = Mathf.Sin(Mathf.PI * (p - TurnSwingFrom) / (1f - TurnSwingFrom));
            return yaw + TurnOvershoot * swing * swing;
        }

        static float TurnRate(float p)
        {
            float a = Mathf.Max(0f, p - TurnRateProbe);
            float b = Mathf.Min(1f, p + TurnRateProbe);
            return (TurnYaw(b) - TurnYaw(a)) / Mathf.Max(1e-4f, b - a);
        }

        static float TurnSettle(float p) => 1f - Mathf.SmoothStep(0f, 1f, p / TurnSettleShare);

        static float TurnPace(float p)
        {
            if (p >= TurnSlowUntil) return 1f;

            float dip = Mathf.Sin(Mathf.PI * p / TurnSlowUntil);
            return 1f - TurnSlow * dip * dip;
        }

        static Vector2 HoverKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                      - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                      - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }

        static bool GroundUnder(Vector3 pos, out float deck)
        {
            bool hit = Physics.Raycast(pos + Vector3.up * GroundProbe, Vector3.down,
                out RaycastHit info, GroundProbe * 2f, 1 << ProceduralTerrain.GroundLayer,
                QueryTriggerInteraction.Ignore);

            deck = hit ? info.point.y + GroundSkim : 0f;
            return hit;
        }

        public void SetSolid(ISolid solid, float radius = PlaneScrapes.HitboxRadius)
        {
            _solid = solid;
            _solidRadius = radius;
        }

        public void SetLeftWall(float x) => _wallMinX = Mathf.Max(_wallMinX, x);

        public void SetRightWall(float x) => _wallMaxX = Mathf.Min(_wallMaxX, x);

        public void SetCeiling(float y) => _ceilingY = y;

        public void SetWalls(float minX, float maxX)
        {
            _wallMinX = minX;
            _wallMaxX = maxX;
        }

        public void ClearWalls()
        {
            _wallMinX = float.NegativeInfinity;
            _wallMaxX = float.PositiveInfinity;
        }

        public void SetBoost(bool on) => _boostTarget = on && _config != null
            ? Mathf.Max(1f, _config.boostMultiplier)
            : 1f;

        public void SetCinematic(bool value)
        {
            if (_cinematic == value) return;

            _cinematic = value;
            PlaneScrapes.SetGroundCollisions(!value);
        }

        float CruiseSpeed => _config.flySpeed * _boost;

        void ApplyRotation()
        {
            float roll = _roll.Angle + _barrelAngle + _bank + _windRoll * _windBlend
                         + (_fall != null ? _fall.Roll : 0f);
            float pitch = _heading * Mathf.Rad2Deg + _windPitch * _windBlend;
            transform.rotation = Quaternion.Euler(0f, 0f, pitch) * Quaternion.Euler(roll, 0f, 0f);
        }

        public void TakeDamage(float amount)
        {
            if (!_active || _falling) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnDamaged?.Invoke();
            if (CurrentHealth < SmokeHealthThreshold && _smoke != null) _smoke.Arm(ExplosionSize);
            if (CurrentHealth <= 0f) BeginFall();
        }

        public void Heal(float amount)
        {
            if (!_active || _falling || amount <= 0f) return;

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            if (CurrentHealth >= SmokeHealthThreshold && _smoke != null) _smoke.Disarm();
        }

        public void Bump()
        {
            if (!_active || _falling) return;

            if (_shake != null) _shake.Play();
        }

        public void Brush()
        {
            if (!_active || _falling || Evading) return;
            OnScraped?.Invoke();
        }

        public bool Scrape()
        {
            if (!_active || _falling || Evading) return false;
            if (Time.time - _lastCollisionTime < CollisionCooldown) return false;
            _lastCollisionTime = Time.time;

            if (!_cinematic)
            {
                TakeDamage(CollisionDamage);
                Sparks.Spawn(transform.position, ExplosionSize);
            }
            if (_shake != null) _shake.Play();
            OnScraped?.Invoke();
            return true;
        }

        void BeginFall()
        {
            if (TurningBack) EndTurnBack();
            _falling = true;
            _windDrift = Vector2.zero;
            _windPitch = 0f;
            _windRoll = 0f;
            _barrelLeft = 0f;
            _barrelAngle = 0f;
            _evadeGrace = 0f;
            OnShotDown?.Invoke();

            if (_smoke != null) _smoke.Ignite(ExplosionSize);
            _fire = PlaneFire.Ignite(gameObject, ExplosionSize, _engine);

            _fall = PlaneFall.Begin(_rb, _heading, Mathf.Max(_speed, CruiseSpeed));
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == BattlefieldProps.Layer) Scrape();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!_active) return;

            if (collision.gameObject.GetComponent<Bullet>() != null) return;

            if (collision.gameObject.GetComponent<Bomb>() != null) return;

            if (collision.gameObject.GetComponentInParent<EnemyController>() != null) return;

            if (_cinematic) { Scrape(); return; }

            Crash();
        }

        void Crash()
        {
            if (!_active) return;
            _active = false;

            Explosion.Spawn(transform.position, ExplosionSize);
            StartCoroutine(HideModelAfter(Explosion.RemovalDelay));

            OnCrashed?.Invoke();
        }

        IEnumerator HideModelAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideModel();
        }

        void HideModel()
        {
            if (_smoke != null) _smoke.Clear();
            if (_fire != null) _fire.Extinguish();

            foreach (Transform child in transform)
                child.gameObject.SetActive(false);
        }
    }
}
