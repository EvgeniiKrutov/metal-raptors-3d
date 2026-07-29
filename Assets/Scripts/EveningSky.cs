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
        public static readonly Color HazeColor = new Color(0.82f, 0.63f, 0.48f);
        // The cloud layer's albedo, so the drifting blobs read as this sky's own clouds rather
        // than white paper on an orange wall. CloudSystem reads it; see docs/clouds.md.
        public static readonly Color CloudColor = new Color(0.92f, 0.71f, 0.56f);
        static readonly Color ZenithColor = new Color(0.38f, 0.34f, 0.52f);
        static readonly Color SunColor = new Color(1.00f, 0.62f, 0.30f);
        static readonly Color SunLightColor = new Color(1.00f, 0.72f, 0.45f);
        // Gradient ambient: a magenta-violet sky over warm brown ground, so shaded sides lean
        // dusk-purple and bounce warm from below instead of going flat grey.
        static readonly Color AmbientSkyColor = new Color(0.58f, 0.38f, 0.66f);
        static readonly Color AmbientEquatorColor = new Color(0.80f, 0.54f, 0.58f);
        static readonly Color AmbientGroundColor = new Color(0.42f, 0.27f, 0.20f);

        // Sun column on screen (left of frame — the setting sun is this sky's centrepiece) and
        // how far above the map-edge horizon the disc rides; SkyHorizon re-anchors it every
        // frame, so with the big soft disc the lower rim sits in the haze — a sun mid-set.
        const float SunViewportX = 0.22f;
        const float SunHorizonLift = 0.04f;

        // Light shafts: the longest and strongest of the four skies — dusk is where the low sun
        // rakes right across the play plane, and the shafts reach furthest across the frame.
        static readonly Color RayColor = new Color(1.00f, 0.70f, 0.42f);
        const float RayIntensity = 0.8f;
        const float RayDensity = 0.85f;
        const float RayFalloff = 1.0f;

        static readonly Quaternion SunLightRotation = Quaternion.Euler(16f, 20f, 0f);
        const float SunLightIntensity = 1.05f;

        /// <summary>Applies the whole look to the scene rendered by <paramref name="cam"/>.</summary>
        public static void Apply(Camera cam, Weather weather)
        {
            BuildSkybox(cam);
            TuneSunLight();
            BuildPostFx(cam);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSkyColor;
            RenderSettings.ambientEquatorColor = AmbientEquatorColor;
            RenderSettings.ambientGroundColor = AmbientGroundColor;
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
            sky.SetFloat("_SunIntensity", 6f);   // past HDR white, but the disc keeps its edge
            sky.SetFloat("_HaloFalloff", 4.5f);
            sky.SetFloat("_HaloIntensity", 0.4f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky, SunViewportX, SunHorizonLift, anchorSun: true);
            GodRays.Attach(cam, sky, RayColor, RayIntensity,
                density: RayDensity, radialFalloff: RayFalloff);
        }

        static void TuneSunLight()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = SunLightColor;
                light.intensity = SunLightIntensity;
                light.transform.rotation = SunLightRotation;
                light.shadowNormalBias = 0.5f;   // grazing dusk light: acne without the extra bias
                RenderSettings.sun = light;
                break;
            }
        }

        static void BuildPostFx(Camera cam)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Evening Post FX (runtime)";

            // Bloom does most of the softness in the shafts: the pass runs before post, so this
            // blooms the rays themselves along with the HDR disc.
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.7f);

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(22f);

            var grade = profile.Add<ColorAdjustments>();
            grade.postExposure.Override(0.10f);
            grade.saturation.Override(8f);
            grade.contrast.Override(4f);

            var splitToning = profile.Add<SplitToning>();
            splitToning.shadows.Override(new Color(0.42f, 0.22f, 0.55f));
            splitToning.highlights.Override(new Color(1.00f, 0.62f, 0.24f));
            splitToning.balance.Override(-10f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var go = new GameObject("Evening Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
