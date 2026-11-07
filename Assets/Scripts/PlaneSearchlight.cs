using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneSearchlight : MonoBehaviour
    {
        const float RangeOfView = 0.6f;
        const float Intensity = 3f;
        public const float InnerAngle = 14f;
        public const float OuterAngle = 34f;
        public const float Falloff = 1.6f;
        public const float SelfShadowNear = 60f;
        const float SourceInsideFraction = 0.3f;
        const float MinSourceRadius = 4f;
        const int Priority = 100;
        const float BeamIntensity = 0.45f;
        const float BeamGlowSize = 5f;

        static readonly Color BeamColor = new Color(1f, 0.851f, 0.627f);

        NightLight2D _light;
        Searchlight _beam;
        bool _on;

        public bool IsOn => _on && isActiveAndEnabled;

        public static PlaneSearchlight Mount(GameObject body, Vector3 noseLocal, Daytime daytime,
            bool on = true)
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
            light.shadowLead = true;
            light.shadowNear = SelfShadowNear;

            Searchlight beam = Searchlight.Mount(go.transform, Vector3.zero);
            if (beam != null)
            {
                beam.innerAngle = InnerAngle;
                beam.outerAngle = OuterAngle;
                beam.length = light.range;
                beam.sourceRadius = light.sourceRadius;
                beam.glowSize = BeamGlowSize;
                beam.intensity = BeamIntensity;
                light.beam = beam;
            }

            var searchlight = go.AddComponent<PlaneSearchlight>();
            searchlight._light = light;
            searchlight._beam = beam;
            searchlight.SetOn(on);
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

        void LateUpdate()
        {
            if (_beam == null || _light == null) return;
            Vector2 dir = _light.PlaneDirection;
            _beam.worldDirection = new Vector3(dir.x, dir.y, 0f);
        }

        void SetOn(bool on)
        {
            _on = on;
            if (_light != null) _light.enabled = on;
            if (_beam != null) _beam.enabled = on;
        }
    }
}
