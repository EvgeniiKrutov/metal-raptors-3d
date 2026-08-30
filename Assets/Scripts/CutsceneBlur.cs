using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class CutsceneBlur
    {
        public const float FadeSec = 0.35f;

        const float DefaultFocus = 420f;
        const float MaxRadius = 1.5f;
        const float Sharp = 100000f;
        const int Priority = 500;

        static Volume _volume;
        static DepthOfField _field;
        static float _focus = DefaultFocus;
        static float _amount;

        public static void Focus(float distance)
        {
            if (distance > 1f) _focus = distance;
        }

        public static void Set(float amount)
        {
            _amount = Mathf.Clamp01(amount);

            if (_amount <= 0f)
            {
                if (_volume != null) _volume.enabled = false;
                return;
            }

            Ensure();
            if (_field == null) return;

            _volume.enabled = true;
            _field.gaussianEnd.Override(_focus / _amount);
        }

        public static void Clear() => Set(0f);

        static void Ensure()
        {
            if (_volume != null && _field != null) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Cutscene Blur (runtime)";

            _field = profile.Add<DepthOfField>();
            _field.mode.Override(DepthOfFieldMode.Gaussian);
            _field.gaussianStart.Override(0f);
            _field.gaussianEnd.Override(Sharp);
            _field.gaussianMaxRadius.Override(MaxRadius);
            _field.highQualitySampling.Override(!GraphicsOptions.Mobile);

            var go = new GameObject("Cutscene Blur");
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = Priority;
            _volume.profile = profile;
            _volume.enabled = false;

            Camera cam = Camera.main;
            if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
