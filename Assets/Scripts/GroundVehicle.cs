using System;
using UnityEngine;

namespace MetalRaptors
{
    public abstract class GroundVehicle : MonoBehaviour, IDamageable
    {
        public const float FenceStandoff = 45f;
        public const float QueueGap = 30f;

        protected const float ShotVolume = 0.18f;
        protected const float Epsilon = 0.0001f;

        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;
        const float SmokeHealthThreshold = 30f;
        const float BurnSeconds = 4f;

        const float BarWidth = 36f;
        const float BarHeight = 3.2f;
        const float BarLiftMargin = 10f;

        public event Action<GroundVehicle> OnDestroyed;

        public float CurrentHealth { get; private set; }

        public bool IsAlive => !_dead;

        public bool Halted => _stopped;

        public Vector3 Size => _size;

        public float ModelSize => Mathf.Max(_size.x, Mathf.Max(_size.y, _size.z));

        public Vector3 Centre => transform.position + Vector3.up * (_size.y * 0.5f);

        public float RearX => transform.position.x + _size.x * 0.5f;

        public abstract float MaxHealth { get; }

        protected abstract float DriveSpeed { get; }

        protected Rigidbody Target => _target;

        protected float PlaneZ => _z;

        protected Collider Hitbox => _collider;

        Vector3 _size;
        Rigidbody _target;
        CampaignTerrain _land;
        Camera _cam;
        BoxCollider _collider;
        SmokeTrail _smoke;
        PlaneFire _fire;
        AudioSource _audio;
        AudioClip _shotClip;
        GroundVehicle _ahead;

        float _fenceStopX;
        float _roadLift;
        float _z;
        float _lastCollisionTime = -999f;
        bool _stopped;
        bool _standDown;
        bool _dead;
        bool _reported;

        Transform _bar;
        Transform _barFillPivot;
        Renderer _barFill;

        protected void Begin(Vector3 size, float fenceStopX, float roadLift, Rigidbody target,
            CampaignTerrain land)
        {
            _size = size;
            _fenceStopX = fenceStopX;
            _roadLift = roadLift;
            _target = target;
            _land = land;
            _z = transform.position.z;
            _cam = Camera.main;
            CurrentHealth = MaxHealth;

            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.center = new Vector3(0f, _size.y * 0.5f, 0f);
            _collider.size = _size;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _smoke = gameObject.AddComponent<SmokeTrail>();

            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            BuildHealthBar();
            Settle(transform.position.x);
        }

        public void SetAhead(GroundVehicle ahead) => _ahead = ahead;

        public void StandDown() => _standDown = true;

        public bool Touches(Vector3 point, float radius)
        {
            if (_dead) return false;

            Vector3 centre = Centre;
            Vector3 half = _size * 0.5f + Vector3.one * radius;

            return Mathf.Abs(point.x - centre.x) <= half.x
                && Mathf.Abs(point.y - centre.y) <= half.y
                && Mathf.Abs(point.z - centre.z) <= half.z;
        }

        public bool Scrape()
        {
            if (_dead) return false;
            if (Time.time - _lastCollisionTime < CollisionCooldown) return false;
            _lastCollisionTime = Time.time;

            ApplyDamage(CollisionDamage);
            if (!_dead) Sparks.Spawn(Centre, ModelSize);
            return true;
        }

        public void TakeDamage(float amount)
        {
            if (_dead) return;
            ApplyDamage(amount);
        }

        void Update()
        {
            if (_dead) return;

            float dt = Time.deltaTime;
            Drive(dt);
            if (!_standDown) Aim(dt);
            PlaceHealthBar();
        }

        void Drive(float dt)
        {
            if (_standDown) return;

            float x = transform.position.x;
            float next = Mathf.Max(StopLine(), x - DriveSpeed * dt);

            _stopped = next >= x - Epsilon;
            if (_stopped) return;

            Settle(next);
            Roll(dt);
        }

        float StopLine()
        {
            float line = _fenceStopX;
            if (_ahead != null && _ahead.IsAlive)
                line = Mathf.Max(line, _ahead.RearX + QueueGap + _size.x * 0.5f);
            return line;
        }

        void Settle(float x)
        {
            float y = _land != null && _land.SampleHeight(x, _z, out float ground)
                ? ground
                : ProceduralTerrain.BaseLevel;

            transform.position = new Vector3(x, y + _roadLift, _z);
        }

        protected abstract void Aim(float dt);

        protected virtual void Roll(float dt) { }

        protected bool OnCamera(Vector3 worldPoint)
        {
            if (_cam == null) return true;

            Vector3 vp = _cam.WorldToViewportPoint(worldPoint);
            return vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        protected Vector3 Intercept(Vector3 muzzle, float bulletSpeed, float lead)
        {
            if (_target == null) return muzzle;

            Vector3 point = _target.position;
            Vector3 velocity = _target.linearVelocity;

            float t = 0f;
            for (int i = 0; i < 2; i++)
            {
                float distance = Vector3.Distance(muzzle, point + velocity * (t * lead));
                t = bulletSpeed > 0f ? distance / bulletSpeed : 0f;
            }
            return point + velocity * (t * lead);
        }

        protected void FireRound(GameObject template, Vector3 muzzle, Vector3 dir, float speed,
            float damage, float flashSize)
        {
            if (template == null) return;

            GameObject go = Instantiate(template, muzzle,
                Quaternion.FromToRotation(Vector3.up, dir));
            go.name = "VehicleShell";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, speed, damage, _collider, fromEnemy: true);

            MuzzleFlash.Spawn(muzzle, dir, flashSize);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Sfx);
        }

        void ApplyDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            UpdateHealthBar();
            if (CurrentHealth < SmokeHealthThreshold && _smoke != null) _smoke.Arm(ModelSize);
            if (CurrentHealth <= 0f) Die();
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;
            _stopped = true;

            Explosion.Spawn(Centre, ModelSize);
            if (_smoke != null) _smoke.Ignite(ModelSize);
            _fire = PlaneFire.Ignite(gameObject, ModelSize);
            if (_collider != null) _collider.enabled = false;
            if (_bar != null) Destroy(_bar.gameObject);

            Report();
            Destroy(gameObject, BurnSeconds);
        }

        void Report()
        {
            if (_reported) return;
            _reported = true;
            OnDestroyed?.Invoke(this);
        }

        void BuildHealthBar()
        {
            _bar = new GameObject("VehicleHealthBar").transform;

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
            if (_barFillPivot == null || _barFill == null) return;

            float frac = Mathf.Clamp01(CurrentHealth / Mathf.Max(1f, MaxHealth));
            Vector3 s = _barFillPivot.localScale;
            s.x = frac;
            _barFillPivot.localScale = s;

            var color = Color.Lerp(new Color(0.95f, 0.2f, 0.12f),
                new Color(0.25f, 0.9f, 0.3f), frac);
            var mat = _barFill.sharedMaterial;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_EmissionColor", color * 2f);
        }

        void PlaceHealthBar()
        {
            if (_bar != null)
                _bar.position = transform.position + Vector3.up * (_size.y + BarLiftMargin);
        }

        protected virtual void OnDestroy()
        {
            Report();
            if (_bar != null) Destroy(_bar.gameObject);
            if (_fire != null) _fire.Extinguish();
        }

        protected static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }
    }
}
