using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class Aerodrome : MonoBehaviour
    {
        public const string ModelResource = "objects/aerodrome_stow_maries";

        const string CamelNode = "Camel_01";
        const string TowerNode = "Tower";
        const string GroundNode = "Ground";

        // Every group null holds its buildings as direct children, so one rule covers all four.
        static readonly string[] SolidGroups = { "Hangars", "Quarters", "Sheds", "Tower" };
        const float ProbeY = -9000f;
        const float Epsilon = 0.0001f;

        const int FireCount = 5;
        const float FireRadiusMin = 7f, FireRadiusMax = 14f;
        const float FireFromX = 0.07f, FireSpanX = 0.74f;
        const float FireFromZ = 0.05f, FireSpanZ = 0.58f;

        static readonly Quaternion FieldYaw = Quaternion.Euler(0f, -90f, 0f);

        static GameObject _prefab;
        static bool _measured;
        static float _scale = PlaneModelConfig.UnitsPerMeter;
        static Vector3 _size;
        static Vector3 _modelMin;
        static float _towerLeft;
        static Vector3 _hardMin, _hardSize;

        public static bool Ready => _measured && _prefab != null;
        public static float Scale => _scale;
        public static float Width => _size.x;
        public static float Height => _size.y;
        public static float LandDepth => _size.z;

        // The tower's near face, in world units from the placed field's own left edge — what the
        // level offsets the field by so the tower opens the scene. See docs/aerodrome.md.
        public static float TowerLeft => _towerLeft;

        // The model's own apron slabs, in world XZ for a field placed at (leftX, nearZ). The
        // land keeps its grass off this and grows it over the rest of the field.
        public static Rect Hardstanding(float leftX, float nearZ) => new Rect(
            leftX + _hardMin.x, nearZ + _hardMin.z, _hardSize.x, _hardSize.z);

        public static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            _prefab = Resources.Load<GameObject>(ModelResource);
            if (_prefab == null)
            {
                Debug.LogError($"Aerodrome: {ModelResource} not found in Resources.");
                return false;
            }

            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("Aerodrome Probe");
            holder.transform.SetPositionAndRotation(origin, FieldYaw);
            Instantiate(_prefab, holder.transform, false);

            Transform camel = PlaneFactory.FindDeep(holder.transform, CamelNode);
            float span = camel != null ? CamelSpan(camel) : 0f;
            Bounds whole = BoundsIn(Matrix4x4.identity, holder.transform);

            Transform tower = PlaneFactory.FindDeep(holder.transform, TowerNode);
            float towerMinX = tower != null
                ? BoundsIn(Matrix4x4.identity, tower).min.x
                : whole.min.x;

            Transform apron = PlaneFactory.FindDeep(holder.transform, GroundNode);
            Bounds hard = apron != null ? BoundsIn(Matrix4x4.identity, apron) : new Bounds();

            Destroy(holder);

            if (span <= Epsilon || whole.size.x <= Epsilon)
            {
                Debug.LogError($"Aerodrome: {ModelResource} measured empty"
                               + $" (span {span}, width {whole.size.x}); the field is skipped.");
                _prefab = null;
                return false;
            }

            _scale = PlaneModels.Sopwith.OnScreenSize / span;
            _modelMin = whole.min - origin;
            _size = whole.size * _scale;
            _towerLeft = (towerMinX - whole.min.x) * _scale;
            _hardMin = (hard.min - whole.min) * _scale;
            _hardSize = hard.size * _scale;
            return true;
        }

        static float CamelSpan(Transform camel) =>
            BoundsIn(camel.worldToLocalMatrix, camel).size.x
            * camel.TransformVector(Vector3.right).magnitude;

        public static Aerodrome Place(float leftX, float nearZ, float groundY, int seed)
        {
            if (!Measure()) return null;

            var root = new GameObject("Aerodrome");
            root.transform.localScale = Vector3.one * _scale;
            root.transform.SetPositionAndRotation(
                new Vector3(leftX, groundY, nearZ) - _modelMin * _scale, FieldYaw);

            var view = Instantiate(_prefab, root.transform, false);
            view.name = "stow_maries";

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            foreach (Collider collider in view.GetComponentsInChildren<Collider>())
                Destroy(collider);

            Solidify(view.transform);
            SetLayer(root.transform, BattlefieldProps.Layer);
            LightFires(leftX, nearZ, groundY, seed);
            return root.AddComponent<Aerodrome>();
        }

        // The buildings are the one part of the field a plane cannot fly through. Each gets a
        // trigger box, which on `BattlefieldProps.Layer` is what `CubeController.OnTriggerEnter`
        // reads as a scrape — the same answer a battlefield house gives, not a crash.
        static void Solidify(Transform view)
        {
            foreach (string group in SolidGroups)
            {
                Transform node = PlaneFactory.FindDeep(view, group);
                if (node == null)
                {
                    Debug.LogWarning($"Aerodrome: {ModelResource} has no {group} node; "
                                     + "nothing in it can be hit.");
                    continue;
                }

                for (int i = 0; i < node.childCount; i++) Enclose(node.GetChild(i));
            }
        }

        // Measured in the building's own local space, so a yawed barrack gets a box that fits it
        // rather than the world-aligned one its bounds would give.
        static void Enclose(Transform building)
        {
            Bounds box = BoundsIn(building.worldToLocalMatrix, building);
            if (box.size.x <= Epsilon || box.size.y <= Epsilon || box.size.z <= Epsilon) return;

            var collider = building.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = box.center;
            collider.size = box.size;
        }

        // Braziers and burn barrels down the field, so the airfield has something alight on it
        // rather than reading as an empty apron. Their holder is unscaled — the model's root is
        // not, and a fire parented to it would come out `Scale` times too big.
        static void LightFires(float leftX, float nearZ, float groundY, int seed)
        {
            var holder = new GameObject("Aerodrome Fires");
            var rng = new System.Random(seed ^ 0x0A17);

            for (int i = 0; i < FireCount; i++)
            {
                float alongX = (i + (float)rng.NextDouble()) / FireCount;
                float x = leftX + Width * (FireFromX + FireSpanX * alongX);
                float z = nearZ + LandDepth * (FireFromZ + FireSpanZ * (float)rng.NextDouble());
                float radius = Mathf.Lerp(FireRadiusMin, FireRadiusMax, (float)rng.NextDouble());

                WreckFire.Begin(holder.transform, new Vector3(x, groundY, z), radius, seed + i);
            }
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }

        static Bounds BoundsIn(Matrix4x4 toSpace, Transform subtree)
        {
            bool any = false;
            var bounds = new Bounds();

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
