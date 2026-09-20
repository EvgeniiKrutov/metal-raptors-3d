using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class VehicleModel
    {
        const float ProbeY = -9000f;
        const float Epsilon = 0.0001f;

        static readonly Quaternion RoadYaw = Quaternion.Euler(0f, -90f, 0f);

        readonly string _resource;

        GameObject _prefab;
        bool _measured;
        Vector3 _size;
        Vector3 _yawedMin;

        public VehicleModel(string resource) => _resource = resource;

        public bool Ready => _measured && _prefab != null;

        public Vector3 Size => _size;

        public float Length => _size.x;

        public bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            _prefab = Resources.Load<GameObject>(_resource);
            if (_prefab == null)
            {
                Debug.LogError($"VehicleModel: {_resource} not found in Resources.");
                return false;
            }

            var holder = new GameObject("Vehicle Probe");
            holder.transform.position = new Vector3(0f, ProbeY, 0f);
            Object.Instantiate(_prefab, holder.transform, false);

            Bounds box = BoundsIn(holder.transform);
            Object.Destroy(holder);

            if (box.size.x <= Epsilon || box.size.z <= Epsilon)
            {
                Debug.LogError($"VehicleModel: {_resource} measured empty ({box.size}); skipped.");
                _prefab = null;
                return false;
            }

            float scale = PlaneModelConfig.UnitsPerMeter;
            _size = new Vector3(box.size.z, box.size.y, box.size.x) * scale;
            _yawedMin = new Vector3(-box.max.z, box.min.y, box.min.x) * scale;
            return true;
        }

        public Transform Build(Transform root)
        {
            if (!Measure()) return null;

            var holder = new GameObject("model");
            holder.transform.SetParent(root, false);
            holder.transform.localRotation = RoadYaw;
            holder.transform.localScale = Vector3.one * PlaneModelConfig.UnitsPerMeter;
            holder.transform.localPosition =
                new Vector3(-_size.x * 0.5f, 0f, -_size.z * 0.5f) - _yawedMin;

            GameObject view = Object.Instantiate(_prefab, holder.transform, false);

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            foreach (Collider collider in view.GetComponentsInChildren<Collider>())
                Object.Destroy(collider);

            return view.transform;
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
