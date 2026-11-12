using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class VosgesCow
    {
        const string ModelResource = "objects/cow";
        const string BrownTexture = "animals/swiss_cow_texture_brown";
        const float Sink = 0.12f;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly Dictionary<Material, Material> Browns = new Dictionary<Material, Material>();

        static GameObject _prefab;
        static Texture2D _brown;
        static float _floor;
        static bool _measured;

        public static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"VosgesCow: {ModelResource} not found in Resources.");
                return false;
            }

            GameObject view = ModelProbe.Open(prefab);
            bool measured = ModelProbe.Bounds(view, out Bounds box);
            ModelProbe.Close(view);
            if (!measured)
            {
                Debug.LogError($"VosgesCow: {ModelResource} has no renderers; it is skipped.");
                return false;
            }

            _brown = Resources.Load<Texture2D>(BrownTexture);
            if (_brown == null) Debug.LogError($"VosgesCow: {BrownTexture} not found in Resources; all cows stay black.");

            _prefab = prefab;
            _floor = box.min.y;
            return true;
        }

        public static void Place(Transform parent, VosgesLayout.CowSpot spot)
        {
            if (!Measure()) return;

            float scale = PlaneModelConfig.UnitsPerMeter * spot.scale;
            var root = new GameObject("Cow");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);
            Vector3 up = spot.up.sqrMagnitude > 0.01f ? spot.up.normalized : Vector3.up;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, spot.yaw, 0f);
            root.transform.SetPositionAndRotation(
                spot.position + up * (-(_floor + Sink) * scale), rotation);
            root.transform.localScale = Vector3.one * scale;

            var view = Object.Instantiate(_prefab, root.transform, false);
            view.name = _prefab.name;
            if (spot.brown && _brown != null) Paint(view);
            NightReceivers.Adopt(view);
        }

        static void Paint(GameObject view)
        {
            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = Brown(slots[i]);
                renderer.sharedMaterials = slots;
            }
        }

        static Material Brown(Material source)
        {
            if (source == null) return null;
            if (Browns.TryGetValue(source, out Material cached) && cached != null) return cached;

            var copy = new Material(source) { name = source.name + " (brown)" };
            if (copy.HasProperty(BaseMapId)) copy.SetTexture(BaseMapId, _brown);
            if (copy.HasProperty(MainTexId)) copy.SetTexture(MainTexId, _brown);
            Browns[source] = copy;
            return copy;
        }
    }
}
