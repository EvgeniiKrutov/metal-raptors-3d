using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneSearchlight : MonoBehaviour
    {
        const float RangeOfView = 0.6f;
        const float Intensity = 3f;
        const float InnerAngle = 14f;
        const float OuterAngle = 34f;
        const float Falloff = 1.6f;
        const float SourceInsideFraction = 0.3f;
        const float MinSourceRadius = 4f;
        const int Priority = 100;

        static readonly Color BeamColor = new Color(1f, 0.851f, 0.627f);

        NightLight2D _light;
        bool _on;

        public bool IsOn => _on && isActiveAndEnabled;

        public static PlaneSearchlight Mount(GameObject body, Vector3 noseLocal, Daytime daytime)
        {
            if (daytime != Daytime.Night) return null;

            var go = new GameObject("Searchlight");
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = noseLocal;

            var light = go.AddComponent<NightLight2D>();
            light.type = NightLightType.Cone;
            light.color = BeamColor;
            light.intensity = Intensity;
            light.range = NightLights.ViewWidth * RangeOfView;
            light.innerAngle = InnerAngle;
            light.outerAngle = OuterAngle;
            light.falloff = Falloff;
            light.sourceRadius = Mathf.Max(MinSourceRadius, Mathf.Abs(noseLocal.x) * SourceInsideFraction);
            light.localDirection = Vector3.right;
            light.priority = Priority;
            light.castShadows = true;

            var searchlight = go.AddComponent<PlaneSearchlight>();
            searchlight._light = light;
            searchlight.SetOn(true);
            return searchlight;
        }

        public void Toggle()
        {
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return;
            SetOn(!_on);
        }

        void Update()
        {
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) SetOn(!_on);
        }

        void SetOn(bool on)
        {
            _on = on;
            if (_light != null) _light.enabled = on;
        }
    }
}
