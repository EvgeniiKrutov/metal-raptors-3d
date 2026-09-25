using System;
using UnityEngine;

namespace MetalRaptors
{
    public class EnemyZeppelin : MonoBehaviour, IDamageable, ISolid
    {
        const float BackgroundScale = 1.3f;
        const float EnvelopeRadius = 0.0592f;
        const float ModelLength = 158f;
        const float ModelMidHeight = -0.27f;
        const int RepelPasses = 3;

        const float Health = 6000f;
        const float HealthFloor = 500f;
        const float SmokeBelow = 5000f;
        const float BurnBelow = 1000f;

        const float Speed = 30f;
        const float Braking = 6f;
        const float CreepSpeed = 1f;
        const float EntryMargin = 40f;

        const float BobHeight = 3f;
        const float BobPeriod = 7f;

        const float BulletDamage = 6f;
        const float BulletSpeed = 140f;
        const float FireInterval = 1f;
        const float FireRange = 500f;
        const float LeadFactor = 1f;
        const float MuzzleClear = 5f;
        const float GunClear = 2f;
        const float FlashSize = 20f;
        const float ShotVolume = 0.18f;

        const float RoundScale = 1.25f;
        const float RoundGlow = 2.5f;
        const float RimWidth = 1.7f;
        const float RimLength = 1.25f;
        const float RimDepth = 0.8f;

        const float BarWidth = 150f;
        const float BarHeight = 5f;
        const float BarLiftMargin = 25f;

        const float SmokeScale = 1.2f;
        const float FireSize = 100f;

        static readonly Vector2[] Muzzles =
        {
            new Vector2(-0.1804f, -0.0872f),
            new Vector2(0.1329f, -0.0809f),
            new Vector2(-0.0367f, 0.0707f),
        };

        static readonly float[] Phases = { 0.5f, 1f, 0.75f };

        static readonly Vector2[] Outline =
        {
            new Vector2(-79f, 0f), new Vector2(-71f, 4.95f), new Vector2(-65f, 6.7f),
            new Vector2(-57f, 7.84f), new Vector2(-51f, 8.59f), new Vector2(-43f, 9.07f),
            new Vector2(-30f, 9.35f), new Vector2(-21f, 9.32f), new Vector2(-8.5f, 9.2f),
            new Vector2(-7.9f, 10.9f), new Vector2(-3.7f, 10.1f), new Vector2(-3.4f, 9.1f),
            new Vector2(1f, 8.99f), new Vector2(7f, 8.78f), new Vector2(15f, 8.51f),
            new Vector2(21f, 8.18f), new Vector2(29f, 7.78f), new Vector2(35f, 7.3f),
            new Vector2(43f, 6.73f), new Vector2(50.3f, 6.05f), new Vector2(55.75f, 13.5f),
            new Vector2(68.25f, 13.5f), new Vector2(70.7f, 13.1f), new Vector2(76.7f, 13.1f),
            new Vector2(79f, 0f),
            new Vector2(76.7f, -13.1f), new Vector2(70.7f, -13.1f), new Vector2(68.25f, -13.5f),
            new Vector2(55.75f, -13.5f), new Vector2(50.3f, -7.05f), new Vector2(43f, -7.73f),
            new Vector2(35f, -8.3f), new Vector2(29f, -8.78f), new Vector2(28f, -9.9f),
            new Vector2(26f, -12.65f), new Vector2(19f, -13.05f), new Vector2(15f, -12.55f),
            new Vector2(13.5f, -9.9f), new Vector2(7f, -9.78f), new Vector2(1f, -9.99f),
            new Vector2(-7f, -10.15f), new Vector2(-15f, -10.26f), new Vector2(-21f, -10.32f),
            new Vector2(-22.5f, -13.2f), new Vector2(-25f, -14f), new Vector2(-32f, -14.04f),
            new Vector2(-34.8f, -13.4f), new Vector2(-36.5f, -10f), new Vector2(-43f, -10.07f),
            new Vector2(-51f, -9.59f), new Vector2(-57f, -8.84f), new Vector2(-65f, -6.7f),
            new Vector2(-71f, -4.95f),
        };

        const int VentCount = 3;
        const float VentSpanMin = -0.3f;
        const float VentSpanMax = 0.12f;
        const float VentSlotMargin = 0.15f;
        const float VentDepth = 0.88f;
        const float VentAngleMin = 40f;
        const float VentAngleMax = 65f;

        static readonly Color RoundCore = new Color(1f, 0.2f, 0.12f);
        static readonly Color RoundRim = new Color(0.05f, 0.04f, 0.04f);

        static readonly Plane[] Frustum = new Plane[6];

        public static float Length => SkyZeppelin.ApparentLength * BackgroundScale;

        public event Action Sighted;

        public float CurrentHealth { get; private set; }

        public bool Hanging { get; private set; }

        struct Gun
        {
            public Vector3 local;
            public float side;
            public float cooldown;
        }

        readonly Gun[] _guns = new Gun[3];
        readonly Vector3[] _vents = new Vector3[VentCount];
        readonly SmokeColumn[] _smoke = new SmokeColumn[VentCount];
        readonly Transform[] _fires = new Transform[VentCount];

        Rigidbody _target;
        Camera _cam;
        CapsuleCollider _collider;
        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;
        FloatingHealthBar _bar;
        Vector2[] _outline;

        Vector3 _size;
        float _hoverX;
        float _baseY;
        float _bobTime;
        bool _sighted;
        bool _standDown;
        bool _smoking;
        bool _burning;

        float Radius => _size.x * EnvelopeRadius;

        public static EnemyZeppelin Spawn(float viewRightX, float hoverX, float y, float z,
            Rigidbody target)
        {
            float x = Mathf.Max(viewRightX + Length * 0.5f + EntryMargin, hoverX);

            var root = new GameObject("Enemy Zeppelin");
            root.transform.position = new Vector3(x, y, z);

            Transform model = SkyZeppelin.BuildModel(root.transform, Length);
            if (model == null || !SkyZeppelin.Measure(model, out Bounds bounds))
            {
                Destroy(root);
                return null;
            }

            SetLayer(root.transform, PlaneFactory.PlaneLayer);

            var zeppelin = root.AddComponent<EnemyZeppelin>();
            zeppelin.Initialize(bounds.size, hoverX, target);
            return zeppelin;
        }

        void Initialize(Vector3 size, float hoverX, Rigidbody target)
        {
            _size = size;
            _hoverX = hoverX;
            _baseY = transform.position.y;
            _target = target;
            _cam = Camera.main;
            CurrentHealth = Health;

            _collider = gameObject.AddComponent<CapsuleCollider>();
            _collider.direction = 0;
            _collider.radius = Radius;
            _collider.height = _size.x;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _bulletTemplate = BuildRound();
            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            MountGuns();
            MountVents();
            BuildOutline();

            _bar = new FloatingHealthBar("ZeppelinHealthBar", BarWidth, BarHeight);
            _bar.Set(1f);
            PlaceBar();
        }

        public void StandDown() => _standDown = true;

        public ZeppelinSnapshot Capture() => new ZeppelinSnapshot
        {
            position = transform.position,
            baseY = _baseY,
            bobTime = _bobTime,
            hanging = Hanging,
            health = CurrentHealth,
        };

        public void Restore(ZeppelinSnapshot snapshot)
        {
            if (snapshot == null) return;

            Vector3 pos = snapshot.position;
            if (snapshot.hanging) pos.x = _hoverX;

            transform.position = pos;
            _baseY = snapshot.baseY;
            _bobTime = snapshot.bobTime;
            Hanging = snapshot.hanging;

            CurrentHealth = Mathf.Clamp(snapshot.health, HealthFloor, Health);
            if (_bar != null) _bar.Set(CurrentHealth / Health);
            if (CurrentHealth <= SmokeBelow) StartSmoking();
            if (CurrentHealth <= BurnBelow) StartBurning();
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;

            CurrentHealth = Mathf.Max(HealthFloor, CurrentHealth - amount);
            if (_bar != null) _bar.Set(CurrentHealth / Health);

            if (!_smoking && CurrentHealth <= SmokeBelow) StartSmoking();
            if (!_burning && CurrentHealth <= BurnBelow) StartBurning();
        }

        public bool Repel(ref Vector3 position, ref Vector3 velocity, float radius)
        {
            if (this == null || _outline == null) return false;

            Vector3 origin = transform.position;
            bool moved = false;

            for (int pass = 0; pass < RepelPasses; pass++)
            {
                var p = new Vector2(position.x - origin.x, position.y - origin.y);
                if (Mathf.Abs(p.x) > _size.x * 0.5f + radius
                    || Mathf.Abs(p.y) > _size.y * 0.5f + radius) break;

                Vector2 closest = ClosestOnOutline(p);
                Vector2 away = p - closest;
                float distance = away.magnitude;
                bool inside = InsideOutline(p);

                if (!inside && distance >= radius) break;
                if (distance < 0.0001f) break;

                Vector2 normal = (inside ? -away : away) / distance;
                Vector2 surface = closest + normal * radius;
                position.x = origin.x + surface.x;
                position.y = origin.y + surface.y;

                float into = velocity.x * normal.x + velocity.y * normal.y;
                if (into < 0f)
                {
                    velocity.x -= normal.x * into;
                    velocity.y -= normal.y * into;
                }

                moved = true;
            }

            return moved;
        }

        void BuildOutline()
        {
            float scale = _size.x / ModelLength;
            _outline = new Vector2[Outline.Length];
            for (int i = 0; i < Outline.Length; i++)
                _outline[i] = new Vector2(Outline[i].x * scale,
                    (Outline[i].y - ModelMidHeight) * scale);
        }

        Vector2 ClosestOnOutline(Vector2 p)
        {
            Vector2 best = _outline[0];
            float bestSq = float.PositiveInfinity;

            for (int i = 0; i < _outline.Length; i++)
            {
                Vector2 a = _outline[i];
                Vector2 ab = _outline[(i + 1) % _outline.Length] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                Vector2 q = a + ab * t;

                float sq = (p - q).sqrMagnitude;
                if (sq >= bestSq) continue;

                bestSq = sq;
                best = q;
            }

            return best;
        }

        bool InsideOutline(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = _outline.Length - 1; i < _outline.Length; j = i++)
            {
                Vector2 a = _outline[i];
                Vector2 b = _outline[j];
                if ((a.y > p.y) != (b.y > p.y)
                    && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Drift(dt);
            if (!_standDown) Aim(dt);
            PlaceBar();
            Watch();
        }

        void Drift(float dt)
        {
            Vector3 pos = transform.position;

            if (!Hanging)
            {
                float gap = pos.x - _hoverX;
                float speed = Mathf.Max(CreepSpeed,
                    Mathf.Min(Speed, Mathf.Sqrt(2f * Braking * Mathf.Max(0f, gap))));

                pos.x = Mathf.Max(_hoverX, pos.x - speed * dt);
                Hanging = pos.x <= _hoverX;
            }
            else
            {
                _bobTime += dt;
                pos.y = _baseY + BobHeight * Mathf.Sin(_bobTime * 2f * Mathf.PI / BobPeriod);
            }

            transform.position = pos;
        }

        void Watch()
        {
            if (_sighted || _cam == null) return;

            GeometryUtility.CalculateFrustumPlanes(_cam, Frustum);
            if (!GeometryUtility.TestPlanesAABB(Frustum, new Bounds(transform.position, _size)))
                return;

            _sighted = true;
            Sighted?.Invoke();
        }

        void Aim(float dt)
        {
            for (int i = 0; i < _guns.Length; i++)
            {
                _guns[i].cooldown -= dt;
                if (_guns[i].cooldown > 0f) continue;

                _guns[i].cooldown += FireInterval;

                Vector3 muzzle = transform.position + _guns[i].local;
                if (InReach(muzzle) && Covers(_guns[i]) && Lead(muzzle, out Vector3 dir))
                    Fire(muzzle, dir);
            }
        }

        bool InReach(Vector3 muzzle) =>
            _target != null && Gunnery.OnCamera(_cam, muzzle)
            && Vector3.Distance(muzzle, _target.position) <= FireRange;

        bool Covers(Gun gun) => (_target.position.y - transform.position.y) * gun.side >= 0f;

        bool Lead(Vector3 muzzle, out Vector3 dir)
        {
            dir = Gunnery.Intercept(muzzle, _target, BulletSpeed, LeadFactor) - muzzle;
            dir.z = 0f;
            if (dir.sqrMagnitude < 1f) return false;

            dir.Normalize();
            return true;
        }

        void Fire(Vector3 muzzle, Vector3 dir)
        {
            Vector3 start = muzzle + dir * MuzzleClear;
            GameObject go = Instantiate(_bulletTemplate, start,
                Quaternion.FromToRotation(Vector3.up, dir));
            go.name = "ZeppelinRound";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, BulletSpeed, BulletDamage, _collider,
                fromEnemy: true);

            MuzzleFlash.Spawn(start, dir, FlashSize);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Sfx);
        }

        static GameObject BuildRound()
        {
            GameObject round = Bullet.BuildTemplate(RoundCore);
            round.transform.localScale *= RoundScale;

            var core = round.GetComponent<Renderer>();
            if (core != null && core.sharedMaterial != null)
                core.sharedMaterial.SetColor("_EmissionColor", RoundCore * RoundGlow);

            var rim = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder, Vector3.zero,
                Vector3.one, RoundRim, emissive: false, keepCollider: false);
            rim.name = "Rim";
            rim.transform.SetParent(round.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0f, RimDepth);
            rim.transform.localScale = new Vector3(RimWidth, RimLength, RimWidth);

            var rimRenderer = rim.GetComponent<Renderer>();
            if (rimRenderer != null && rimRenderer.sharedMaterial != null)
                rimRenderer.sharedMaterial.SetFloat("_Smoothness", 0f);

            return round;
        }

        void MountGuns()
        {
            for (int i = 0; i < _guns.Length; i++)
            {
                float y = Muzzles[i].y * _size.x;
                _guns[i].side = Mathf.Sign(y);
                _guns[i].local = new Vector3(Muzzles[i].x * _size.x, y + _guns[i].side * GunClear,
                    0f);
                _guns[i].cooldown = FireInterval * Phases[i];
            }
        }

        void MountVents()
        {
            float slot = (VentSpanMax - VentSpanMin) / VentCount;

            for (int i = 0; i < VentCount; i++)
            {
                float start = VentSpanMin + slot * i;
                float x = UnityEngine.Random.Range(start + slot * VentSlotMargin,
                    start + slot * (1f - VentSlotMargin));
                float angle = UnityEngine.Random.Range(VentAngleMin, VentAngleMax)
                              * Mathf.Deg2Rad;

                _vents[i] = new Vector3(_size.x * x, Radius * VentDepth * Mathf.Sin(angle),
                    -Radius * VentDepth * Mathf.Cos(angle));

                var fire = new GameObject("Fire Vent").transform;
                fire.SetParent(transform, false);
                fire.localPosition = _vents[i];
                _fires[i] = fire;
            }
        }

        void StartSmoking()
        {
            _smoking = true;
            for (int i = 0; i < VentCount; i++)
                if (_smoke[i] == null)
                    _smoke[i] = SmokeColumn.Follow(transform, _vents[i],
                        UnityEngine.Random.Range(0, int.MaxValue), SmokeScale);
        }

        void StartBurning()
        {
            StartSmoking();
            _burning = true;

            foreach (SmokeColumn smoke in _smoke)
                if (smoke != null) smoke.Thicken();

            foreach (Transform fire in _fires)
                if (fire != null) PlaneFire.Ignite(fire.gameObject, FireSize);
        }

        void PlaceBar()
        {
            if (_bar != null)
                _bar.Place(transform.position + Vector3.up * (Radius + BarLiftMargin));
        }

        void OnDestroy()
        {
            if (_bar != null) _bar.Destroy();
            if (_bulletTemplate != null) Destroy(_bulletTemplate);
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }
    }
}
