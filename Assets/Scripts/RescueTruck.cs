using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class RescueTruck : MonoBehaviour
    {
        const float Speed = 36f;
        const float Brake = 30f;
        const float Gap = 30f;
        const float StopSnap = 0.3f;
        const float Epsilon = 0.0001f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly VehicleModel Model = new VehicleModel(EnemyTruck.ModelResource);

        static readonly string[] PaintNames = { "feldgrau_tint", "feldgrau", "cargo_bed" };

        static readonly Color[] Paints =
        {
            new Color(0.33f, 0.39f, 0.46f),
            new Color(0.40f, 0.47f, 0.56f),
            new Color(0.32f, 0.38f, 0.45f),
        };

        static readonly Dictionary<Material, Material> Painted = new Dictionary<Material, Material>();

        readonly Transform[] _wheels = new Transform[EnemyTruck.WheelNodes.Length];
        CampaignTerrain _land;
        float _stopX;
        float _roadLift;
        float _z;
        float _wheelRadius = 1f;

        public bool Parked { get; private set; }

        public static float ColumnLength(int count) =>
            count > 0 && Model.Measure() ? count * Model.Length + (count - 1) * Gap : 0f;

        public static List<RescueTruck> Column(int count, float entryX, float frontStopX, float z,
            float roadLift, CampaignTerrain land, float lift)
        {
            var column = new List<RescueTruck>();
            if (!Model.Measure()) return column;

            float length = Model.Length;
            float step = length + Gap;
            float travel = Mathf.Max(0f, frontStopX - entryX);
            for (int i = 0; i < count; i++)
            {
                float start = entryX - length * 0.5f - i * step;
                RescueTruck truck = Spawn(new Vector3(start, ProceduralTerrain.BaseLevel, z),
                    start + travel, roadLift, land, lift);
                if (truck != null) column.Add(truck);
            }
            return column;
        }

        static RescueTruck Spawn(Vector3 point, float stopX, float roadLift, CampaignTerrain land, float lift)
        {
            var root = new GameObject("Rescue Truck");
            root.transform.position = point;

            Transform turn = new GameObject("turn").transform;
            turn.SetParent(root.transform, false);
            turn.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Transform view = Model.Build(turn);
            MaterialLift.Apply(view, lift);
            Repaint(view);
            SetLayer(root.transform, PlaneFactory.PlaneLayer);

            var truck = root.AddComponent<RescueTruck>();
            truck.Begin(stopX, roadLift, land, view);
            return truck;
        }

        void Begin(float stopX, float roadLift, CampaignTerrain land, Transform view)
        {
            _stopX = stopX;
            _roadLift = roadLift;
            _land = land;
            _z = transform.position.z;
            FindWheels(view);
            Place(transform.position.x);
            if (_stopX - transform.position.x <= StopSnap) Parked = true;
        }

        void Update()
        {
            if (Parked) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float x = transform.position.x;
            float left = _stopX - x;
            if (left <= StopSnap)
            {
                Place(_stopX);
                Parked = true;
                return;
            }

            float pace = Mathf.Min(Speed, Mathf.Sqrt(2f * Brake * left));
            float move = Mathf.Min(pace * dt, left);
            Place(x + move);
            Roll(move);
        }

        void Place(float x)
        {
            float y = _land != null && _land.SampleHeight(x, _z, out float ground)
                ? ground
                : ProceduralTerrain.BaseLevel;

            transform.position = new Vector3(x, y + _roadLift, _z);
        }

        void Roll(float distance)
        {
            if (_wheelRadius <= Epsilon) return;

            float degrees = -distance / _wheelRadius * Mathf.Rad2Deg;
            foreach (Transform wheel in _wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, degrees, Space.Self);
        }

        void FindWheels(Transform view)
        {
            if (view == null) return;

            float best = 0f;
            for (int i = 0; i < _wheels.Length; i++)
            {
                _wheels[i] = PlaneFactory.FindDeep(view, EnemyTruck.WheelNodes[i]);
                if (_wheels[i] == null) continue;

                best = Mathf.Max(best, _wheels[i].position.y - transform.position.y);
            }

            if (best > Epsilon) _wheelRadius = best;
        }

        static void Repaint(Transform view)
        {
            if (view == null) return;

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Blue(mats[i]);
                renderer.sharedMaterials = mats;
            }
        }

        static Material Blue(Material source)
        {
            if (source == null) return null;

            int paint = PaintIndex(source.name);
            if (paint < 0) return source;
            if (Painted.TryGetValue(source, out Material cached) && cached != null) return cached;

            var copy = new Material(source) { name = source.name + " (blue)" };
            if (copy.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, Paints[paint]);
            else if (copy.HasProperty(ColorId)) copy.SetColor(ColorId, Paints[paint]);

            Painted[source] = copy;
            return copy;
        }

        static int PaintIndex(string name)
        {
            for (int i = 0; i < PaintNames.Length; i++)
                if (name.StartsWith(PaintNames[i])) return i;
            return -1;
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }
    }
}
