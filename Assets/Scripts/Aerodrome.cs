using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class Aerodrome : MonoBehaviour
    {
        public const string ModelResource = "objects/aerodrome_stow_maries";

        const string CamelNode = "Camel_01";
        const float ProbeY = -9000f;
        const float Epsilon = 0.0001f;

        static readonly Quaternion FieldYaw = Quaternion.Euler(0f, -90f, 0f);

        static GameObject _prefab;
        static bool _measured;
        static float _scale = PlaneModelConfig.UnitsPerMeter;
        static Vector3 _size;
        static Vector3 _modelMin;

        public static bool Ready => _measured && _prefab != null;
        public static float Scale => _scale;
        public static float Width => _size.x;
        public static float Height => _size.y;
        public static float LandDepth => _size.z;

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
            return true;
        }

        static float CamelSpan(Transform camel) =>
            BoundsIn(camel.worldToLocalMatrix, camel).size.x
            * camel.TransformVector(Vector3.right).magnitude;

        public static Aerodrome Place(float leftX, float nearZ, float groundY)
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

            SetLayer(root.transform, BattlefieldProps.Layer);
            return root.AddComponent<Aerodrome>();
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
