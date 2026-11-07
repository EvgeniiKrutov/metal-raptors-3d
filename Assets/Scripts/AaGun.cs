using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class AaGun : MonoBehaviour
    {
        const string ModelResource = "objects/machines/flak_18";
        const float M = PlaneModelConfig.UnitsPerMeter;
        const float ModelScale = M * PlaneModelConfig.LevelScale;

        const float BaseWidth = 7f, BaseHigh = 1f, BaseDeep = 6f;
        const float CarriageWidth = 2.6f, CarriageHigh = 1.8f, CarriageDeep = 2.4f;
        const float PivotHigh = 2.6f;
        const float BarrelLength = 5.2f, BarrelGirth = 0.45f;
        const float LampOffsetX = -1.7f, LampHigh = 3.7f, LampOffsetZ = -1.3f;
        const float LampRadius = 0.85f, LampLength = 1.1f;
        const float LampHitSize = 6.8f;
        const float Sink = 0.3f;
        const float ModelSink = 0.05f;
        const float FootShare = 0.8f;
        const float LensLength = 0.3f, LensGrow = 1.1f;
        const float DroopDeg = 18f;

        const float BeamReach = 1150f;
        const float BeamInner = 7f, BeamOuter = 16f;
        const float BeamIntensity = 0.55f;
        const float BeamGlow = 1.6f;
        const float LightRangeShare = 0.85f;
        const float LightIntensity = 2.4f;
        const float LightSource = 6f;
        const float ShadowClearance = 2f;
        const int LightPriority = 60;
        const float BlindGlowRange = 70f;
        const float BlindGlowIntensity = 0.8f;

        const float SeeHalfDeg = 5.5f;
        const float SweepMidDeg = 90f, SweepAmpDeg = 52f, SweepPeriod = 9f;
        const float MinElevation = 12f, MaxElevation = 168f;
        const float SearchRateDeg = 45f;
        const float TrackShare = 0.85f;
        const float DayTrackShare = 1.5f;
        const float TrackNear = 120f;

        const float LampHealth = 30f;
        const float RelightSec = 8f;

        const float FlakGapMin = 0.75f, FlakGapMax = 1.1f;
        const float FlakWarnSec = 0.55f;
        const float FlakReachMin = 0.3f, FlakReachMax = 1.1f;
        const float FlakRadius = 0.6f;
        const float FlakDamage = 7f;
        const float FlakDepthFront = 10f, FlakDepthBack = 25f;
        const float FlashSize = 30f;
        const float SmokeSize = 22f;

        const float BlowSize = 75f;
        const float BlowFire = 26f;
        const float BlastHitShare = 0.8f;
        const float HurtSmokeScale = 0.5f;

        const float ProbeTop = 1200f, ProbeDepth = 1600f;

        const float ClearStep = 30f;
        const int ClearTries = 40;
        const float ClearMargin = 60f;

        static readonly Color GunColor = new Color(0.27f, 0.29f, 0.24f);
        static readonly Color BaseColor = new Color(0.42f, 0.38f, 0.3f);
        static readonly Color DrumColor = new Color(0.17f, 0.17f, 0.16f);
        static readonly Color FaceLit = new Color(1f, 0.92f, 0.75f);
        static readonly Color FaceBlind = new Color(0.45f, 0.12f, 0.04f);
        static readonly Color BeamColor = new Color(1f, 0.9f, 0.72f);
        static readonly Color BlindColor = new Color(1f, 0.42f, 0.18f);
        static readonly Color BlowColor = new Color(1f, 0.62f, 0.3f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        Transform _target;
        Rigidbody _targetBody;
        float _cruise;
        float _planeLength;
        float _x, _z;
        int _seed;
        bool _lamped;

        Transform _view;
        Transform _lamp;
        Transform _drum;
        Transform _barrel;
        float _restDeg = 90f;
        Vector3 _muzzleLocal;
        Vector3 _lampLocal;
        bool _lampRides;
        float _lampRadius = LampRadius * M;
        float _sink = Sink;
        Material _face;
        Searchlight _beam;
        NightLight2D _light;
        NightLight2D _glow;
        readonly List<Collider> _hits = new List<Collider>();
        readonly List<Collider> _shots = new List<Collider>();
        Vector3 _centre;
        float _halfWidth;

        bool _grounded;
        bool _down;
        bool _hurt;
        bool _blind;
        bool _tracking;
        float _angle = SweepMidDeg;
        float _clock;
        float _phase;
        float _lampHealth = LampHealth;
        float _relightIn;
        float _flakIn;

        public bool Down => _down;

        public bool Hurt => _hurt;

        public bool Grounded => _grounded;

        public bool Lit => _lamped && _grounded && !_down && !_blind;

        public bool Tracking => _tracking;

        public float X => _x;

        public static float BeamSpan =>
            BeamReach * Mathf.Abs(Mathf.Cos((SweepMidDeg + SweepAmpDeg) * Mathf.Deg2Rad));

        public Vector3 LampPoint => _lamp != null ? _lamp.position : transform.position;

        public static AaGun Place(float x, float z, Transform target, float cruise, float planeLength,
            int seed, bool lamp = true)
        {
            var go = new GameObject("AA Gun");
            go.layer = BattlefieldProps.Layer;
            go.transform.position = new Vector3(x, SeaSurface.Level, z);

            var gun = go.AddComponent<AaGun>();
            gun._target = target;
            gun._targetBody = target != null ? target.GetComponent<Rigidbody>() : null;
            gun._cruise = Mathf.Max(1f, cruise);
            gun._planeLength = Mathf.Max(1f, planeLength);
            gun._x = x;
            gun._z = z;
            gun._seed = seed;
            gun._lamped = lamp;
            gun._phase = (float)new System.Random(seed).NextDouble() * Mathf.PI * 2f;
            gun.Build();
            return gun;
        }

        public static float ClearSpot(CampaignTerrain terrain, float x, float z)
        {
            if (terrain == null || !terrain.Occupied(x, z, ClearMargin)) return x;

            for (int i = 1; i <= ClearTries; i++)
            {
                float right = x + i * ClearStep;
                if (!terrain.Occupied(right, z, ClearMargin)) return right;

                float left = x - i * ClearStep;
                if (!terrain.Occupied(left, z, ClearMargin)) return left;
            }
            return x;
        }

        void Build()
        {
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab != null) BuildModel(prefab);
            else BuildPlaceholder();

            NightReceivers.Adopt(_view.gameObject);
            NightLights.MarkCaster(_view.gameObject);

            Physics.IgnoreLayerCollision(BattlefieldProps.Layer, 0, true);
            Bounds box = LocalBounds();
            _halfWidth = box.extents.x;
            _centre = box.center;
            if (_hits.Count == 0)
            {
                var body = gameObject.AddComponent<BoxCollider>();
                body.isTrigger = true;
                body.center = box.center;
                body.size = box.size;
                _hits.Add(body);
            }

            if (_lamped) BuildLamp();
            _view.gameObject.SetActive(false);
            SetLights(false);
        }

        void BuildModel(GameObject prefab)
        {
            _view = new GameObject("Flak 18").transform;
            _view.SetParent(transform, false);
            _view.localScale = Vector3.one * ModelScale;
            var view = Instantiate(prefab, _view, false);
            view.name = prefab.name;
            _sink = ModelSink;

            foreach (Renderer renderer in _view.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.On;
            foreach (MeshFilter mesh in _view.GetComponentsInChildren<MeshFilter>(true))
                AddHit(mesh);

            _lamp = new GameObject("Lamp").transform;
            _lamp.SetParent(transform, false);

            Transform elevation = Node(_view, "Elevation");
            Renderer barrel = Body(_view, "barrel");
            if (elevation == null || barrel == null)
            {
                Bounds box = LocalBounds();
                _lamp.localPosition = new Vector3(box.center.x - box.extents.x * 0.5f, box.max.y + 0.3f * M,
                    box.min.z);
                _muzzleLocal = new Vector3(box.center.x, box.max.y, box.center.z);
                return;
            }

            Vector3 along = barrel.bounds.center - elevation.position;
            _view.rotation = Quaternion.Euler(0f, Mathf.Atan2(along.z, along.x) * Mathf.Rad2Deg, 0f)
                             * _view.rotation;

            _barrel = new GameObject("Elevation Pivot").transform;
            _barrel.SetParent(elevation.parent, false);
            _barrel.SetPositionAndRotation(elevation.position, transform.rotation);
            elevation.SetParent(_barrel, true);
            _restDeg = 0f;

            Bounds gun = barrel.bounds;
            _muzzleLocal = _barrel.InverseTransformPoint(new Vector3(gun.max.x, gun.center.y, gun.center.z));

            Renderer tube = Body(_view, "searchlight");
            Bounds light = tube != null ? tube.bounds : gun;
            var front = new Vector3(light.max.x, light.center.y, light.center.z);
            _lampLocal = _barrel.InverseTransformPoint(front);
            _lampRides = true;
            _lampRadius = light.extents.y;
            _lamp.position = front;

            if (_lamped) BuildLens(light.size.y);
        }

        void AddHit(MeshFilter mesh)
        {
            if (mesh.sharedMesh == null) return;

            var hit = new GameObject("Hit");
            hit.layer = BattlefieldProps.Layer;
            hit.transform.SetParent(mesh.transform, false);

            var box = hit.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = mesh.sharedMesh.bounds.center;
            box.size = mesh.sharedMesh.bounds.size;
            _hits.Add(box);

            if (!_lamped) return;

            var shot = new GameObject("Lamp Hitbox");
            shot.layer = PlaneFactory.PlaneLayer;
            shot.transform.SetParent(mesh.transform, false);

            var target = shot.AddComponent<BoxCollider>();
            target.center = box.center;
            target.size = box.size;
            shot.AddComponent<AaLamp>().Gun = this;
            _shots.Add(target);
        }

        void BuildLens(float girth)
        {
            float unit = 1f / Mathf.Abs(_barrel.lossyScale.x);
            float length = LensLength * ModelScale;

            GameObject lens = Part(PrimitiveType.Cylinder, "Lens", Vector3.zero, Vector3.one, FaceLit, _barrel,
                emissive: true);
            lens.transform.localPosition = _lampLocal - Vector3.right * (length * 0.5f * unit);
            lens.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            lens.transform.localScale = new Vector3(girth * LensGrow, length * 0.5f, girth * LensGrow) * unit;
            _face = lens.GetComponent<Renderer>().sharedMaterial;
        }

        void BuildPlaceholder()
        {
            _view = new GameObject("AA Gun (placeholder)").transform;
            _view.SetParent(transform, false);

            Part(PrimitiveType.Cube, "Base", new Vector3(0f, BaseHigh * 0.5f, 0f),
                new Vector3(BaseWidth, BaseHigh, BaseDeep), BaseColor, _view);
            Part(PrimitiveType.Cube, "Carriage", new Vector3(0f, BaseHigh + CarriageHigh * 0.5f, 0f),
                new Vector3(CarriageWidth, CarriageHigh, CarriageDeep), GunColor, _view);

            _barrel = new GameObject("Barrel Pivot").transform;
            _barrel.SetParent(_view, false);
            _barrel.localPosition = new Vector3(0f, PivotHigh, 0f) * M;
            Part(PrimitiveType.Cylinder, "Barrel", new Vector3(0f, BarrelLength * 0.5f, 0f),
                new Vector3(BarrelGirth, BarrelLength * 0.5f, BarrelGirth), GunColor, _barrel);

            _lamp = new GameObject("Lamp").transform;
            _lamp.SetParent(transform, false);
            _lamp.localPosition = new Vector3(LampOffsetX, LampHigh, LampOffsetZ) * M;

            Part(PrimitiveType.Cube, "Lamp Post",
                new Vector3(LampOffsetX, (BaseHigh + LampHigh) * 0.5f, LampOffsetZ),
                new Vector3(0.3f, LampHigh - BaseHigh, 0.3f), GunColor, _view);

            _drum = new GameObject("Lamp Drum").transform;
            _drum.SetParent(_view, false);
            _drum.localPosition = _lamp.localPosition;
            Part(PrimitiveType.Cylinder, "Drum", Vector3.zero,
                new Vector3(LampRadius * 2f, LampLength * 0.5f, LampRadius * 2f), DrumColor, _drum);
            GameObject face = Part(PrimitiveType.Cylinder, "Lens",
                new Vector3(0f, LampLength * 0.5f, 0f),
                new Vector3(LampRadius * 1.8f, 0.04f, LampRadius * 1.8f), FaceLit, _drum, emissive: true);
            _face = face.GetComponent<Renderer>().sharedMaterial;

            _muzzleLocal = Vector3.up * (BarrelLength * M);
        }

        static GameObject Part(PrimitiveType type, string name, Vector3 localMeters, Vector3 sizeMeters,
            Color color, Transform parent, bool emissive = false)
        {
            GameObject go = UIFactory.CreatePrimitive3D(type, Vector3.zero, sizeMeters * M, color,
                emissive: emissive, keepCollider: false);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localMeters * M;
            go.transform.localScale = sizeMeters * M;

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        static Transform Node(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            return null;
        }

        static Renderer Body(Transform root, string name)
        {
            Transform node = Node(root, name);
            return node != null ? node.GetComponent<Renderer>() : null;
        }

        Bounds LocalBounds()
        {
            var renderers = _view.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            var box = new Bounds(Vector3.zero, Vector3.one * M);

            foreach (Renderer renderer in renderers)
            {
                if (_barrel != null && renderer.transform.IsChildOf(_barrel)) continue;

                Bounds world = renderer.bounds;
                Vector3 min = transform.InverseTransformPoint(world.min);
                Vector3 max = transform.InverseTransformPoint(world.max);
                var local = new Bounds((min + max) * 0.5f, Vector3.zero);
                local.Encapsulate(min);
                local.Encapsulate(max);

                if (any) box.Encapsulate(local);
                else box = local;
                any = true;
            }
            return box;
        }

        void BuildLamp()
        {
            if (_shots.Count == 0)
            {
                var hit = new GameObject("Lamp Hitbox");
                hit.layer = PlaneFactory.PlaneLayer;
                hit.transform.SetParent(_lamp, false);
                var box = hit.AddComponent<BoxCollider>();
                box.size = Vector3.one * (LampHitSize * M);
                hit.AddComponent<AaLamp>().Gun = this;
                _shots.Add(box);
            }

            _beam = Searchlight.Mount(_lamp, Vector3.zero);
            if (_beam != null)
            {
                _beam.innerAngle = BeamInner;
                _beam.outerAngle = BeamOuter;
                _beam.length = BeamReach;
                _beam.sourceRadius = _lampRadius;
                _beam.glowSize = LampRadius * M * 1.4f;
                _beam.intensity = BeamIntensity;
                _beam.glowIntensity = BeamGlow;
            }

            var cone = new GameObject("Beam Light");
            cone.transform.SetParent(_lamp, false);
            _light = cone.AddComponent<NightLight2D>();
            _light.type = NightLightType.Cone;
            _light.color = BeamColor;
            _light.intensity = LightIntensity;
            _light.range = BeamReach * LightRangeShare;
            _light.innerAngle = BeamInner;
            _light.outerAngle = BeamOuter;
            _light.sourceRadius = LightSource;
            _light.localDirection = Vector3.right;
            _light.priority = LightPriority;
            _light.castShadows = true;
            _light.shadowNear = (BarrelLength + ShadowClearance) * M;
            _light.beam = _beam;

            var glow = new GameObject("Blind Glow");
            glow.transform.SetParent(_lamp, false);
            _glow = glow.AddComponent<NightLight2D>();
            _glow.type = NightLightType.Point;
            _glow.color = BlindColor;
            _glow.intensity = BlindGlowIntensity;
            _glow.range = BlindGlowRange;
            _glow.priority = LightPriority;
        }

        void SetLights(bool on)
        {
            bool lit = on && !_blind && !_down;
            if (_beam != null) _beam.enabled = on;
            if (_light != null) _light.enabled = lit;
            if (_glow != null) _glow.enabled = on && _blind && !_down;
            if (_face != null)
            {
                Color face = _down || !_lamped ? Color.black : _blind ? FaceBlind : FaceLit;
                _face.SetColor(BaseColorId, face);
                _face.SetColor(EmissionColorId, face * (_down ? 0f : 2f));
            }
        }

        public void Track(bool on)
        {
            bool next = on && Lit;
            if (next && !_tracking) _flakIn = Random.Range(FlakGapMin, FlakGapMax) * 0.5f;
            _tracking = next;
        }

        public bool Sees(Vector3 point, float radius)
        {
            if (!Lit) return false;

            Vector2 to = (Vector2)point - (Vector2)LampPoint;
            float distance = to.magnitude;
            if (distance < 1f || distance > BeamReach) return false;

            float off = Mathf.Abs(Mathf.DeltaAngle(_angle, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg));
            return off <= SeeHalfDeg + Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
        }

        public bool InBlast(Vector3 point, float radius)
        {
            if (!_grounded || _down) return false;

            float reach = radius * BlastHitShare;
            foreach (Collider hit in _hits)
                if ((hit.ClosestPoint(point) - point).sqrMagnitude <= reach * reach) return true;
            return false;
        }

        public void HitLamp(float amount)
        {
            if (!Lit || amount <= 0f) return;

            _lampHealth -= amount;
            if (_lampHealth > 0f) return;

            _blind = true;
            _tracking = false;
            _relightIn = RelightSec;
            if (_beam != null) _beam.Fail();
            Sparks.Spawn(LampPoint, LampRadius * M * 2f);
            SetLights(true);
        }

        public bool Shoot()
        {
            if (!_grounded || _down) return false;

            Vector3 muzzle = MuzzlePoint;
            MuzzleFlash.Spawn(muzzle, Heading, FlashSize);
            MuzzleSmoke.Spawn(muzzle, Heading, SmokeSize);
            return true;
        }

        public void Damage()
        {
            if (_hurt || _down) return;

            _hurt = true;
            SmokeColumn.Follow(transform, _centre, _seed, HurtSmokeScale);
        }

        public void Blow()
        {
            if (_down) return;

            _down = true;
            _tracking = false;
            if (_beam != null) _beam.Fail();
            SetLights(true);
            foreach (Collider shot in _shots) shot.enabled = false;
            foreach (Collider hit in _hits) hit.enabled = false;

            Vector3 centre = transform.TransformPoint(_centre);
            Vector3 foot = transform.position;
            Camera cam = Camera.main;

            Explosion.Spawn(centre, BlowSize);
            GroundBlast.Spawn(foot, BlowSize, cam != null ? cam.transform.position : foot);
            WreckFire.Begin(transform, foot + Vector3.up * M, BlowFire, _seed);
            NightLights.Flash(centre, BlowColor, 3f, BlowSize * 5f, 0.7f, 5);

            Pose(DroopDeg);
            if (_drum != null) _drum.localRotation = Quaternion.Euler(0f, 0f, 115f);
        }

        void Update()
        {
            if (!_grounded)
            {
                if (!Ground()) return;
                _view.gameObject.SetActive(true);
                SetLights(true);
            }

            if (_down) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _clock += dt;

            if (_blind)
            {
                _relightIn -= dt;
                if (_relightIn <= 0f) Relight();
            }

            bool firing = (_tracking || !_lamped) && _target != null;
            bool tracking = firing || (_hurt && Lit && _target != null);
            float want = tracking ? AngleTo(_target.position) : SweepAngle();
            float rate = tracking ? TrackRate() : SearchRateDeg;
            _angle = Mathf.Clamp(Mathf.MoveTowardsAngle(_angle, want, rate * dt),
                MinElevation, MaxElevation);
            Aim();

            if (firing && Sees(_target.position, 0f)) Fire(dt);
        }

        bool Ground()
        {
            if (!Floor(_x, out float y)) return false;

            float reach = _halfWidth * FootShare;
            if (Floor(_x - reach, out float left)) y = Mathf.Min(y, left);
            if (Floor(_x + reach, out float right)) y = Mathf.Min(y, right);

            transform.position = new Vector3(_x, y - _sink * M, _z);
            _grounded = true;
            return true;
        }

        bool Floor(float x, out float y)
        {
            bool hit = Physics.Raycast(new Vector3(x, ProbeTop, _z), Vector3.down, out RaycastHit ground,
                ProbeDepth, 1 << ProceduralTerrain.GroundLayer, QueryTriggerInteraction.Ignore);
            y = hit ? ground.point.y : 0f;
            return hit;
        }

        void Relight()
        {
            _blind = false;
            _lampHealth = LampHealth;
            if (_beam != null) _beam.Relight();
            SetLights(true);
        }

        float SweepAngle() =>
            SweepMidDeg + SweepAmpDeg * Mathf.Sin(_clock * Mathf.PI * 2f / SweepPeriod + _phase);

        float AngleTo(Vector3 point)
        {
            Vector2 to = (Vector2)point - (Vector2)LampPoint;
            return Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        }

        float TrackRate()
        {
            float distance = Mathf.Max(TrackNear, ((Vector2)_target.position - (Vector2)LampPoint).magnitude);
            return (_lamped ? TrackShare : DayTrackShare) * _cruise / distance * Mathf.Rad2Deg;
        }

        Vector3 Heading
        {
            get
            {
                float radians = _angle * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
            }
        }

        void Aim()
        {
            if (_beam != null) _beam.worldDirection = Heading;
            if (_light != null) _light.transform.rotation = Quaternion.Euler(0f, 0f, _angle);
            if (_drum != null) _drum.localRotation = Quaternion.Euler(0f, 0f, _angle - 90f);
            Pose(_angle);
        }

        void Pose(float angle)
        {
            if (_barrel == null) return;

            _barrel.rotation = transform.rotation * Quaternion.Euler(0f, 0f, angle - _restDeg);
            if (_lampRides) _lamp.position = _barrel.TransformPoint(_lampLocal);
        }

        Vector3 MuzzlePoint => _barrel != null
            ? _barrel.TransformPoint(_muzzleLocal)
            : transform.TransformPoint(_muzzleLocal);

        void Fire(float dt)
        {
            _flakIn -= dt;
            if (_flakIn > 0f) return;
            _flakIn = Random.Range(FlakGapMin, FlakGapMax);

            Vector3 muzzle = MuzzlePoint;
            Vector3 plane = _target.position;
            Vector3 aim = (plane - muzzle).normalized;
            MuzzleFlash.Spawn(muzzle, aim, FlashSize);
            MuzzleSmoke.Spawn(muzzle, aim, SmokeSize);
            NightLights.Flash(muzzle, BlowColor, 1.6f, FlashSize * 4f, 0.12f);

            Vector3 velocity = _targetBody != null ? _targetBody.linearVelocity : Vector3.zero;
            Vector3 ahead = plane + velocity * FlakWarnSec;
            Vector2 offset = Random.insideUnitCircle.normalized
                             * (Random.Range(FlakReachMin, FlakReachMax) * _planeLength);
            var at = new Vector3(ahead.x + offset.x, ahead.y + offset.y,
                plane.z + Random.Range(-FlakDepthFront, FlakDepthBack));

            SkyBlast.Arm(at, _planeLength * FlakRadius, FlakWarnSec, _target, FlakDamage);
        }
    }
}
