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

        public float MaxTurnRate => _config != null
            ? _config.rotationSpeed * Mathf.Deg2Rad * _boost
            : 0f;

        public bool Boosting => _boostTarget > 1f;

        const float ExplosionSize = 60f;

        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;

        const float SmokeHealthThreshold = 30f;

        const float BoostResponse = 3.5f;
        const float BoostSnap = 0.001f;

        const float GroundProbe = 500f;
        const float GroundSkim = 14f;

        PlayerConfig _config;
        Rigidbody _rb;
        ShakeEffect _shake;
        SmokeTrail _smoke;
        PlaneFire _fire;
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

        bool _hardLeftWall;
        float _wallX = float.NegativeInfinity;

        bool _headingSteering;
        float _targetHeading;

        public void Initialize(PlayerConfig config, float startHeadingRad, float minX, float maxX,
            float ceilingY, float edgeMargin, bool hardLeftWall = false)
        {
            _hardLeftWall = hardLeftWall;
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

        public void SetControlled(bool value) => _controlled = value;

        public bool Steerable => _active && _controlled && !_falling;

        public float TargetHeading => _targetHeading;

        public void EnableHeadingSteering()
        {
            _headingSteering = true;
            _targetHeading = _heading;
        }

        public void SetTargetHeading(float headingRad) => _targetHeading = headingRad;

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

            _boost = Mathf.Abs(_boostTarget - _boost) < BoostSnap
                ? _boostTarget
                : _boost + (_boostTarget - _boost) * (1f - Mathf.Exp(-BoostResponse * dt));

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

            if (!_hardLeftWall)
                desiredRate = FlightSteering.EdgeSteer(_rb.position.x, _heading,
                    _minX, _maxX, _edgeMargin, maxRate, desiredRate);

            float approach = 1f - Mathf.Exp(-(_config.turnResponsiveness / _rb.mass) * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
            _roll.Tick(dt, _heading, steady, _config.rotationSpeed);
            ApplyRotation();

            _speed = CruiseSpeed;
            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _speed;

            Vector3 pos = _rb.position;

            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            if (_hardLeftWall && pos.x <= _wallX && vel.x < 0f) vel.x = 0f;
            _rb.linearVelocity = vel;

            bool clamped = false;
            if (pos.y > _ceilingY) { pos.y = _ceilingY; clamped = true; }
            if (_hardLeftWall && pos.x < _wallX) { pos.x = _wallX; clamped = true; }

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

        static bool GroundUnder(Vector3 pos, out float deck)
        {
            bool hit = Physics.Raycast(pos + Vector3.up * GroundProbe, Vector3.down,
                out RaycastHit info, GroundProbe * 2f, 1 << ProceduralTerrain.GroundLayer,
                QueryTriggerInteraction.Ignore);

            deck = hit ? info.point.y + GroundSkim : 0f;
            return hit;
        }

        public void SetLeftWall(float x) => _wallX = Mathf.Max(_wallX, x);

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
            float roll = _roll.Angle + (_fall != null ? _fall.Roll : 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg)
                               * Quaternion.Euler(roll, 0f, 0f);
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
            OnScraped?.Invoke();
        }

        public bool Scrape()
        {
            if (!_active || _falling) return false;
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
            _falling = true;
            OnShotDown?.Invoke();

            if (_smoke != null) _smoke.Ignite(ExplosionSize);
            _fire = PlaneFire.Ignite(gameObject, ExplosionSize);

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
