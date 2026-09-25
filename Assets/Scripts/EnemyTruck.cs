using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class EnemyTruck : GroundVehicle
    {
        public const string ModelResource = "objects/machines/truck_ww1";

        const float Health = 100f;
        const float Speed = 30f;

        const float BulletDamage = 6f;
        const float BulletSpeed = 200f;
        const float FireInterval = 1f;
        const float FireRange = 500f;
        const float LeadFactor = 1f;

        const float BedRear = 0.16f;
        const float BedHeight = 0.86f;
        const float MuzzleClear = 5f;
        const float FenceSparkSize = 16f;

        const float LiftGamma = 1f / 2.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Dictionary<Material, Material> Lifted =
            new Dictionary<Material, Material>();

        static readonly VehicleModel Model = new VehicleModel(ModelResource);

        static readonly string[] WheelNodes =
            { "Wheel_FL_Spin", "Wheel_FR_Spin", "Wheel_RL_Spin", "Wheel_RR_Spin" };

        public static bool Measure() => Model.Measure();

        public static float BodyLength => Model.Length;

        public override float MaxHealth => Health;

        protected override float DriveSpeed => Speed;

        readonly Transform[] _wheels = new Transform[WheelNodes.Length];
        float _wheelRadius = 1f;
        float _fireCooldown;
        GameObject _bulletTemplate;

        Vector3 Muzzle => transform.position
                          + new Vector3(Size.x * BedRear, Size.y * BedHeight, 0f);

        public static EnemyTruck Spawn(Vector3 groundPoint, float fenceStopX, float roadLift,
            Rigidbody target, CampaignTerrain land)
        {
            if (!Model.Measure()) return null;

            var root = new GameObject("Enemy Truck");
            root.transform.position = groundPoint;

            Transform view = Model.Build(root.transform);
            LiftColours(view);
            SetLayer(root.transform, PlaneFactory.PlaneLayer);

            var truck = root.AddComponent<EnemyTruck>();
            truck.Initialize(fenceStopX, roadLift, target, land, view);
            return truck;
        }

        void Initialize(float fenceStopX, float roadLift, Rigidbody target, CampaignTerrain land,
            Transform view)
        {
            Begin(Model.Size, fenceStopX, roadLift, target, land);

            _bulletTemplate = Bullet.BuildTemplate(Bullet.RoundColor);
            FindWheels(view);
        }

        protected override void Roll(float dt)
        {
            if (_wheelRadius <= Epsilon) return;

            float degrees = -Speed / _wheelRadius * Mathf.Rad2Deg * dt;
            foreach (Transform wheel in _wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, degrees, Space.Self);
        }

        protected override void Aim(float dt)
        {
            _fireCooldown -= dt;
            if (_fireCooldown > 0f) return;

            if (Halted) ShellFence();
            else ShootTarget();
        }

        void ShootTarget()
        {
            if (Target == null || !OnCamera(Centre)) return;

            Vector3 muzzle = Muzzle;
            if (Vector3.Distance(muzzle, Target.position) > FireRange) return;

            Vector3 dir = Intercept(muzzle, BulletSpeed, LeadFactor) - muzzle;
            dir.z = 0f;
            if (dir.sqrMagnitude < 1f) return;

            Shoot(muzzle, dir.normalized);
        }

        void ShellFence()
        {
            Airfield field = Airfield.Current;
            if (field == null || field.Lost) return;

            Vector3 muzzle = Muzzle;
            Vector3 aim = field.AimPoint(PlaneZ);
            Vector3 dir = aim - muzzle;
            dir.z = 0f;
            if (dir.sqrMagnitude < 1f) return;

            Shoot(muzzle, dir.normalized);
            field.Shell(Airfield.MaxHealth * FireInterval / Airfield.ShellSeconds);
            Sparks.Spawn(aim, FenceSparkSize);
        }

        void Shoot(Vector3 muzzle, Vector3 dir)
        {
            _fireCooldown = FireInterval;
            FireRound(_bulletTemplate, muzzle + dir * MuzzleClear, dir, BulletSpeed, BulletDamage,
                Size.y);
        }

        void FindWheels(Transform view)
        {
            if (view == null) return;

            float best = 0f;
            for (int i = 0; i < WheelNodes.Length; i++)
            {
                _wheels[i] = PlaneFactory.FindDeep(view, WheelNodes[i]);
                if (_wheels[i] == null) continue;

                best = Mathf.Max(best, _wheels[i].position.y - transform.position.y);
            }

            if (best > Epsilon) _wheelRadius = best;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_bulletTemplate != null) Destroy(_bulletTemplate);
        }

        static void LiftColours(Transform view)
        {
            if (view == null) return;

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = LiftAll(renderer.sharedMaterials);
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
    }
}
