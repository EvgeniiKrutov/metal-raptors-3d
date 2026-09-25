using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class BattlefieldRubble : MonoBehaviour
    {
        const float CellSize = 8f;
        const float StreamMargin = 360f;
        const float ZMin = 0f;
        const float ForegroundBias = 1.585f;
        const int Salt = 21;

        const float SizeJitter = 0.12f;
        const float Clearance = 2f * BattlefieldProps.MetreScale;
        const float PieceRadiusMax = 1.6f * BattlefieldProps.MetreScale;
        const float MaxSlopeDeg = 40f;
        const float MinSlopeStep = 2f;
        const float SinkFraction = 0.2f;
        const float MaxSink = 0.1f * BattlefieldProps.MetreScale;

        const float ProbeY = -5000f;

        const string ModelFolder = "objects/rubble/";
        const int FamilySize = 6;

        static readonly string[][] Families =
        {
            Family("barrels/barrel_"),
            Family("crates/crate_"),
            Family("planks/plank_"),
            Family("rocks/rock_"),
            Family("sandbags/sandbag_"),
            Family("scrap/scrap_"),
        };

        class Prototype
        {
            public GameObject prefab;
            public Vector3 offset;
            public float radius, height;
        }

        readonly Dictionary<string, Prototype> _prototypes = new Dictionary<string, Prototype>();
        readonly Dictionary<int, GameObject> _pieces = new Dictionary<int, GameObject>();
        readonly List<int> _scratch = new List<int>();

        Battlefield _field;
        int _seed;
        float _cell;

        public static BattlefieldRubble Begin(Battlefield field, int seed)
        {
            var go = new GameObject("Battlefield Rubble");
            go.transform.SetParent(field.transform, false);

            var rubble = go.AddComponent<BattlefieldRubble>();
            rubble._field = field;
            rubble._seed = seed;
            rubble._cell = CellSize * GraphicsOptions.RubbleCellScale;

            rubble.Tick(field.CameraX);
            return rubble;
        }

        public void Tick(float camX)
        {
            int first = Mathf.FloorToInt((camX - _field.HalfViewWidth - StreamMargin) / _cell);
            int last = Mathf.FloorToInt((camX + _field.HalfViewWidth + StreamMargin) / _cell);

            _scratch.Clear();
            foreach (var kv in _pieces)
                if (kv.Key < first || kv.Key > last) _scratch.Add(kv.Key);

            foreach (int cell in _scratch)
            {
                if (_pieces[cell] != null) Destroy(_pieces[cell]);
                _pieces.Remove(cell);
            }

            for (int cell = first; cell <= last; cell++)
            {
                if (_pieces.ContainsKey(cell)) continue;
                if (TryPiece(cell, out GameObject piece)) _pieces[cell] = piece;
            }
        }

        bool TryPiece(int cell, out GameObject piece)
        {
            piece = null;

            var rng = new System.Random(BattlefieldProps.Hash(_seed, cell, Salt));
            float x = (cell + (float)rng.NextDouble()) * _cell;
            float z = Mathf.Lerp(ZMin, _field.PeopleZMax,
                Mathf.Pow((float)rng.NextDouble(), ForegroundBias));
            if (!_field.InBand(x)) return true;

            if (_field.Props != null && !_field.Props.Settled(x, PieceRadiusMax + Clearance))
                return false;
            if (!_field.SampleGround(x - 2f * PieceRadiusMax, z, out _)) return false;
            if (!_field.SampleGround(x + 2f * PieceRadiusMax, z, out _)) return false;

            var proto = Load(Pick(rng));
            float scale = BattlefieldProps.MetreScale
                          * (1f + ((float)rng.NextDouble() * 2f - 1f) * SizeJitter);
            float yaw = (float)rng.NextDouble() * 360f;
            if (proto == null) return true;

            float radius = proto.radius * scale;
            if (!Clear(x, z, radius)) return true;
            if (!GroundNormal(x, z, Mathf.Max(MinSlopeStep, radius), out Vector3 normal))
                return true;
            if (Vector3.Angle(Vector3.up, normal) > MaxSlopeDeg) return true;
            if (!_field.SampleGround(x, z, out float y)) return true;

            float sink = Mathf.Min(MaxSink, proto.height * scale * SinkFraction);
            piece = Place(proto, new Vector3(x, y - sink, z), normal, yaw, scale);
            return true;
        }

        bool Clear(float x, float z, float radius)
        {
            float margin = Clearance + radius;
            if (z - radius < ZMin || z > _field.PeopleZMax) return false;
            if (!_field.InBand(x - margin) || !_field.InBand(x + margin)) return false;
            if (_field.OnRoad(z, margin)) return false;

            return _field.Props == null || !_field.Props.Blocks(x, z, margin, out _);
        }

        bool GroundNormal(float x, float z, float step, out Vector3 normal)
        {
            normal = Vector3.up;
            if (!_field.SampleGround(x - step, z, out float left)) return false;
            if (!_field.SampleGround(x + step, z, out float right)) return false;
            float zNear = Mathf.Max(ZMin, z - step);
            if (!_field.SampleGround(x, zNear, out float near)) return false;
            if (!_field.SampleGround(x, z + step, out float far)) return false;

            normal = new Vector3((left - right) / (2f * step), 1f,
                (near - far) / (z + step - zNear)).normalized;
            return true;
        }

        GameObject Place(Prototype proto, Vector3 position, Vector3 normal, float yaw,
            float scale)
        {
            var root = new GameObject(proto.prefab.name);
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal)
                                      * Quaternion.Euler(0f, yaw, 0f);
            root.transform.localScale = Vector3.one * scale;

            var view = Instantiate(proto.prefab, root.transform);
            view.transform.localPosition = proto.offset;

            foreach (var r in view.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;

            return root;
        }

        Prototype Load(string model)
        {
            if (_prototypes.TryGetValue(model, out var cached)) return cached;

            var prefab = Resources.Load<GameObject>(ModelFolder + model);
            if (prefab == null)
            {
                Debug.LogError($"BattlefieldRubble: {ModelFolder}{model} not found in Resources.");
                _prototypes[model] = null;
                return null;
            }

            var bounds = Measure(prefab);
            var proto = new Prototype
            {
                prefab = prefab,
                offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z),
                radius = Mathf.Max(bounds.extents.x, bounds.extents.z),
                height = bounds.size.y,
            };
            _prototypes[model] = proto;
            return proto;
        }

        static Bounds Measure(GameObject prefab)
        {
            var probe = Instantiate(prefab);
            probe.transform.position = new Vector3(0f, ProbeY, 0f);

            var renderers = probe.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(new Vector3(0f, ProbeY, 0f), Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            Destroy(probe);

            bounds.center -= new Vector3(0f, ProbeY, 0f);
            return bounds;
        }

        static string Pick(System.Random rng)
        {
            var family = Families[rng.Next(Families.Length)];
            return family[rng.Next(family.Length)];
        }

        static string[] Family(string stem)
        {
            var models = new string[FamilySize];
            for (int i = 0; i < FamilySize; i++) models[i] = stem + i;
            return models;
        }
    }
}
