using UnityEngine;

namespace MetalRaptors
{
    public static class FlandersBunker
    {
        public const string ModelResource = "objects/bunkers/bunker_";
        public const int Models = 3;

        const float Shrink = 0.77f;
        const float MuzzleHeight = 2.9f;
        const float ProbeY = -9000f;

        static readonly GameObject[] Prefabs = new GameObject[Models];
        static readonly Bounds[] Boxes = new Bounds[Models];
        static bool _measured;
        static bool _any;

        public static float Scale => PlaneModelConfig.UnitsPerMeter * Shrink;

        public static bool Measure()
        {
            if (_measured) return _any;
            _measured = true;

            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("Bunker Probe");
            holder.transform.position = origin;

            for (int i = 0; i < Models; i++)
            {
                string path = ModelResource + i;
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"FlandersBunker: {path} not found in Resources.");
                    continue;
                }

                var view = Object.Instantiate(prefab, holder.transform, false);
                Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Debug.LogError($"FlandersBunker: {path} has no renderers; it is skipped.");
                    continue;
                }

                Bounds box = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) box.Encapsulate(renderers[r].bounds);
                box.center -= origin;

                Prefabs[i] = prefab;
                Boxes[i] = box;
                _any = true;
            }

            Object.Destroy(holder);
            return _any;
        }

        public static bool Has(int model) =>
            Measure() && model >= 0 && model < Models && Prefabs[model] != null;

        public static Bounds Footprint(int model) =>
            new Bounds(Boxes[model].center * Scale, Boxes[model].size * Scale);

        public static void Place(Transform parent, int model, Vector3 origin, Quaternion rotation)
        {
            if (!Has(model)) return;

            var root = new GameObject("Bunker");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(origin, rotation);
            root.transform.localScale = Vector3.one * Scale;

            var view = Object.Instantiate(Prefabs[model], root.transform, false);
            view.name = Prefabs[model].name;
            NightReceivers.Adopt(view);
            NightLights.MarkCaster(view);

            Physics.IgnoreLayerCollision(BattlefieldProps.Layer, 0, true);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Boxes[model].center;
            box.size = Boxes[model].size;

            root.AddComponent<BunkerGun>().Aim(new Vector3(0f, MuzzleHeight, Boxes[model].max.z));
        }
    }
}
