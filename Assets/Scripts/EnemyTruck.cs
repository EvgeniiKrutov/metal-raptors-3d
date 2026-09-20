using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class EnemyTruck : MonoBehaviour, IDamageable
    {
        public const string ModelResource = "objects/machines/truck_ww1";

        public const float MaxHealth = 100f;
        public const float DriveSpeed = 30f;
        public const float FenceStandoff = 45f;

        const float BulletDamage = 6f;
        const float BulletSpeed = 400f;
        const float FireInterval = 1f;
        const float FireRange = 500f;
        const float LeadFactor = 1f;
        const float ShotVolume = 0.18f;

        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;

        const float BedRear = 0.16f;
        const float BedHeight = 0.86f;
        const float MuzzleClear = 5f;
        const float FenceSparkSize = 16f;

        const float SmokeHealthThreshold = 30f;
        const float BurnSeconds = 4f;

        const float BarWidth = 36f;
        const float BarHeight = 3.2f;
        const float BarLiftMargin = 10f;

        const float ProbeY = -9000f;
        const float Epsilon = 0.0001f;
        const float LiftGamma = 1f / 2.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Dictionary<Material, Material> Lifted =
            new Dictionary<Material, Material>();

        static readonly Quaternion RoadYaw = Quaternion.Euler(0f, -90f, 0f);

        static readonly string[] WheelNodes =
            { "Wheel_FL_Spin", "Wheel_FR_Spin", "Wheel_RL_Spin", "Wheel_RR_Spin" };

        static GameObject _prefab;
        static bool _measured;
        static float _scale = PlaneModelConfig.UnitsPerMeter;
        static Vector3 _size;
        static Vector3 _yawedMin;

        public static bool Ready => _measured && _prefab != null;

        public static float BodyLength => _size.x;

        public event Action<EnemyTruck> OnDestroyed;

        public float CurrentHealth { get; private set; } = MaxHealth;

        public bool IsAlive => !_dead;

        public float ModelSize => Mathf.Max(_size.x, Mathf.Max(_size.y, _size.z));

        Rigidbody _target;
        CampaignTerrain _land;
        Camera _cam;
        BoxCollider _collider;
        SmokeTrail _smoke;
        PlaneFire _fire;
        AudioSource _audio;
        AudioClip _shotClip;
        GameObject _bulletTemplate;

        readonly Transform[] _wheels = new Transform[WheelNodes.Length];
        float _wheelRadius = 1f;

        float _stopX;
        float _roadLift;
        float _z;
        float _fireCooldown;
        float _lastCollisionTime = -999f;
        bool _stopped;
        bool _standDown;
        bool _dead;
        bool _reported;

        Transform _bar;
        Transform _barFillPivot;
        Renderer _barFill;

        public bool Halted => _stopped;

        public Vector3 Centre => transform.position + Vector3.up * (_size.y * 0.5f);

        Vector3 Muzzle => transform.position
                          + new Vector3(_size.x * BedRear, _size.y * BedHeight, 0f);

        public static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            _prefab = Resources.Load<GameObject>(ModelResource);
            if (_prefab == null)
            {
                Debug.LogError($"EnemyTruck: {ModelResource} not found in Resources.");
                return false;
            }

            var holder = new GameObject("Truck Probe");
            holder.transform.position = new Vector3(0f, ProbeY, 0f);
            Instantiate(_prefab, holder.transform, false);

            Bounds box = BoundsIn(holder.transform);
            Destroy(holder);

            if (box.size.x <= Epsilon || box.size.z <= Epsilon)
            {
                Debug.LogError($"EnemyTruck: {ModelResource} measured empty"
                               + $" ({box.size}); the truck is skipped.");
                _prefab = null;
                return false;
            }

            _scale = PlaneModelConfig.UnitsPerMeter;
            _size = new Vector3(box.size.z, box.size.y, box.size.x) * _scale;
            _yawedMin = new Vector3(-box.max.z, box.min.y, box.min.x) * _scale;
            return true;
        }

        public static EnemyTruck Spawn(Vector3 groundPoint, float stopX, float roadLift,
            Rigidbody target, CampaignTerrain land)
        {
            if (!Measure()) return null;

            var root = new GameObject("Enemy Truck");
            root.transform.position = groundPoint;

            var holder = new GameObject("truck_ww1");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localRotation = RoadYaw;
            holder.transform.localScale = Vector3.one * _scale;
            holder.transform.localPosition =
                new Vector3(-_size.x * 0.5f, 0f, -_size.z * 0.5f) - _yawedMin;

            GameObject view = Instantiate(_prefab, holder.transform, false);

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.sharedMaterials = LiftAll(renderer.sharedMaterials);
            }

            foreach (Collider collider in view.GetComponentsInChildren<Collider>())
                Destroy(collider);

            SetLayer(root.transform, PlaneFactory.PlaneLayer);

            var truck = root.AddComponent<EnemyTruck>();
            truck.Initialize(stopX, roadLift, target, land, view.transform);
            return truck;
        }

        void Initialize(float stopX, float roadLift, Rigidbody target, CampaignTerrain land,
            Transform view)
        {
            _stopX = stopX;
            _roadLift = roadLift;
            _target = target;
            _land = land;
            _z = transform.position.z;
            _cam = Camera.main;

            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.center = new Vector3(0f, _size.y * 0.5f, 0f);
            _collider.size = _size;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _smoke = gameObject.AddComponent<SmokeTrail>();
            _bulletTemplate = Bullet.BuildTemplate(Bullet.RoundColor);

            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            FindWheels(view);
            BuildHealthBar();
            Settle(transform.position.x);
        }

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
            UpdateFiring(dt);
            PlaceHealthBar();
        }

        void Drive(float dt)
        {
            if (_stopped || _standDown) return;

            float x = transform.position.x - DriveSpeed * dt;
            if (x <= _stopX)
            {
                x = _stopX;
                _stopped = true;
            }

            Settle(x);
            SpinWheels(dt);
        }

        void Settle(float x)
        {
            float y = _land != null && _land.SampleHeight(x, _z, out float ground)
                ? ground
                : ProceduralTerrain.BaseLevel;

            transform.position = new Vector3(x, y + _roadLift, _z);
        }

        void SpinWheels(float dt)
        {
            if (_wheelRadius <= Epsilon) return;

            float degrees = -DriveSpeed / _wheelRadius * Mathf.Rad2Deg * dt;
            foreach (Transform wheel in _wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, degrees, Space.Self);
        }

        void UpdateFiring(float dt)
        {
            _fireCooldown -= dt;
            if (_standDown || _fireCooldown > 0f) return;

            if (_stopped) ShellFence();
            else ShootTarget();
        }

        void ShootTarget()
        {
            if (_target == null || !OnCamera(Centre)) return;

            Vector3 muzzle = Muzzle;
            Vector3 aim = Intercept(muzzle);
            if (Vector3.Distance(muzzle, _target.position) > FireRange) return;

            Vector3 dir = aim - muzzle;
            dir.z = 0f;
            if (dir.sqrMagnitude < 1f) return;

            FireRound(muzzle, dir.normalized);
        }

        void ShellFence()
        {
            Airfield field = Airfield.Current;
            if (field == null || field.Lost) return;

            Vector3 muzzle = Muzzle;
            Vector3 aim = field.AimPoint(_z);
            Vector3 dir = aim - muzzle;
            dir.z = 0f;
            if (dir.sqrMagnitude < 1f) return;

            FireRound(muzzle, dir.normalized);
            field.Shell(Airfield.MaxHealth * FireInterval / Airfield.ShellSeconds);
            Sparks.Spawn(aim, FenceSparkSize);
        }

        void FireRound(Vector3 muzzle, Vector3 dir)
        {
            _fireCooldown = FireInterval;

            Vector3 point = muzzle + dir * MuzzleClear;
            GameObject go = Instantiate(_bulletTemplate, point,
                Quaternion.FromToRotation(Vector3.up, dir));
            go.name = "TruckBullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, BulletSpeed, BulletDamage, _collider,
                fromEnemy: true);

            MuzzleFlash.Spawn(point, dir, _size.y);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Sfx);
        }

        Vector3 Intercept(Vector3 muzzle)
        {
            Vector3 point = _target.position;
            Vector3 velocity = _target.linearVelocity;

            float t = 0f;
            for (int i = 0; i < 2; i++)
            {
                float distance = Vector3.Distance(muzzle, point + velocity * (t * LeadFactor));
                t = BulletSpeed > 0f ? distance / BulletSpeed : 0f;
            }
            return point + velocity * (t * LeadFactor);
        }

        bool OnCamera(Vector3 worldPoint)
        {
            if (_cam == null) return true;

            Vector3 vp = _cam.WorldToViewportPoint(worldPoint);
            return vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
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

        void FindWheels(Transform view)
        {
            float best = 0f;
            for (int i = 0; i < WheelNodes.Length; i++)
            {
                _wheels[i] = PlaneFactory.FindDeep(view, WheelNodes[i]);
                if (_wheels[i] == null) continue;

                best = Mathf.Max(best, _wheels[i].position.y - transform.position.y);
            }

            if (best > Epsilon) _wheelRadius = best;
        }

        void BuildHealthBar()
        {
            _bar = new GameObject("TruckHealthBar").transform;

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

            float frac = Mathf.Clamp01(CurrentHealth / MaxHealth);
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

        void OnDestroy()
        {
            Report();
            if (_bar != null) Destroy(_bar.gameObject);
            if (_fire != null) _fire.Extinguish();
            if (_bulletTemplate != null) Destroy(_bulletTemplate);
        }

        static Material[] LiftAll(Material[] sources)
        {
            for (int i = 0; i < sources.Length; i++) sources[i] = LiftedMaterial(sources[i]);
            return sources;
        }

        static Material LiftedMaterial(Material source)
        {
            if (source == null) return null;
            if (Lifted.TryGetValue(source, out Material cached) && cached != null) return cached;

            var copy = new Material(source) { name = source.name + " (lifted)" };

            if (copy.HasProperty(BaseColorId))
                copy.SetColor(BaseColorId, Lift(copy.GetColor(BaseColorId)));
            else if (copy.HasProperty(ColorId))
                copy.SetColor(ColorId, Lift(copy.GetColor(ColorId)));

            Lifted[source] = copy;
            return copy;
        }

        static Color Lift(Color linear)
        {
            Color gamma = linear.gamma;
            gamma.r = Mathf.Pow(Mathf.Clamp01(gamma.r), LiftGamma);
            gamma.g = Mathf.Pow(Mathf.Clamp01(gamma.g), LiftGamma);
            gamma.b = Mathf.Pow(Mathf.Clamp01(gamma.b), LiftGamma);

            Color raised = gamma.linear;
            raised.a = linear.a;
            return raised;
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }

        static Bounds BoundsIn(Transform subtree)
        {
            bool any = false;
            var bounds = new Bounds();
            Matrix4x4 toSpace = subtree.worldToLocalMatrix;

            foreach (MeshFilter filter in subtree.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Matrix4x4 m = toSpace * filter.transform.localToWorldMatrix;
                Bounds local = mesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);

                    Vector3 point =
                        m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));

                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }

            return bounds;
        }
    }
}
