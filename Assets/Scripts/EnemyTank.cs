using UnityEngine;

namespace MetalRaptors
{
    public class EnemyTank : GroundVehicle
    {
        public const string ModelResource = "objects/machines/tank_ww1";

        const float Health = 300f;
        const float Speed = 18f;

        const float RoofDamage = 12f;
        const float RoofBulletSpeed = 140f;
        const float RoofReload = 2.5f;
        const float RoofRange = 500f;
        const float RoofLead = 1f;
        const float TraverseDegPerSec = 60f;
        const float FireConeDeg = 12f;
        const float MinElevationDeg = 8f;
        const float MinRise = 0.001f;
        const float ShellScale = 1.6f;

        const float SponsonDamage = 6f;
        const float SponsonBulletSpeed = 200f;
        const float SponsonInterval = 0.5f;
        const float ShellSeconds = 22f;
        const float FenceSparkSize = 20f;

        const string CannonNode = "cannon";
        const string AxleNode = "cannonAxle";

        static readonly Color ShellColor = new Color(1.00f, 0.45f, 0.18f);

        static readonly string[] SponsonNodes = { "gunMuzzle", "gunMuzzle.001" };

        static readonly VehicleModel Model = new VehicleModel(ModelResource);

        public static bool Measure() => Model.Measure();

        public static float BodyLength => Model.Length;

        public override float MaxHealth => Health;

        protected override float DriveSpeed => Speed;

        Transform _pivot;
        Transform _axle;
        Quaternion _pivotRest;
        float _restAngle;
        float _barrelLength;
        float _traverse;

        readonly Transform[] _sponsons = new Transform[SponsonNodes.Length];
        int _nextSponson;

        float _roofCooldown;
        float _sponsonCooldown;
        GameObject _shellTemplate;
        GameObject _sponsonTemplate;

        public static EnemyTank Spawn(Vector3 groundPoint, float fenceStopX, float roadLift,
            Rigidbody target, CampaignTerrain land)
        {
            if (!Model.Measure()) return null;

            var root = new GameObject("Enemy Tank");
            root.transform.position = groundPoint;

            Transform view = Model.Build(root.transform);
            TankSkin.Apply(view);
            SetLayer(root.transform, PlaneFactory.PlaneLayer);

            var tank = root.AddComponent<EnemyTank>();
            tank.Initialize(fenceStopX, roadLift, target, land, view);
            return tank;
        }

        void Initialize(float fenceStopX, float roadLift, Rigidbody target, CampaignTerrain land,
            Transform view)
        {
            Begin(Model.Size, fenceStopX, roadLift, target, land);

            _shellTemplate = Bullet.BuildTemplate(ShellColor);
            _shellTemplate.transform.localScale *= ShellScale;
            _sponsonTemplate = Bullet.BuildTemplate(Bullet.RoundColor);

            MountRoofGun(view);
            FindSponsons(view);
        }

        void MountRoofGun(Transform view)
        {
            if (view == null) return;

            Transform cannon = PlaneFactory.FindDeep(view, CannonNode);
            _axle = PlaneFactory.FindDeep(view, AxleNode);

            if (cannon == null || _axle == null)
            {
                Debug.LogWarning($"EnemyTank: {ModelResource} has no {CannonNode}/{AxleNode}; "
                                 + "the roof gun cannot traverse.");
                return;
            }

            var pivot = new GameObject("Roof Gun Pivot").transform;
            pivot.SetParent(cannon.parent, false);
            pivot.SetPositionAndRotation(_axle.position, cannon.parent.rotation);
            cannon.SetParent(pivot, true);

            _pivot = pivot;
            _pivotRest = pivot.rotation;

            Vector3 tip = FarthestPoint(cannon, _axle.position);
            Vector3 rest = tip - _axle.position;
            _barrelLength = new Vector2(rest.x, rest.y).magnitude;
            _restAngle = Mathf.Atan2(rest.y, rest.x) * Mathf.Rad2Deg;
        }

        void FindSponsons(Transform view)
        {
            if (view == null) return;

            for (int i = 0; i < SponsonNodes.Length; i++)
            {
                _sponsons[i] = PlaneFactory.FindDeep(view, SponsonNodes[i]);
                if (_sponsons[i] == null)
                    Debug.LogWarning($"EnemyTank: {ModelResource} has no {SponsonNodes[i]}; "
                                     + "that side gun cannot fire.");
            }
        }

        protected override void Aim(float dt)
        {
            TrackTarget(dt);
            ShellFence(dt);
        }

        void TrackTarget(float dt)
        {
            _roofCooldown -= dt;
            if (_pivot == null || _axle == null || Target == null) return;

            Vector3 axle = _axle.position;
            Vector3 muzzle = axle + Barrel(_restAngle + _traverse) * _barrelLength;
            Vector3 to = Intercept(muzzle, RoofBulletSpeed, RoofLead) - axle;

            float want = Mathf.Atan2(Mathf.Max(to.y, MinRise), to.x) * Mathf.Rad2Deg;
            want = Mathf.Clamp(want, MinElevationDeg, 180f - MinElevationDeg);

            float wanted = Mathf.DeltaAngle(_restAngle, want);
            _traverse = Mathf.MoveTowards(_traverse, wanted, TraverseDegPerSec * dt);
            _pivot.rotation = Quaternion.AngleAxis(_traverse, Vector3.forward) * _pivotRest;

            if (_roofCooldown > 0f) return;
            if (!OnCamera(Centre)) return;
            if (Vector3.Distance(muzzle, Target.position) > RoofRange) return;

            float laid = _restAngle + _traverse;
            if (Mathf.Abs(Mathf.DeltaAngle(laid, want)) > FireConeDeg) return;

            _roofCooldown = RoofReload;
            Vector3 dir = Barrel(laid);
            FireRound(_shellTemplate, axle + dir * _barrelLength, dir, RoofBulletSpeed, RoofDamage,
                Size.y);
        }

        void ShellFence(float dt)
        {
            _sponsonCooldown -= dt;
            if (!Halted || _sponsonCooldown > 0f) return;

            Airfield field = Airfield.Current;
            if (field == null || field.Lost) return;

            Transform gun = _sponsons[_nextSponson];
            _nextSponson = (_nextSponson + 1) % _sponsons.Length;
            if (gun == null) return;

            Vector3 muzzle = gun.position;
            Vector3 aim = field.AimPoint(PlaneZ);
            Vector3 dir = aim - muzzle;
            dir.z = 0f;
            if (dir.sqrMagnitude < 1f) return;

            _sponsonCooldown = SponsonInterval;
            FireRound(_sponsonTemplate, muzzle, dir.normalized, SponsonBulletSpeed, SponsonDamage,
                Size.y * 0.5f);

            field.Shell(Airfield.MaxHealth * SponsonInterval / ShellSeconds);
            Sparks.Spawn(aim, FenceSparkSize);
        }

        static Vector3 Barrel(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        }

        static Vector3 FarthestPoint(Transform subtree, Vector3 from)
        {
            Vector3 best = from;
            float reach = -1f;

            foreach (MeshFilter filter in subtree.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Bounds local = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);

                    Vector3 point = filter.transform.TransformPoint(
                        local.center + Vector3.Scale(local.extents, sign));

                    float distance = (point - from).sqrMagnitude;
                    if (distance <= reach) continue;

                    reach = distance;
                    best = point;
                }
            }

            return best;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_shellTemplate != null) Destroy(_shellTemplate);
            if (_sponsonTemplate != null) Destroy(_sponsonTemplate);
        }
    }
}
