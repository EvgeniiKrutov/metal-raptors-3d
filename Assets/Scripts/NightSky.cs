using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    /// <summary>
    /// The calm middle-of-night atmosphere for the terrain levels: a crisp solid moon disc
    /// riding high over the horizon under a field of stars, cold silver-blue key light far
    /// weaker than any sun, dark-violet air, built entirely at runtime like the other skies.
    /// Design notes: docs/atmospheres.md.
    /// </summary>
    public static class NightSky
    {
        // Fog AND the skybox horizon band: one value, so the fogged terrain edge and the sky
        // meet with no visible seam. Retune them together or the illusion breaks.
        public static readonly Color HazeColor = new Color(0.16f, 0.13f, 0.25f);
        static readonly Color ZenithColor = new Color(0.03f, 0.03f, 0.08f);
        static readonly Color MoonColor = new Color(0.85f, 0.90f, 1.00f);
        static readonly Color MoonLightColor = new Color(0.60f, 0.68f, 0.90f);
        static readonly Color AmbientColor = new Color(0.20f, 0.18f, 0.30f);
        static readonly Color ColorFilter = new Color(0.65f, 0.56f, 0.85f);

        // Moon column on screen and how far above the map-edge horizon the disc rides:
        // well up the sky — it is the middle of the night, not a moonrise.
        const float MoonViewportX = 0.74f;
        const float MoonHorizonLift = 0.30f;

        static readonly Quaternion MoonLightRotation = Quaternion.Euler(50f, -14f, 0f);
        const float MoonLightIntensity = 0.5f;

        /// <summary>Applies the whole look to the scene rendered by <paramref name="cam"/>.</summary>
        public static void Apply(Camera cam, Weather weather)
        {
            BuildSkybox(cam);
            TuneMoonLight();
            BuildPostFx(cam);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;
        }

        static void BuildSkybox(Camera cam)
        {
            var shader = Shader.Find("Custom/GradientSkybox");
            if (shader == null)
            {
                Debug.LogWarning("NightSky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = HazeColor;
                return;
            }

            var sky = new Material(shader) { name = "Night Sky (runtime)" };
            sky.SetColor("_TopColor", ZenithColor);
            sky.SetColor("_HorizonColor", HazeColor);
            sky.SetColor("_BottomColor", HazeColor);
            sky.SetFloat("_HorizonFalloff", 2.2f);
            sky.SetColor("_SunColor", MoonColor);
            sky.SetFloat("_SunIntensity", 1.2f);
            sky.SetFloat("_DiscRadius", 1.8f);
            sky.SetFloat("_DiscEdge", 0.12f);
            sky.SetFloat("_MariaIntensity", 0.25f);
            sky.SetFloat("_HaloFalloff", 8f);
            sky.SetFloat("_HaloIntensity", 0.22f);
            sky.SetFloat("_StarIntensity", 1.4f);
            sky.SetFloat("_StarScale", 80f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky, MoonViewportX, MoonHorizonLift, anchorSun: true);
        }

        // The key light cannot shine out of the visible moon (that would backlight the planes
        // into silhouettes, since the camera looks straight down +Z), so it falls steeply into
        // +Z from the moon's side of the sky.
        static void TuneMoonLight()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = MoonLightColor;
                light.intensity = MoonLightIntensity;
                light.transform.rotation = MoonLightRotation;
                RenderSettings.sun = light;
                break;
            }
        }

        static void BuildPostFx(Camera cam)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Night Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.75f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.6f);

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(-22f);

            var grade = profile.Add<ColorAdjustments>();
            grade.colorFilter.Override(ColorFilter);
            grade.saturation.Override(-12f);
            grade.contrast.Override(8f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var go = new GameObject("Night Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
