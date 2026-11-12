using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public static class VosgesSpruce
    {
        public const int Models = 7;

        const string ForeResource = "objects/trees/spruce_trees/spruce_fore_";
        const string BackResource = "objects/trees/spruce_trees/spruce_back_";
        const float Sink = 0.4f;

        static readonly GameObject[] Fore = new GameObject[Models];
        static readonly GameObject[] Back = new GameObject[Models];
        static bool _measured;

        static void Load()
        {
            if (_measured) return;
            _measured = true;

            for (int i = 0; i < Models; i++)
            {
                Fore[i] = Resources.Load<GameObject>(ForeResource + i);
                Back[i] = Resources.Load<GameObject>(BackResource + i);
                if (Fore[i] == null) Debug.LogError($"VosgesSpruce: {ForeResource}{i} not found in Resources.");
                if (Back[i] == null) Debug.LogError($"VosgesSpruce: {BackResource}{i} not found in Resources.");
            }
        }

        public static void Place(Transform parent, VosgesLayout.TreeSpot spot)
        {
            Load();
            int model = Mathf.Clamp(spot.model, 0, Models - 1);
            GameObject prefab = spot.fore ? Fore[model] : Back[model];
            if (prefab == null) prefab = spot.fore ? Back[model] : Fore[model];
            if (prefab == null) return;

            float scale = PlaneModelConfig.UnitsPerMeter * spot.scale;
            var root = new GameObject("Spruce");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(spot.position + Vector3.down * (Sink * scale),
                Quaternion.Euler(0f, spot.yaw, 0f));
            root.transform.localScale = Vector3.one * scale;

            var view = Object.Instantiate(prefab, root.transform, false);
            view.name = prefab.name;
            NightReceivers.Adopt(view);
            foreach (var r in view.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;
        }
    }
}
