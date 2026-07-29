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
        // The cloud layer's albedo: dark and violet, so night clouds read as shapes the moon
        // catches the edge of rather than the daytime layer turned down. See docs/clouds.md.
        public static readonly Color CloudColor = new Color(0.40f, 0.42f, 0.58f);
        static readonly Color ZenithColor = new Color(0.03f, 0.03f, 0.08f);
        static readonly Color MoonColor = new Color(0.85f, 0.90f, 1.00f);
        static readonly Color MoonLightColor = new Color(0.60f, 0.68f, 0.90f);
        // Gradient ambient: a touch of moonlit sky above fading to near-black ground — a third
        // of the daytime skies' fill, so the scene reads as night and the planes stay legible.
        static readonly Color AmbientSkyColor = new Color(0.21f, 0.21f, 0.39f);
        static readonly Color AmbientEquatorColor = new Color(0.23f, 0.21f, 0.35f);
        static readonly Color AmbientGroundColor = new Color(0.12f, 0.10f, 0.17f);
        static readonly Color ColorFilter = new Color(0.65f, 0.56f, 0.85f);

        // Moon column on screen and how far above the map-edge horizon the disc rides:
        // well up the sky — it is the middle of the night, not a moonrise.
        const float MoonViewportX = 0.74f;
        const float MoonHorizonLift = 0.30f;

        // Moonbeams: the faintest shafts of the four, cold — enough to say the air is there,
        // never enough to light anything.
        static readonly Color RayColor = new Color(0.72f, 0.80f, 1.00f);
        const float RayIntensity = 0.30f;
        const float RayDensity = 0.75f;
        const float RayFalloff = 1.5f;

        static readonly Quaternion MoonLightRotation = Quaternion.Euler(50f, -14f, 0f);
        const float MoonLightIntensity = 1f;

        /// <summary>Applies the whole look to the scene rendered by <paramref name="cam"/>.</summary>
        public static void Apply(Camera cam, Weather weather)
        {
            BuildSkybox(cam);
            TuneMoonLight();
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
            sky.SetFloat("_SunIntensity", 2.5f);   // past HDR white, but a moon, not a sun
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
            GodRays.Attach(cam, sky, RayColor, RayIntensity,
                density: RayDensity, radialFalloff: RayFalloff);
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
                light.shadowNormalBias = 0.5f;
                RenderSettings.sun = light;
                break;
            }
        }

        static void BuildPostFx(Camera cam)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Night Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.85f);   // the moon core and the beams, little else
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.7f);

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(-22f);

            var grade = profile.Add<ColorAdjustments>();
            grade.postExposure.Override(2f);  // the murkiest sky gets the biggest lift
            grade.colorFilter.Override(ColorFilter);
            grade.saturation.Override(-12f);
            grade.contrast.Override(4f);

            // Night inverts the warm-highlight rule: nothing here is lit by a sun, so the
            // highlights stay silver-blue and only the deepest shadows go violet.
            var splitToning = profile.Add<SplitToning>();
            splitToning.shadows.Override(new Color(0.38f, 0.24f, 0.58f));
            splitToning.highlights.Override(new Color(0.62f, 0.72f, 1.00f));
            splitToning.balance.Override(10f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.27f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var go = new GameObject("Night Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
