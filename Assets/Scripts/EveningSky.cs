using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    /// <summary>
    /// The blooming-evening atmosphere for the terrain levels: golden-hour light with the sun
    /// low by the horizon, warm yellow-orange air, built entirely at runtime like
    /// <see cref="MorningSky"/> and <see cref="MiddaySky"/>. Design notes: docs/atmospheres.md.
    /// </summary>
    public static class EveningSky
    {
        // Fog AND the skybox horizon band: one value, so the fogged terrain edge and the sky
        // meet with no visible seam. Retune them together or the illusion breaks.
        public static readonly Color HazeColor = new Color(0.95f, 0.72f, 0.50f);
        static readonly Color ZenithColor = new Color(0.38f, 0.34f, 0.52f);
        static readonly Color SunColor = new Color(1.00f, 0.62f, 0.30f);
        static readonly Color SunLightColor = new Color(1.00f, 0.72f, 0.45f);
        static readonly Color AmbientColor = new Color(0.60f, 0.52f, 0.58f);

        // Sun column on screen (left of frame — the setting sun is this sky's centrepiece) and
        // how far above the map-edge horizon the disc rides; SkyHorizon re-anchors it every
        // frame, so with the big soft disc the lower rim sits in the haze — a sun mid-set.
        const float SunViewportX = 0.22f;
        const float SunHorizonLift = 0.04f;

        static readonly Quaternion SunLightRotation = Quaternion.Euler(16f, 20f, 0f);
        const float SunLightIntensity = 1.15f;

        /// <summary>Applies the whole look to the scene rendered by <paramref name="cam"/>.</summary>
        public static void Apply(Camera cam, Weather weather)
        {
            BuildSkybox(cam);
            TuneSunLight();
            BuildPostFx(cam);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;
        }

        static void BuildSkybox(Camera cam)
        {
            var shader = Shader.Find("Custom/GradientSkybox");
            if (shader == null)
            {
                Debug.LogWarning("EveningSky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = HazeColor;
                return;
            }

            var sky = new Material(shader) { name = "Evening Sky (runtime)" };
            sky.SetColor("_TopColor", ZenithColor);
            sky.SetColor("_HorizonColor", HazeColor);
            sky.SetColor("_BottomColor", HazeColor);
            sky.SetFloat("_HorizonFalloff", 3.5f);
            sky.SetColor("_SunColor", SunColor);
            sky.SetFloat("_SunFalloff", 150f);
            sky.SetFloat("_SunIntensity", 1.8f);
            sky.SetFloat("_HaloFalloff", 4.5f);
            sky.SetFloat("_HaloIntensity", 0.6f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky, SunViewportX, SunHorizonLift, anchorSun: true);
        }

        static void TuneSunLight()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = SunLightColor;
                light.intensity = SunLightIntensity;
                light.transform.rotation = SunLightRotation;
                RenderSettings.sun = light;
                break;
            }
        }

        static void BuildPostFx(Camera cam)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Evening Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.75f);

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(28f);

            var grade = profile.Add<ColorAdjustments>();
            grade.saturation.Override(14f);
            grade.contrast.Override(4f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var go = new GameObject("Evening Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
