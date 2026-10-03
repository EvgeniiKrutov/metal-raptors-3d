using UnityEngine;

namespace MetalRaptors
{
    public static class FlandersGroyne
    {
        public const string ModelResource = "objects/flanders_groyne";

        const string LandNode = "pile_00";
        const string SeaNode = "pile_09";
        const string PilesNode = "piles";
        const float Tall = 1.5f;
        const float PileBury = 2f;
        const float ProbeY = -9000f;
        const float Epsilon = 0.0001f;

        static GameObject _prefab;
        static bool _measured;
        static Vector3 _landAnchor;
        static Vector3 _axis;

        static float Scale => PlaneModelConfig.UnitsPerMeter;

        public static float Length => _axis.magnitude * Scale;

        public static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            _prefab = Resources.Load<GameObject>(ModelResource);
            if (_prefab == null)
            {
                Debug.LogError($"FlandersGroyne: {ModelResource} not found in Resources.");
                return false;
            }

            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("Groyne Probe");
            holder.transform.position = origin;
            var view = Object.Instantiate(_prefab, holder.transform, false);

            Transform land = PlaneFactory.FindDeep(view.transform, LandNode);
            Transform sea = PlaneFactory.FindDeep(view.transform, SeaNode);
            if (land != null && sea != null)
            {
                Vector3 a = Centre(land) - origin;
                Vector3 b = Centre(sea) - origin;
                _landAnchor = new Vector3(a.x, 0f, a.z);
                _axis = new Vector3(b.x - a.x, 0f, b.z - a.z);
            }

            Object.Destroy(holder);

            if (_axis.sqrMagnitude <= Epsilon)
            {
                Debug.LogError($"FlandersGroyne: {ModelResource} has no {LandNode} to {SeaNode} "
                               + "run; groynes are skipped.");
                _prefab = null;
                return false;
            }

            return true;
        }

        public static void Place(Transform parent, Vector3 land, Vector3 sea,
            System.Func<float, float, float> ground)
        {
            if (!Measure()) return;

            Vector3 run = sea - land;
            if (run.sqrMagnitude <= Epsilon) return;

            Quaternion rotation = Quaternion.LookRotation(run, Vector3.up)
                                  * Quaternion.Inverse(Quaternion.LookRotation(_axis, Vector3.up));

            var root = new GameObject("Groyne");
            root.transform.SetParent(parent, false);
            root.transform.localScale = new Vector3(Scale, Scale * Tall, Scale);
            root.transform.SetPositionAndRotation(land - rotation * (_landAnchor * Scale), rotation);

            var view = Object.Instantiate(_prefab, root.transform, false);
            view.name = "flanders_groyne";
            NightReceivers.Adopt(view);
            NightLights.MarkCaster(view);

            Transform piles = PlaneFactory.FindDeep(view.transform, PilesNode);
            if (piles == null) return;
            foreach (Transform pile in piles) Reach(pile, ground);
        }

        static void Reach(Transform pile, System.Func<float, float, float> ground)
        {
            var renderer = pile.GetComponent<Renderer>();
            if (renderer == null) return;

            Bounds bounds = renderer.bounds;
            float span = bounds.size.y;
            float target = ground(bounds.center.x, bounds.center.z) - PileBury;
            if (bounds.min.y <= target || span <= Epsilon) return;

            float stretch = (bounds.max.y - target) / span;
            Vector3 up = pile.InverseTransformDirection(Vector3.up);
            float ax = Mathf.Abs(up.x), ay = Mathf.Abs(up.y), az = Mathf.Abs(up.z);
            int axis = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;

            Vector3 scale = pile.localScale;
            scale[axis] *= stretch;
            pile.localScale = scale;

            float pivot = pile.position.y;
            pile.position += Vector3.up * ((bounds.max.y - pivot) * (1f - stretch));
        }

        static Vector3 Centre(Transform node)
        {
            var renderer = node.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds.center : node.position;
        }
    }
}
