using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class CoastSky
    {
        public const float FogEnd = 1500f;
        public const float NightFogEnd = 1250f;

        class Palette
        {
            public Color haze, zenith, cloud;
            public Color disc, keyLight;
            public Color ambientSky, ambientEquator, ambientGround;
            public Color sea;
            public Color rayColor, colorFilter, shadowTone, highlightTone;

            public float horizonFalloff, discFalloff, discIntensity, haloFalloff, haloIntensity;
            public float discRadius, mariaIntensity, starIntensity;
            public float discViewportX, discLift;
            public bool anchorDisc;

            public Quaternion lightRotation;
            public float lightIntensity;
            public float fogStartOffset, fogEnd;
            public float rayIntensity, rayDensity, rayFalloff;

            public float bloomThreshold, bloomIntensity;
            public float temperature, postExposure, saturation, contrast;
            public float splitBalance, vignette, cloudGlow;
        }

        static readonly Palette[] Palettes =
        {
            new Palette
            {
                haze = new Color(0.88f, 0.90f, 0.89f),
                zenith = new Color(0.55f, 0.66f, 0.76f),
                cloud = new Color(0.97f, 0.98f, 0.96f),
                disc = new Color(1.00f, 0.95f, 0.86f),
                keyLight = new Color(0.97f, 0.95f, 0.90f),
                ambientSky = new Color(0.76f, 0.84f, 0.92f),
                ambientEquator = new Color(0.92f, 0.95f, 0.94f),
                ambientGround = new Color(0.42f, 0.42f, 0.38f),
                sea = new Color(0.34f, 0.44f, 0.43f),
                rayColor = new Color(0.95f, 0.95f, 0.90f),
                colorFilter = Color.white,
                shadowTone = new Color(0.30f, 0.36f, 0.48f),
                highlightTone = new Color(0.92f, 0.90f, 0.78f),
                horizonFalloff = 2.4f,
                discFalloff = 260f, discIntensity = 2.6f,
                haloFalloff = 6f, haloIntensity = 0.30f,
                discViewportX = 0.78f, discLift = 0.09f, anchorDisc = true,
                lightRotation = Quaternion.Euler(28f, -14f, 0f),
                lightIntensity = 1.35f,
                fogStartOffset = 330f, fogEnd = FogEnd,
                rayIntensity = 0.45f, rayDensity = 0.80f, rayFalloff = 1.2f,
                bloomThreshold = 0.95f, bloomIntensity = 0.9f,
                temperature = -6f, postExposure = 0.62f, saturation = -4f, contrast = 4f,
                splitBalance = -10f, vignette = 0.12f, cloudGlow = 0.30f,
            },
            new Palette
            {
                haze = new Color(0.89f, 0.92f, 0.91f),
                zenith = new Color(0.54f, 0.67f, 0.76f),
                cloud = new Color(0.98f, 0.99f, 0.99f),
                disc = new Color(1.00f, 1.00f, 0.96f),
                keyLight = new Color(0.97f, 0.98f, 0.96f),
                ambientSky = new Color(0.80f, 0.90f, 0.96f),
                ambientEquator = new Color(0.95f, 0.98f, 0.97f),
                ambientGround = new Color(0.48f, 0.48f, 0.44f),
                sea = new Color(0.37f, 0.48f, 0.47f),
                rayColor = new Color(0.95f, 0.96f, 0.94f),
                colorFilter = Color.white,
                shadowTone = new Color(0.30f, 0.36f, 0.50f),
                highlightTone = new Color(0.94f, 0.93f, 0.84f),
                horizonFalloff = 1.9f,
                discFalloff = 500f, discIntensity = 3.2f,
                haloFalloff = 12f, haloIntensity = 0.20f,
                discViewportX = 0.50f, discLift = 0.82f, anchorDisc = false,
                lightRotation = Quaternion.Euler(52f, 4f, 0f),
                lightIntensity = 1.50f,
                fogStartOffset = 620f, fogEnd = FogEnd,
                rayIntensity = 0.30f, rayDensity = 0.65f, rayFalloff = 1.8f,
                bloomThreshold = 1.05f, bloomIntensity = 0.75f,
                temperature = -8f, postExposure = 0.62f, saturation = -2f, contrast = 5f,
                splitBalance = -18f, vignette = 0.10f, cloudGlow = 0.22f,
            },
            new Palette
            {
                haze = new Color(0.92f, 0.82f, 0.72f),
                zenith = new Color(0.34f, 0.44f, 0.60f),
                cloud = new Color(0.96f, 0.86f, 0.78f),
                disc = new Color(1.00f, 0.84f, 0.60f),
                keyLight = new Color(1.00f, 0.86f, 0.68f),
                ambientSky = new Color(0.54f, 0.60f, 0.78f),
                ambientEquator = new Color(0.90f, 0.80f, 0.74f),
                ambientGround = new Color(0.40f, 0.37f, 0.34f),
                sea = new Color(0.30f, 0.37f, 0.40f),
                rayColor = new Color(1.00f, 0.80f, 0.56f),
                colorFilter = Color.white,
                shadowTone = new Color(0.26f, 0.30f, 0.52f),
                highlightTone = new Color(1.00f, 0.74f, 0.44f),
                horizonFalloff = 2.6f,
                discFalloff = 220f, discIntensity = 4.0f,
                haloFalloff = 5f, haloIntensity = 0.42f,
                discViewportX = 0.82f, discLift = 0.03f, anchorDisc = true,
                lightRotation = Quaternion.Euler(18f, -22f, 0f),
                lightIntensity = 1.30f,
                fogStartOffset = 520f, fogEnd = FogEnd,
                rayIntensity = 0.65f, rayDensity = 0.85f, rayFalloff = 1.1f,
                bloomThreshold = 0.85f, bloomIntensity = 1.2f,
                temperature = 6f, postExposure = 0.66f, saturation = 0f, contrast = 4f,
                splitBalance = -20f, vignette = 0.14f, cloudGlow = 0.40f,
            },
            new Palette
            {
                haze = new Color(0.20f, 0.25f, 0.32f),
                zenith = new Color(0.05f, 0.09f, 0.16f),
                cloud = new Color(0.46f, 0.52f, 0.64f),
                disc = new Color(0.90f, 0.94f, 1.00f),
                keyLight = new Color(0.66f, 0.76f, 0.96f),
                ambientSky = new Color(0.28f, 0.33f, 0.48f),
                ambientEquator = new Color(0.30f, 0.34f, 0.44f),
                ambientGround = new Color(0.18f, 0.20f, 0.24f),
                sea = new Color(0.14f, 0.19f, 0.23f),
                rayColor = new Color(0.72f, 0.82f, 1.00f),
                colorFilter = new Color(0.68f, 0.74f, 0.92f),
                shadowTone = new Color(0.26f, 0.32f, 0.56f),
                highlightTone = new Color(0.62f, 0.74f, 1.00f),
                horizonFalloff = 0.65f,
                discFalloff = 60f, discIntensity = 2.5f,
                haloFalloff = 12f, haloIntensity = 0.20f,
                discRadius = 1.8f, mariaIntensity = 0.25f, starIntensity = 1.4f,
                discViewportX = 0.72f, discLift = 0.28f, anchorDisc = true,
                lightRotation = Quaternion.Euler(48f, -12f, 0f),
                lightIntensity = 1.15f,
                fogStartOffset = 420f, fogEnd = NightFogEnd,
                rayIntensity = 0.25f, rayDensity = 0.75f, rayFalloff = 1.5f,
                bloomThreshold = 0.85f, bloomIntensity = 1.1f,
                temperature = -20f, postExposure = 2.1f, saturation = -10f, contrast = 3f,
                splitBalance = 10f, vignette = 0.16f, cloudGlow = 0.55f,
            },
        };

        static Palette For(Daytime daytime) =>
            Palettes[Mathf.Clamp((int)daytime, 0, Palettes.Length - 1)];

        public static Color HazeColor(Daytime daytime) => For(daytime).haze;

        public static Color CloudColor(Daytime daytime) => For(daytime).cloud;

        public static Color CloudGlow(Daytime daytime)
        {
            Palette p = For(daytime);
            return p.haze * p.cloudGlow;
        }

        public static Color SeaColor(Daytime daytime) => For(daytime).sea;

        public static void ApplyFog(Daytime daytime, float cameraDistance, float playPlaneZ)
        {
            Palette p = For(daytime);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.haze;
            RenderSettings.fogStartDistance = cameraDistance + p.fogStartOffset;
            RenderSettings.fogEndDistance = p.fogEnd;
        }

        public static void Apply(Camera cam, Daytime daytime, Weather weather)
        {
            Palette p = For(daytime);

            BuildSkybox(cam, p);
            TuneKeyLight(p);
            BuildPostFx(cam, p, daytime);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
        }

        static void BuildSkybox(Camera cam, Palette p)
        {
            var shader = Shader.Find("Custom/GradientSkybox");
            if (shader == null)
            {
                Debug.LogWarning("CoastSky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = p.haze;
                return;
            }

            var sky = new Material(shader) { name = "Coast Sky (runtime)" };
            sky.SetColor("_TopColor", p.zenith);
            sky.SetColor("_HorizonColor", p.haze);
            sky.SetColor("_BottomColor", p.haze);
            sky.SetFloat("_HorizonFalloff", p.horizonFalloff);
            sky.SetColor("_SunColor", p.disc);
            sky.SetFloat("_SunFalloff", p.discFalloff);
            sky.SetFloat("_SunIntensity", p.discIntensity);
            sky.SetFloat("_HaloFalloff", p.haloFalloff);
            sky.SetFloat("_HaloIntensity", p.haloIntensity);
            sky.SetFloat("_DiscRadius", p.discRadius);
            sky.SetFloat("_MariaIntensity", p.mariaIntensity);
            sky.SetFloat("_StarIntensity", p.starIntensity);
            sky.SetFloat("_StarScale", 80f);
            sky.SetFloat("_Exposure", 1f);

            if (!p.anchorDisc)
                sky.SetVector("_SunDirection", cam.ViewportPointToRay(
                    new Vector3(p.discViewportX, p.discLift, 1f)).direction);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.AtEyeLevel(cam, sky, p.discViewportX, p.discLift, p.anchorDisc);
            GodRays.Attach(cam, sky, p.rayColor, p.rayIntensity,
                density: p.rayDensity, radialFalloff: p.rayFalloff);
            AerialHaze.Attach(cam, sky);
        }

        static void TuneKeyLight(Palette p)
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = p.keyLight;
                light.intensity = p.lightIntensity;
                light.transform.rotation = p.lightRotation;
                light.shadowNormalBias = 0.5f;
                RenderSettings.sun = light;
                break;
            }
        }

        static void BuildPostFx(Camera cam, Palette p, Daytime daytime)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Coast Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(p.bloomThreshold);
            bloom.intensity.Override(p.bloomIntensity);
            bloom.scatter.Override(0.7f);

            GraphicsOptions.TrackBloom(bloom);

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(p.temperature);

            var grade = profile.Add<ColorAdjustments>();
            grade.postExposure.Override(p.postExposure);
            grade.colorFilter.Override(p.colorFilter);
            grade.saturation.Override(p.saturation);
            grade.contrast.Override(p.contrast);

            var splitToning = profile.Add<SplitToning>();
            splitToning.shadows.Override(p.shadowTone);
            splitToning.highlights.Override(p.highlightTone);
            splitToning.balance.Override(p.splitBalance);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(p.vignette);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var go = new GameObject($"Coast Post FX ({DaytimeNames.For(daytime)})");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
