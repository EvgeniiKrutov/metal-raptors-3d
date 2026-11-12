using UnityEngine;

namespace MetalRaptors
{
    public static class VosgesHouse
    {
        public const string ModelResource = "objects/alpine_houses/alpine_house_";
        public const int Models = 6;

        const string DoorSuffix = "_Door";

        static readonly GameObject[] Prefabs = new GameObject[Models];
        static readonly Bounds[] Boxes = new Bounds[Models];
        static readonly float[] Facings = new float[Models];
        static bool _measured;

        public static bool Ready { get; private set; }

        public static void Measure()
        {
            if (_measured) return;
            _measured = true;

            for (int i = 0; i < Models; i++)
            {
                string path = ModelResource + i;
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"VosgesHouse: {path} not found in Resources.");
                    continue;
                }

                GameObject view = ModelProbe.Open(prefab);
                if (!ModelProbe.Bounds(view, out Bounds box))
                {
                    Debug.LogError($"VosgesHouse: {path} has no renderers; it is skipped.");
                    ModelProbe.Close(view);
                    continue;
                }

                Vector3 front = Vector3.back;
                Transform door = Door(view.transform);
                if (door != null && ModelProbe.Bounds(door.gameObject, out Bounds doorBox))
                    front = Wall(doorBox.center - box.center, box.extents);
                else
                    Debug.LogWarning($"VosgesHouse: {path} has no *{DoorSuffix} node; its front is guessed.");

                Prefabs[i] = prefab;
                Boxes[i] = box;
                Facings[i] = 180f - Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg;
                Ready = true;
                ModelProbe.Close(view);
            }
        }

        static Transform Door(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(DoorSuffix, System.StringComparison.Ordinal)) return t;
            return null;
        }

        static Vector3 Wall(Vector3 offset, Vector3 extents)
        {
            float sx = extents.x > 1e-4f ? offset.x / extents.x : 0f;
            float sz = extents.z > 1e-4f ? offset.z / extents.z : 0f;
            if (Mathf.Abs(sx) > Mathf.Abs(sz)) return new Vector3(Mathf.Sign(sx), 0f, 0f);
            if (Mathf.Abs(sz) > 0f) return new Vector3(0f, 0f, Mathf.Sign(sz));
            return Vector3.back;
        }

        public static bool Has(int model) =>
            model >= 0 && model < Models && Prefabs[model] != null;

        public static Bounds Footprint(int model) => Boxes[model];

        public static float Facing(int model) => Facings[model];

        public static Transform Place(Transform parent, VosgesLayout.HouseSpot spot)
        {
            if (!Has(spot.model)) return null;

            var root = new GameObject($"House {spot.model}");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(spot.origin, spot.rotation);
            root.transform.localScale = Vector3.one * PlaneModelConfig.UnitsPerMeter;

            var view = Object.Instantiate(Prefabs[spot.model], root.transform, false);
            view.name = Prefabs[spot.model].name;
            NightReceivers.Adopt(view);
            NightLights.MarkCaster(view);
            return root.transform;
        }
    }
}
