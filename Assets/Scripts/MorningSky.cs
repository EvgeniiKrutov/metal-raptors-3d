using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    /// <summary>
    /// The foggy-morning-sun atmosphere for the terrain levels, built entirely at runtime
    /// (no material or profile assets). Four ingredients that only work together:
    /// a gradient skybox with a low glowing sun off toward the right edge of the view (out of
    /// the player's sightline), linear fog whose colour is exactly the skybox's horizon band so the
    /// land dissolves seamlessly into the sky (<see cref="HazeColor"/> is that one shared
    /// value — <see cref="ProceduralTerrain"/> reads it for the fog), a warm low sun light
    /// against a cool ambient so shadows lean blue, and restrained URP post FX (bloom makes
    /// the HDR sun core glow, warm white balance, subtle vignette, neutral tonemapping).
    /// </summary>
    public static class MorningSky
    {
        // ---- Palette (warm light / cool shade) ----
        // Fog AND the skybox horizon band: one value, so the fogged terrain edge and the sky
        // meet with no visible seam. Retune them together or the illusion breaks.
        public static readonly Color HazeColor = new Color(0.90f, 0.84f, 0.75f);
        static readonly Color ZenithColor = new Color(0.52f, 0.62f, 0.76f);  // muted morning blue
        static readonly Color SunColor = new Color(1.00f, 0.88f, 0.66f);     // pale gold disc + halo
        static readonly Color SunLightColor = new Color(1.00f, 0.84f, 0.64f);// amber key light
        static readonly Color AmbientColor = new Color(0.62f, 0.66f, 0.74f); // cool fill -> blue-ish shadows

        // Sun column on screen (0..1 across; right edge, out of the dogfight's sightline) and
        // how far above the map-edge horizon the disc rides, as a viewport fraction. SkyHorizon
        // re-anchors the sun there every frame so it dawns at the land's visible edge.
        const float SunViewportX = 0.80f;
        const float SunHorizonLift = 0.08f;

        // The key light can't actually come from the visible sun (that would backlight the
        // planes into unreadable silhouettes, since the camera looks straight down +Z), so it
        // shines into +Z from over the camera's right shoulder — low enough for long morning
        // shadows, on the sun's side of the sky so the direction still feels plausible.
        static readonly Quaternion SunLightRotation = Quaternion.Euler(30f, -17f, 0f);
        const float SunLightIntensity = 1.25f;

        /// <summary>
        /// Applies the whole look to the scene rendered by <paramref name="cam"/>.
        /// <paramref name="weather"/> is the seam where weather will modulate this atmosphere
        /// (thicker haze, dimmer sun, harder wind on the grass); <see cref="Weather.Calm"/> is
        /// the identity and changes nothing.
        /// </summary>
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
                // Shader missing (stripped?): fall back to the old flat haze so the horizon
                // at least still matches the fog.
                Debug.LogWarning("MorningSky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = HazeColor;
                return;
            }

            var sky = new Material(shader) { name = "Morning Sky (runtime)" };
            sky.SetColor("_TopColor", ZenithColor);
            sky.SetColor("_HorizonColor", HazeColor);
            // Below the horizon it is HazeColor too: from a high camera the sky past the
            // terrain's far edge is visible, and any tint difference from the fog would draw
            // the map edge as a faint seam.
            sky.SetColor("_BottomColor", HazeColor);
            sky.SetFloat("_HorizonFalloff", 2.5f);   // wide horizon band — the air is thick
            sky.SetColor("_SunColor", SunColor);
            sky.SetFloat("_SunFalloff", 300f);       // soft ~6 degree core, not a hard disc
            sky.SetFloat("_SunIntensity", 1.4f);     // just past HDR white — glows without glare
            sky.SetFloat("_HaloFalloff", 7f);        // broad scattered-light patch around it
            sky.SetFloat("_HaloIntensity", 0.35f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            // Horizon band + sun glued to the map's fogged far edge (see docs/atmospheres.md).
            SkyHorizon.Attach(cam, sky, SunViewportX, SunHorizonLift, anchorSun: true);
        }

        /// <summary>Warms and lowers the scene's directional light into the morning key.</summary>
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

        /// <summary>
        /// A global URP Volume, created in code like the rest of the scene. Kept subtle: the
        /// look comes from the sky and fog, post just seasons it.
        /// </summary>
        static void BuildPostFx(Camera cam)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Morning Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.7f);    // wide soft spill — foggy glow, not neon edges

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(12f);  // push the frame gently toward gold

            var grade = profile.Add<ColorAdjustments>();
            grade.saturation.Override(8f);
            grade.contrast.Override(6f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral); // rolls off the sun's HDR core

            var go = new GameObject("Morning Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
