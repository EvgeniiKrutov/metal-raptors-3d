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

        public float MaxTurnRate => _config != null ? _config.rotationSpeed * Mathf.Deg2Rad : 0f;

        const float FallGravity = 150f;
        const float FallInitialDrop = 25f;
        const float FallHorizontalDrag = 1.5f;
        const float ExplosionSize = 60f;

        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;

        const float SmokeHealthThreshold = 30f;

        PlayerConfig _config;
        Rigidbody _rb;
        ShakeEffect _shake;
        SmokeTrail _smoke;

        float _heading;
        float _angularVelocity;
        float _speed;
        bool _active;
        bool _falling;
        float _lastCollisionTime = -999f;

        float _minX, _maxX, _worldWidth, _ceilingY, _edgeMargin;

        bool _hardLeftWall;
        float _wallX = float.NegativeInfinity;

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
            if (_smoke != null) _smoke.Clear();
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
                Vector3 v = _rb.linearVelocity;
                v.x = Mathf.MoveTowards(v.x, 0f, Mathf.Abs(v.x) * FallHorizontalDrag * dt);
                _rb.linearVelocity = v;
                return;
            }

            var kb = Keyboard.current;
            bool left  = kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed);
            bool right = kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed);

            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad;
            float desiredRate = (left ? maxRate : 0f) - (right ? maxRate : 0f);

            if (!_hardLeftWall)
                desiredRate = FlightSteering.EdgeSteer(_rb.position.x, _heading,
                    _minX, _maxX, _edgeMargin, maxRate, desiredRate);

            float approach = 1f - Mathf.Exp(-(_config.turnResponsiveness / _rb.mass) * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
            ApplyRotation();

            UpdateSpeed(dt);
            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _speed;

            Vector3 pos = _rb.position;

            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            if (_hardLeftWall && pos.x <= _wallX && vel.x < 0f) vel.x = 0f;
            _rb.linearVelocity = vel;

            bool clamped = false;
            if (pos.y > _ceilingY) { pos.y = _ceilingY; clamped = true; }
            if (_hardLeftWall && pos.x < _wallX) { pos.x = _wallX; clamped = true; }
            if (clamped) _rb.position = pos;
        }

        public void SetLeftWall(float x) => _wallX = Mathf.Max(_wallX, x);

        void UpdateSpeed(float dt)
        {
            _speed += -Mathf.Sin(_heading) * _config.diveAcceleration * dt;
            _speed -= (_speed - _config.flySpeed) * _config.speedDrag * dt;
            _speed = Mathf.Clamp(_speed, _config.flySpeed,
                _config.flySpeed * Mathf.Max(1f, _config.maxSpeedMultiplier));
        }

        void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg);
        }

        public void TakeDamage(float amount)
        {
            if (!_active || _falling) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnDamaged?.Invoke();
            if (CurrentHealth < SmokeHealthThreshold && _smoke != null) _smoke.Arm(ExplosionSize);
            if (CurrentHealth <= 0f) BeginFall();
        }

        public bool Scrape()
        {
            if (!_active || _falling) return false;
            if (Time.time - _lastCollisionTime < CollisionCooldown) return false;
            _lastCollisionTime = Time.time;

            TakeDamage(CollisionDamage);
            if (_shake != null) _shake.Play();
            Sparks.Spawn(transform.position, ExplosionSize);
            OnScraped?.Invoke();
            return true;
        }

        void BeginFall()
        {
            _falling = true;
            OnShotDown?.Invoke();

            Physics.gravity = new Vector3(0f, -FallGravity, 0f);
            _rb.useGravity = true;
            Vector3 v = _rb.linearVelocity;
            v.y -= FallInitialDrop;
            _rb.linearVelocity = v;
            _rb.angularVelocity = new Vector3(0f, 0f,
                (UnityEngine.Random.value < 0.5f ? -1f : 1f) * 2.5f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == BattlefieldProps.Layer) Scrape();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!_active) return;

            if (collision.gameObject.GetComponent<Bullet>() != null) return;

            if (collision.gameObject.GetComponentInParent<EnemyController>() != null) return;

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

            foreach (Transform child in transform)
                child.gameObject.SetActive(false);
        }
    }
}
