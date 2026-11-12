using UnityEngine;

namespace MetalRaptors
{
    public static class VosgesWindmill
    {
        const string ModelResource = "objects/austrian_windmill";
        const string TowerNode = "Tower";
        const string SailsNode = "Sails";
        const float TurnSeconds = 12f;

        static GameObject _prefab;
        static Vector3 _axis;
        static bool _measured;

        public static bool Ready { get; private set; }
        public static float Radius { get; private set; }
        public static Vector3 Centre { get; private set; }
        public static float FacingYaw { get; private set; }

        public static void Measure()
        {
            if (_measured) return;
            _measured = true;

            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"VosgesWindmill: {ModelResource} not found in Resources.");
                return;
            }

            GameObject view = ModelProbe.Open(prefab);
            Transform tower = PlaneFactory.FindDeep(view.transform, TowerNode);
            Transform sails = PlaneFactory.FindDeep(view.transform, SailsNode);
            if (tower == null || sails == null || !tower.TryGetComponent(out Renderer towerRenderer))
            {
                Debug.LogError($"VosgesWindmill: {ModelResource} has no {TowerNode}/{SailsNode}; it is skipped.");
                ModelProbe.Close(view);
                return;
            }

            Bounds box = towerRenderer.bounds;
            Vector3 centre = ModelProbe.Local(box.center);
            Vector3 hub = ModelProbe.Local(sails.position);
            Vector3 front = hub - centre;
            front.y = 0f;
            if (front.sqrMagnitude < 1e-6f) front = Vector3.back;
            front.Normalize();

            _prefab = prefab;
            _axis = sails.parent != null ? sails.parent.InverseTransformDirection(front) : front;
            Centre = new Vector3(centre.x, 0f, centre.z);
            Radius = Mathf.Max(box.extents.x, box.extents.z);
            FacingYaw = 180f - Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg;
            Ready = true;
            ModelProbe.Close(view);
        }

        public static Transform Place(Transform parent, VosgesLayout.MillSpot spot)
        {
            if (!Ready) return null;

            var root = new GameObject("Windmill");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(spot.origin, spot.rotation);
            root.transform.localScale = Vector3.one * PlaneModelConfig.UnitsPerMeter;

            var view = Object.Instantiate(_prefab, root.transform, false);
            view.name = _prefab.name;
            NightReceivers.Adopt(view);
            NightLights.MarkCaster(view);

            Transform sails = PlaneFactory.FindDeep(view.transform, SailsNode);
            if (sails != null)
                sails.gameObject.AddComponent<WindmillSails>().Begin(_axis, 360f / TurnSeconds, spot.phase);

            return root.transform;
        }
    }

    public class WindmillSails : MonoBehaviour
    {
        Vector3 _axis;
        Quaternion _rest;
        float _speed;
        float _angle;

        public void Begin(Vector3 axis, float degreesPerSecond, float phase)
        {
            _axis = axis;
            _rest = transform.localRotation;
            _speed = degreesPerSecond;
            _angle = phase;
            Apply();
        }

        void Update()
        {
            _angle = Mathf.Repeat(_angle + _speed * Time.deltaTime, 360f);
            Apply();
        }

        void Apply() => transform.localRotation = Quaternion.AngleAxis(_angle, _axis) * _rest;
    }
}
