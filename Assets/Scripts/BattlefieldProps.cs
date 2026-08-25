using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class BattlefieldProps : MonoBehaviour
    {
        public const int Layer = 9;

        const float TreeCellSize = 58f;
        const float HouseCellSize = 620f;
        const float StreamMargin = 500f;

        const float ZMin = 20f, ZMax = 700f;

        const float MetreScale = 7.2f;
        const float TreeOversize = 1.5f;
        const float HouseOversize = 1.5f;
        const float SizeJitter = 0.25f;
        const float TreeDepthNear = 200f;
        const float TreeDepthBoost = 0.5f;
        const float MaxPropRadius = 75f;

        const float MaxSlopeDeg = 35f;
        const float SlopeStep = 6f;

        const float ProbeY = -5000f;

        const string ModelFolder = "objects/";

        static readonly Quaternion StandUp = Quaternion.Euler(-90f, 0f, 0f);

        static readonly string[] TreeModels =
        {
            "trees/bent_tree_01", "trees/bent_tree_02", "trees/bent_tree_03", "trees/bent_tree_04",
            "trees/dead_tree_01", "trees/dead_tree_02", "trees/dead_tree_03", "trees/dead_tree_04",
            "trees/gnarled_tree_01", "trees/gnarled_tree_02",
            "trees/gnarled_tree_03", "trees/gnarled_tree_04",
        };

        static readonly string[] HouseModels =
        {
            "burned_houses/house_0", "burned_houses/house_1", "burned_houses/house_2",
            "burned_houses/house_3", "burned_houses/house_4", "burned_houses/house_5",
        };

        class Prototype
        {
            public GameObject prefab;
            public Bounds bounds;
            public Vector3 offset;
        }

        class Prop
        {
            public GameObject go;
            public float x, z, radius;
        }

        readonly Dictionary<string, Prototype> _prototypes = new Dictionary<string, Prototype>();
        readonly Dictionary<int, Prop> _trees = new Dictionary<int, Prop>();
        readonly Dictionary<int, Prop> _houses = new Dictionary<int, Prop>();
        readonly List<int> _scratch = new List<int>();

        Battlefield _field;
        int _seed;
        float _treeCell = TreeCellSize;

        public static BattlefieldProps Begin(Battlefield field, int seed)
        {
            var go = new GameObject("Battlefield Props");
            go.transform.SetParent(field.transform, false);

            var props = go.AddComponent<BattlefieldProps>();
            props._field = field;
            props._seed = seed;
            props._treeCell = TreeCellSize * GraphicsOptions.TreeCellScale;

            Physics.IgnoreLayerCollision(Layer, 0, true);

            props.Tick(field.CameraX);
            return props;
        }

        public void Tick(float camX)
        {
            UpdateGrid(_houses, HouseModels, HouseCellSize, 11, camX, tree: false);
            UpdateGrid(_trees, TreeModels, _treeCell, 12, camX, tree: true);
        }

        public bool Blocks(float x, float z, float margin, out Vector2 centre)
        {
            if (Nearest(_houses, HouseCellSize, x, z, margin, out centre)) return true;
            return Nearest(_trees, _treeCell, x, z, margin, out centre);
        }

        void UpdateGrid(Dictionary<int, Prop> grid, string[] models, float cellSize, int salt,
            float camX, bool tree)
        {
            int first = Mathf.FloorToInt((camX - _field.HalfViewWidth - StreamMargin) / cellSize);
            int last = Mathf.FloorToInt((camX + _field.HalfViewWidth + StreamMargin) / cellSize);

            _scratch.Clear();
            foreach (var kv in grid)
                if (kv.Key < first || kv.Key > last) _scratch.Add(kv.Key);

            foreach (int cell in _scratch)
            {
                var stale = grid[cell];
                if (stale != null && stale.go != null) Destroy(stale.go);
                grid.Remove(cell);
            }

            for (int cell = first; cell <= last; cell++)
            {
                if (grid.ContainsKey(cell)) continue;

                var rng = new System.Random(Hash(_seed, cell, salt));
                float x = (cell + (float)rng.NextDouble()) * cellSize;
                float z = Mathf.Lerp(ZMin, ZMax, (float)rng.NextDouble());
                string model = models[rng.Next(models.Length)];
                float yaw = (float)rng.NextDouble() * 360f;
                float size = 1f + ((float)rng.NextDouble() * 2f - 1f) * SizeJitter;
                if (tree) size *= 1f + DepthBoost(z) * (float)rng.NextDouble();

                if (!_field.SampleGround(x, z, out float y)) continue;

                if (_field.InCrater(x, z)
                    || SlopeDeg(x, z, y) > MaxSlopeDeg
                    || (tree && Nearest(_houses, HouseCellSize, x, z, 0f, out _)))
                {
                    grid[cell] = null;
                    continue;
                }

                grid[cell] = Build(model, x, y, z, yaw, size, tree);
            }
        }

        Prop Build(string model, float x, float y, float z, float yaw, float size, bool tree)
        {
            var proto = Load(model);
            if (proto == null) return null;

            float scale = MetreScale * (tree ? TreeOversize : HouseOversize) * size;
            float radius = Mathf.Max(proto.bounds.extents.x, proto.bounds.extents.z) * scale;

            var root = new GameObject(model);
            root.layer = Layer;
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(x, LowestGround(x, z, radius, y), z);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            root.transform.localScale = Vector3.one * scale;

            var view = Instantiate(proto.prefab, root.transform);
            view.transform.localPosition = proto.offset;
            view.transform.localRotation = StandUp;

            foreach (var r in view.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;

            AddCollider(root, proto.bounds, tree);
            return new Prop { go = root, x = x, z = z, radius = radius };
        }

        static void AddCollider(GameObject root, Bounds bounds, bool tree)
        {
            if (tree)
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.center = bounds.center;
                capsule.height = bounds.size.y;
                capsule.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                return;
            }

            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = bounds.center;
            box.size = bounds.size;
        }

        float LowestGround(float x, float z, float radius, float centre)
        {
            float reach = radius * 0.7f;
            float lowest = centre;

            for (int corner = 0; corner < 4; corner++)
            {
                float dx = (corner < 2 ? -reach : reach);
                float dz = (corner % 2 == 0 ? -reach : reach);
                if (_field.SampleGround(x + dx, z + dz, out float y)) lowest = Mathf.Min(lowest, y);
            }

            return lowest;
        }

        Prototype Load(string model)
        {
            if (_prototypes.TryGetValue(model, out var cached)) return cached;

            var prefab = Resources.Load<GameObject>(ModelFolder + model);
            if (prefab == null)
            {
                Debug.LogError($"BattlefieldProps: {ModelFolder}{model} not found in Resources.");
                _prototypes[model] = null;
                return null;
            }

            var proto = new Prototype { prefab = prefab };
            proto.bounds = MeasureBounds(prefab, out proto.offset);
            _prototypes[model] = proto;
            return proto;
        }

        static Bounds MeasureBounds(GameObject prefab, out Vector3 offset)
        {
            var probe = Instantiate(prefab);
            probe.transform.position = new Vector3(0f, ProbeY, 0f);
            probe.transform.rotation = StandUp;

            var renderers = probe.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(new Vector3(0f, ProbeY, 0f), Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            Destroy(probe);

            bounds.center -= new Vector3(0f, ProbeY, 0f);
            offset = new Vector3(0f, -bounds.min.y, 0f);
            bounds.center += offset;
            return bounds;
        }

        static float DepthBoost(float z) =>
            Mathf.InverseLerp(TreeDepthNear, ZMax, z) * TreeDepthBoost;

        float SlopeDeg(float x, float z, float y)
        {
            if (!_field.SampleGround(x + SlopeStep, z, out float alongX)) alongX = y;
            if (!_field.SampleGround(x, z + SlopeStep, out float alongZ)) alongZ = y;

            float rise = Mathf.Max(Mathf.Abs(alongX - y), Mathf.Abs(alongZ - y));
            return Mathf.Atan2(rise, SlopeStep) * Mathf.Rad2Deg;
        }

        static bool Nearest(Dictionary<int, Prop> grid, float cellSize,
            float x, float z, float margin, out Vector2 centre)
        {
            centre = Vector2.zero;

            float reach = MaxPropRadius + margin;
            int first = Mathf.FloorToInt((x - reach) / cellSize);
            int last = Mathf.FloorToInt((x + reach) / cellSize);

            float best = float.MaxValue;
            for (int cell = first; cell <= last; cell++)
            {
                if (!grid.TryGetValue(cell, out var prop) || prop == null) continue;

                float dx = x - prop.x, dz = z - prop.z;
                float distanceSq = dx * dx + dz * dz;
                float limit = prop.radius + margin;
                if (distanceSq > limit * limit || distanceSq >= best) continue;

                best = distanceSq;
                centre = new Vector2(prop.x, prop.z);
            }

            return best < float.MaxValue;
        }

        static int Hash(int seed, int cell, int salt)
        {
            unchecked
            {
                int h = seed;
                h = h * 486187739 + cell;
                h = h * 486187739 + salt;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
