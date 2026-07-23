using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    /// <summary>
    /// The clear-noon atmosphere for the terrain levels — <see cref="MorningSky"/>'s counterpart,
    /// built the same way entirely at runtime (no material or profile assets). Where the morning
    /// is thick gold haze around a low sun off to the side, noon is clear air: a saturated blue
    /// zenith over a thin pale-blue distance haze (<see cref="HazeColor"/> — the fog colour, so
    /// the land still dissolves seamlessly into the sky), a small hard near-white sun pinned
    /// top-centre of the frame, a steep neutral key light for short noon shadows, and post FX
    /// tuned crisper and cooler than the morning's.
    /// </summary>
    public static class MiddaySky
    {
        // ---- Palette (neutral light / blue sky) ----
        // Fog AND the skybox horizon band: one value, so the fogged terrain edge and the sky
        // meet with no visible seam. Retune them together or the illusion breaks.
        public static readonly Color HazeColor = new Color(0.78f, 0.85f, 0.93f);
        static readonly Color ZenithColor = new Color(0.24f, 0.46f, 0.82f);   // deep noon blue
        static readonly Color SunColor = new Color(1.00f, 0.97f, 0.90f);      // near-white disc
        static readonly Color SunLightColor = new Color(1.00f, 0.96f, 0.90f); // neutral daylight
        static readonly Color AmbientColor = new Color(0.66f, 0.72f, 0.82f);  // blue-sky fill

        // Where the sun hangs on the SCREEN, as a viewport anchor (0..1 across, 0..1 up): the
        // camera never rotates, so a skybox direction is a fixed screen spot. x = 0.50 centres
        // it; y = 0.85 parks it at the top of the frame — high noon overhead, above the
        // dogfight. Unlike the low morning/evening suns it is not glued to the horizon; only
        // the horizon band tracks the map edge (SkyHorizon with anchorSun off).
        static readonly Vector2 SunViewportAnchor = new Vector2(0.50f, 0.85f);

        // As in the morning, the key light can't shine out of the visible sun (that would
        // backlight the planes into unreadable silhouettes, since the camera looks straight
        // down +Z), so it pours steeply into +Z from above and behind the camera: short noon
        // shadows, and with the sun dead ahead at the top of the frame the straight-on yaw
        // still feels plausible.
        static readonly Quaternion SunLightRotation = Quaternion.Euler(58f, 0f, 0f);
        const float SunLightIntensity = 1.35f;

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
                // Shader missing (stripped?): fall back to a flat haze so the horizon
                // at least still matches the fog.
                Debug.LogWarning("MiddaySky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = HazeColor;
                return;
            }

            var sky = new Material(shader) { name = "Midday Sky (runtime)" };
            sky.SetColor("_TopColor", ZenithColor);
            sky.SetColor("_HorizonColor", HazeColor);
            // Below the horizon it is HazeColor too: from a high camera the sky past the
            // terrain's far edge is visible, and any tint difference from the fog would draw
            // the map edge as a faint seam.
            sky.SetColor("_BottomColor", HazeColor);
            sky.SetFloat("_HorizonFalloff", 1.8f);   // narrow horizon band — the air is clear,
                                                     // so the blue owns most of the sky
            sky.SetColor("_SunColor", SunColor);
            // The world direction that puts the sun at SunViewportAnchor on this camera's
            // screen (rotation is identity here, so the ray's direction is already in world
            // space and only depends on the FOV/aspect actually in use).
            Vector3 sunDir = cam.ViewportPointToRay(
                new Vector3(SunViewportAnchor.x, SunViewportAnchor.y, 1f)).direction;
            sky.SetVector("_SunDirection", sunDir);
            sky.SetFloat("_SunFalloff", 800f);       // small hard ~4 degree disc — no mist to smear it
            sky.SetFloat("_SunIntensity", 1.6f);     // well past HDR white — noon glare for the bloom
            sky.SetFloat("_HaloFalloff", 14f);       // tight halo: clear air scatters far less light
            sky.SetFloat("_HaloIntensity", 0.22f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky);
        }

        /// <summary>Raises and whitens the scene's directional light into the noon key.</summary>
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
            profile.name = "Midday Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.5f);    // tight glare around the sun, not the morning's foggy spill

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(-4f);  // a breath cool — noon reads neutral next
                                                     // to the morning's gold

            var grade = profile.Add<ColorAdjustments>();
            grade.saturation.Override(10f);
            grade.contrast.Override(10f);    // hard overhead light: let the colours punch

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.15f);      // lighter than morning — noon frames feel open
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral); // rolls off the sun's HDR core

            var go = new GameObject("Midday Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
