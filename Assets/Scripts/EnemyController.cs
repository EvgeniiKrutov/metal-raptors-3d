using System;
using UnityEngine;

namespace MetalRaptors
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        const float ShotVolume = 0.15f;
        const float RecoverClimbAngleDeg = 70f;

        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;

        const float SmokeHealthThreshold = 30f;

        const float BarWidth = 36f;
        const float BarHeight = 3.2f;
        const float BarLiftMargin = 8f;

        public event Action<EnemyController> OnDestroyed;

        public float CurrentHealth { get; private set; }

        public bool IsAlive => !_dead && !_falling;

        public Collider Hitbox => _collider;

        public float ModelSize => _bodyRadius > 0f ? _bodyRadius * 2f : 30f;

        enum AiState { Attack, Fly, Evade, Recover, Return }
        enum EvadePhase { Roll, Jitter, Unroll }

        EnemyConfig _config;
        Rigidbody _target;
        Rigidbody _rb;
        Collider _collider;
        Camera _cam;
        float _bodyRadius;

        float _heading;
        float _angularVelocity;
        bool _dead;
        bool _falling;
        bool _reported;
        float _fallTimer;
        bool _standDown;

        float _minX, _maxX, _groundY, _ceilingY, _edgeMargin;

        AiState _state = AiState.Attack;
        float _stateTimer;
        EvadePhase _evadePhase;
        float _evadeHeading;
        float _evadeRollSign = 1f;
        float _evadeRollAccum;
        float _jitterTimer;
        float _jitterOffset;
        float _flyWeaveT;
        float _flyBaseX;
        float _fireCooldown;
        float _lastCollisionTime = -999f;
        ShakeEffect _shake;
        SmokeTrail _smoke;
        PlaneFire _fire;

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;

        Transform _bar;
        Transform _barFillPivot;
        Renderer _barFill;

        public void Initialize(EnemyConfig config, Rigidbody target,
            float minX, float maxX, float groundY, float ceilingY, float edgeMargin)
        {
            _config = config;
            _target = target;
            _minX = minX;
            _maxX = maxX;
            _groundY = groundY;
            _ceilingY = ceilingY;
            _edgeMargin = edgeMargin;

            CurrentHealth = Mathf.Max(1f, config.health);
            _stateTimer = config.attackDuration;

            Vector3 to = (target != null ? target.position : Vector3.zero) - transform.position;
            _heading = Mathf.Atan2(to.y, to.x);

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;
            _rb.mass = Mathf.Max(0.0001f, config.mass);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _collider = GetComponentInChildren<Collider>();
            _shake = GetComponentInChildren<ShakeEffect>();
            _bodyRadius = MeasureBodyRadius();
            _smoke = gameObject.AddComponent<SmokeTrail>();
            _bulletTemplate = Bullet.BuildTemplate(Bullet.RoundColor);

            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            _cam = Camera.main;

            BuildHealthBar();
            ApplyRotation();
        }

        public void StandDown() => _standDown = true;

        public void SetBounds(float minX, float maxX)
        {
            _minX = minX;
            _maxX = maxX;
        }

        void FixedUpdate()
        {
            if (_dead || _config == null) return;

            float dt = Time.fixedDeltaTime;

            if (_falling)
            {
                TickFall(dt);
                return;
            }

            _stateTimer = Mathf.Max(0f, _stateTimer - dt);
            _jitterTimer = Mathf.Max(0f, _jitterTimer - dt);
            _fireCooldown -= dt;

            if (_standDown)
            {
                if (_state != AiState.Fly) EnterFly(transform.position.x);
                _flyWeaveT += dt;
            }
            else if (CheckGroundAvoidance())
            {
            }
            else if (!IsOnCamera(transform.position))
            {
                _state = AiState.Return;
            }
            else
            {
                if (_state == AiState.Return) EnterAttack();
                TickState(dt);
            }

            SteerToHeading(ComputeHeading(dt), dt);

            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _config.flySpeed;
            Vector3 pos = _rb.position;
            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            _rb.linearVelocity = vel;
            if (pos.y > _ceilingY) { pos.y = _ceilingY; _rb.position = pos; }

            if (!_standDown && _state != AiState.Fly && _state != AiState.Return)
                UpdateFiring();
        }

        void TickFall(float dt)
        {
            PlaneFall.Step(_rb, dt);

            _fallTimer += dt;
            if (_fallTimer >= PlaneFall.Timeout) RemoveWreck();
        }

        bool CheckGroundAvoidance()
        {
            if (transform.position.y - _groundY >= _config.minAltitudeMargin) return false;
            _state = AiState.Recover;
            return true;
        }

        void TickState(float dt)
        {
            if (_state == AiState.Recover)
            {
                if (transform.position.y - _groundY >= _config.safeAltitudeMargin) EnterAttack();
                return;
            }

            if (_state == AiState.Attack && _stateTimer <= 0f)
            {
                EnterFly(_target != null ? _target.position.x : transform.position.x);
                return;
            }

            if (_state == AiState.Fly && _stateTimer <= 0f)
            {
                EnterAttack();
                return;
            }

            if (_state == AiState.Evade && _evadePhase == EvadePhase.Jitter && _stateTimer <= 0f)
            {
                _evadePhase = EvadePhase.Unroll;
                _evadeRollAccum = 0f;
            }

            if (_state == AiState.Fly) _flyWeaveT += dt;
        }

        void EnterAttack()
        {
            _state = AiState.Attack;
            _stateTimer = _config.attackDuration;
        }

        void EnterFly(float baseX)
        {
            _state = AiState.Fly;
            _stateTimer = _config.flyDuration;
            _flyWeaveT = 0f;
            _flyBaseX = baseX;
        }

        void EnterEvade()
        {
            Vector3 away = transform.position
                         - (_target != null ? _target.position : transform.position - Vector3.right);
            _evadeHeading = Mathf.Atan2(away.y, away.x);
            _evadePhase = EvadePhase.Roll;
            _evadeRollSign = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            _evadeRollAccum = 0f;
            _jitterTimer = 0f;
            _jitterOffset = 0f;
            _stateTimer = _config.evadeDuration;
            _state = AiState.Evade;
        }

        float ComputeHeading(float dt)
        {
            switch (_state)
            {
                case AiState.Recover:
                {
                    float climb = RecoverClimbAngleDeg * Mathf.Deg2Rad;
                    return Mathf.Cos(_heading) >= 0f ? climb : Mathf.PI - climb;
                }

                case AiState.Evade:
                {
                    if (_evadePhase == EvadePhase.Roll || _evadePhase == EvadePhase.Unroll)
                    {
                        float sign = _evadePhase == EvadePhase.Roll ? _evadeRollSign : -_evadeRollSign;
                        _evadeRollAccum += _config.rotationSpeed * Mathf.Deg2Rad * dt;
                        if (_evadeRollAccum >= Mathf.PI * 2f)
                        {
                            if (_evadePhase == EvadePhase.Roll)
                            {
                                _evadePhase = EvadePhase.Jitter;
                                _evadeRollAccum = 0f;
                            }
                            else
                            {
                                EnterAttack();
                                return HeadingTo(PredictIntercept());
                            }
                        }
                        return _heading + sign * Mathf.PI * 0.9f;
                    }

                    if (_jitterTimer <= 0f)
                    {
                        _jitterOffset = UnityEngine.Random.Range(-1f, 1f)
                                      * _config.jitterAmplitude * Mathf.Deg2Rad;
                        _jitterTimer = 1f / Mathf.Max(0.01f, _config.jitterHz);
                    }
                    return _evadeHeading + _jitterOffset;
                }

                case AiState.Fly:
                {
                    float targetY = _groundY + (_ceilingY - _groundY) * _config.flyAltitudeFactor;
                    float weaveX = _flyBaseX + Mathf.Sin(_flyWeaveT * Mathf.PI * 2f * _config.weaveHz)
                                             * _config.weaveAmplitude;
                    return HeadingTo(new Vector2(weaveX, targetY));
                }

                case AiState.Return:
                    return _target != null ? HeadingTo((Vector2)_target.position) : _heading;

                case AiState.Attack:
                default:
                    return HeadingTo(PredictIntercept());
            }
        }

        void SteerToHeading(float targetHeading, float dt)
        {
            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad;
            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, targetHeading * Mathf.Rad2Deg)
                        * Mathf.Deg2Rad;
            float desiredRate = Mathf.Clamp(dt > 0f ? error / dt : 0f, -maxRate, maxRate);

            desiredRate = FlightSteering.EdgeSteer(_rb.position.x, _heading,
                _minX, _maxX, _edgeMargin, maxRate, desiredRate);

            float approach = 1f - Mathf.Exp(-(_config.turnResponsiveness / _rb.mass) * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
            ApplyRotation();
        }

        void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg);
        }

        float HeadingTo(Vector2 point)
        {
            return Mathf.Atan2(point.y - transform.position.y, point.x - transform.position.x);
        }

        float MeasureBodyRadius()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 15f;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f;
        }

        void UpdateFiring()
        {
            if (_target == null || !IsOnCamera(_target.position)) return;

            if (Vector2.Distance(transform.position, _target.position) > _config.maxFireRange)
                return;

            Vector2 aim = PredictIntercept();
            float aimErrorDeg = Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg,
                HeadingTo(aim) * Mathf.Rad2Deg));
            if (aimErrorDeg > _config.fireAngleThreshold) return;

            if (_fireCooldown > 0f) return;
            _fireCooldown = Mathf.Max(0.01f, _config.fireRate);

            Vector3 dir = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f);
            Vector3 muzzle = transform.position + dir * (_bodyRadius + 6f);
            var go = Instantiate(_bulletTemplate, muzzle,
                transform.rotation * Quaternion.Euler(0f, 0f, -90f));
            go.name = "EnemyBullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, _config.bulletSpeed, _config.damage, _collider);

            MuzzleFlash.Spawn(muzzle, dir, _bodyRadius);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume);
        }

        Vector2 PredictIntercept()
        {
            if (_target == null) return transform.position;

            Vector2 tp = _target.position;
            Vector2 tv = _target.linearVelocity;
            float t = 0f;
            for (int i = 0; i < 2; i++)
            {
                float d = Vector2.Distance(transform.position, tp + tv * (t * _config.leadFactor));
                t = _config.bulletSpeed > 0f ? d / _config.bulletSpeed : 0f;
            }
            return tp + tv * (t * _config.leadFactor);
        }

        bool IsOnCamera(Vector3 worldPos)
        {
            if (_cam == null) return true;
            Vector3 vp = _cam.WorldToViewportPoint(worldPos);
            return vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        public void TakeDamage(float amount)
        {
            if (_dead || _falling) return;
            ApplyDamage(amount);

            if (!_falling && (_state == AiState.Attack || _state == AiState.Fly)) EnterEvade();
        }

        void ApplyDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            UpdateHealthBar();
            if (CurrentHealth < SmokeHealthThreshold && _smoke != null) _smoke.Arm(ModelSize);
            if (CurrentHealth <= 0f) BeginFall();
        }

        void BeginFall()
        {
            if (_dead || _falling) return;
            _falling = true;

            if (_smoke != null) _smoke.Ignite(ModelSize);
            _fire = PlaneFire.Ignite(gameObject, ModelSize);

            PlaneFall.Begin(_rb);
            if (_bar != null) Destroy(_bar.gameObject);

            ReportDestroyed();
        }

        void RemoveWreck()
        {
            _dead = true;
            if (_smoke != null) _smoke.Clear();

            ReportDestroyed();
            Destroy(gameObject);
        }

        void ReportDestroyed()
        {
            if (_reported) return;
            _reported = true;
            OnDestroyed?.Invoke(this);
        }

        public void Explode()
        {
            if (_dead) return;
            _dead = true;

            Explosion.Spawn(transform.position, ModelSize);
            if (_smoke != null) _smoke.Clear();
            if (_fire != null) _fire.Extinguish();

            if (_rb != null) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            if (_collider != null) _collider.enabled = false;
            if (_bar != null) Destroy(_bar.gameObject);

            ReportDestroyed();
            Destroy(gameObject, Explosion.RemovalDelay);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == BattlefieldProps.Layer) Scrape();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_dead) return;

            if (collision.gameObject.GetComponent<Bullet>() != null) return;

            Explode();
        }

        public bool Scrape()
        {
            if (_dead || _falling) return false;
            if (Time.time - _lastCollisionTime < CollisionCooldown) return false;
            _lastCollisionTime = Time.time;

            ApplyDamage(CollisionDamage);
            if (!_falling)
            {
                if (_shake != null) _shake.Play();
                Sparks.Spawn(transform.position, ModelSize);
            }
            return true;
        }

        void BuildHealthBar()
        {
            _bar = new GameObject("EnemyHealthBar").transform;

            var back = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                Vector3.zero, new Vector3(BarWidth, BarHeight, 0.5f),
                new Color(0.06f, 0.06f, 0.06f), emissive: false, keepCollider: false);
            back.name = "Back";
            back.transform.SetParent(_bar, false);

            _barFillPivot = new GameObject("FillPivot").transform;
            _barFillPivot.SetParent(_bar, false);
            _barFillPivot.localPosition = new Vector3(-BarWidth / 2f, 0f, -0.5f);

            var fill = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                Vector3.zero, new Vector3(BarWidth - 1f, BarHeight - 0.8f, 0.4f),
                new Color(0.25f, 0.9f, 0.3f), emissive: true, keepCollider: false);
            fill.name = "Fill";
            fill.transform.SetParent(_barFillPivot, false);
            fill.transform.localPosition = new Vector3((BarWidth - 1f) / 2f, 0f, 0f);
            _barFill = fill.GetComponent<Renderer>();

            UpdateHealthBar();
            PlaceHealthBar();
        }

        void UpdateHealthBar()
        {
            if (_barFillPivot == null || _config == null) return;

            float frac = Mathf.Clamp01(CurrentHealth / Mathf.Max(1f, _config.health));
            Vector3 s = _barFillPivot.localScale;
            s.x = frac;
            _barFillPivot.localScale = s;

            var color = Color.Lerp(new Color(0.95f, 0.2f, 0.12f), new Color(0.25f, 0.9f, 0.3f), frac);
            var mat = _barFill.sharedMaterial;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_EmissionColor", color * 2f);
        }

        void PlaceHealthBar()
        {
            if (_bar != null)
                _bar.position = transform.position + Vector3.up * (_bodyRadius + BarLiftMargin);
        }

        void LateUpdate()
        {
            if (!_dead) PlaceHealthBar();
        }

        void OnDestroy()
        {
            if (_bar != null) Destroy(_bar.gameObject);
        }
    }
}
