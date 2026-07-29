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
        // The cloud layer's albedo — near-white, with the blue of the air left to the emissive
        // lift CloudSystem takes from HazeColor. See docs/clouds.md.
        public static readonly Color CloudColor = new Color(1.00f, 1.00f, 1.00f);
        static readonly Color ZenithColor = new Color(0.24f, 0.46f, 0.82f);   // deep noon blue
        static readonly Color SunColor = new Color(1.00f, 0.97f, 0.90f);      // near-white disc
        static readonly Color SunLightColor = new Color(1.00f, 0.96f, 0.90f); // neutral daylight
        // Gradient ambient: the blue of the noon sky above, dry warm ground below.
        static readonly Color AmbientSkyColor = new Color(0.61f, 0.75f, 1.00f);
        static readonly Color AmbientEquatorColor = new Color(0.80f, 0.84f, 0.88f);
        static readonly Color AmbientGroundColor = new Color(0.47f, 0.41f, 0.34f);

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
        // Light shafts: barely there. Clear noon air scatters little, and an overhead sun throws
        // no rake across the play plane — a short fan close around the disc, no more.
        static readonly Color RayColor = new Color(1.00f, 0.97f, 0.90f);
        const float RayIntensity = 0.45f;
        const float RayDensity = 0.65f;
        const float RayFalloff = 1.8f;

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
            sky.SetFloat("_SunIntensity", 6f);       // far past HDR white — noon glare for the bloom
            sky.SetFloat("_HaloFalloff", 14f);       // tight halo: clear air scatters far less light
            sky.SetFloat("_HaloIntensity", 0.22f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky);
            GodRays.Attach(cam, sky, RayColor, RayIntensity,
                density: RayDensity, radialFalloff: RayFalloff);
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
                light.shadowNormalBias = 0.5f;
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
            bloom.threshold.Override(1.0f);  // clear air: only the disc itself is over the line
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.6f);    // tight glare around the sun, not the morning's foggy spill

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(-4f);  // a breath cool — noon reads neutral next
                                                     // to the morning's gold

            var grade = profile.Add<ColorAdjustments>();
            grade.postExposure.Override(0.35f);
            grade.saturation.Override(10f);
            grade.contrast.Override(10f);    // hard overhead light: let the colours punch

            var splitToning = profile.Add<SplitToning>();
            splitToning.shadows.Override(new Color(0.32f, 0.34f, 0.62f));
            splitToning.highlights.Override(new Color(1.00f, 0.82f, 0.55f));
            splitToning.balance.Override(-25f);      // the least of the four — noon reads neutral

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.15f);      // lighter than morning — noon frames feel open
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES); // filmic roll-off on the HDR core

            var go = new GameObject("Midday Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
