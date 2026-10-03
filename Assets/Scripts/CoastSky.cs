using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class CoastSky
    {
        public const float FogEnd = 1500f;
        public const float NightFogEnd = 1250f;
        public const float MorningFogEnd = 1150f;

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
            // 0 leaves the warm-light gain neutral; see docs/atmospheres.md.
            public float firelightBoost;
            public bool night;
        }

        static readonly Palette[] Palettes =
        {
            new Palette
            {
                haze = new Color(0.76f, 0.78f, 0.79f),
                zenith = new Color(0.56f, 0.61f, 0.67f),
                cloud = new Color(0.88f, 0.89f, 0.89f),
                disc = new Color(0.96f, 0.95f, 0.91f),
                keyLight = new Color(0.90f, 0.91f, 0.92f),
                ambientSky = new Color(0.66f, 0.70f, 0.76f),
                ambientEquator = new Color(0.78f, 0.80f, 0.80f),
                ambientGround = new Color(0.42f, 0.42f, 0.40f),
                sea = new Color(0.36f, 0.42f, 0.42f),
                rayColor = new Color(0.90f, 0.91f, 0.90f),
                colorFilter = Color.white,
                shadowTone = new Color(0.36f, 0.39f, 0.46f),
                highlightTone = new Color(0.86f, 0.87f, 0.85f),
                horizonFalloff = 1.6f,
                discFalloff = 120f, discIntensity = 1.2f,
                haloFalloff = 4f, haloIntensity = 0.12f,
                discViewportX = 0.78f, discLift = 0.09f, anchorDisc = true,
                lightRotation = Quaternion.Euler(28f, -14f, 0f),
                lightIntensity = 1.20f,
                fogStartOffset = 80f, fogEnd = MorningFogEnd,
                rayIntensity = 0.12f, rayDensity = 0.80f, rayFalloff = 1.2f,
                bloomThreshold = 1.05f, bloomIntensity = 0.5f,
                temperature = -4f, postExposure = 0.40f, saturation = -14f, contrast = 8f,
                splitBalance = -10f, vignette = 0.15f, cloudGlow = 0.15f,
            },
            new Palette
            {
                haze = new Color(0.76f, 0.84f, 0.90f),
                zenith = new Color(0.34f, 0.52f, 0.78f),
                cloud = new Color(0.98f, 0.99f, 1.00f),
                disc = new Color(1.00f, 1.00f, 0.97f),
                keyLight = new Color(0.98f, 0.99f, 1.00f),
                ambientSky = new Color(0.68f, 0.80f, 0.95f),
                ambientEquator = new Color(0.84f, 0.88f, 0.90f),
                ambientGround = new Color(0.48f, 0.48f, 0.44f),
                sea = new Color(0.36f, 0.52f, 0.50f),
                rayColor = new Color(0.94f, 0.97f, 1.00f),
                colorFilter = Color.white,
                shadowTone = new Color(0.28f, 0.38f, 0.52f),
                highlightTone = new Color(0.90f, 0.94f, 0.96f),
                horizonFalloff = 1.9f,
                discFalloff = 500f, discIntensity = 3.2f,
                haloFalloff = 12f, haloIntensity = 0.15f,
                discViewportX = 0.50f, discLift = 0.82f, anchorDisc = false,
                lightRotation = Quaternion.Euler(52f, 4f, 0f),
                lightIntensity = 1.50f,
                fogStartOffset = 620f, fogEnd = FogEnd,
                rayIntensity = 0.22f, rayDensity = 0.65f, rayFalloff = 1.8f,
                bloomThreshold = 1.15f, bloomIntensity = 0.6f,
                temperature = -10f, postExposure = 0.36f, saturation = 4f, contrast = 10f,
                splitBalance = -18f, vignette = 0.13f, cloudGlow = 0.16f,
            },
            new Palette
            {
                haze = new Color(0.88f, 0.78f, 0.68f),
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
                discViewportX = 0.18f, discLift = 0.03f, anchorDisc = true,
                lightRotation = Quaternion.Euler(18f, 22f, 0f),
                lightIntensity = 1.30f,
                fogStartOffset = 520f, fogEnd = FogEnd,
                rayIntensity = 0.65f, rayDensity = 0.85f, rayFalloff = 1.1f,
                bloomThreshold = 0.95f, bloomIntensity = 1.0f,
                temperature = 6f, postExposure = 0.48f, saturation = 0f, contrast = 6f,
                splitBalance = -20f, vignette = 0.14f, cloudGlow = 0.40f,
            },
            new Palette
            {
                haze = new Color(0.134f, 0.183f, 0.305f),
                zenith = new Color(0.032f, 0.061f, 0.156f),
                cloud = new Color(0.38f, 0.43f, 0.55f),
                disc = new Color(0.663f, 0.755f, 1.000f),
                keyLight = new Color(0.60f, 0.70f, 0.92f),
                ambientSky = new Color(0.20f, 0.24f, 0.37f),
                ambientEquator = new Color(0.22f, 0.25f, 0.34f),
                ambientGround = new Color(0.12f, 0.13f, 0.17f),
                sea = new Color(0.09f, 0.12f, 0.15f),
                rayColor = new Color(0.527f, 0.657f, 1.000f),
                colorFilter = Color.white,
                shadowTone = Color.grey,
                highlightTone = Color.grey,
                horizonFalloff = 0.65f,
                discFalloff = 60f, discIntensity = 5.23f,
                haloFalloff = 12f, haloIntensity = 0.42f,
                discRadius = 1.8f, mariaIntensity = 0.25f, starIntensity = 1.4f,
                discViewportX = 0.72f, discLift = 0.28f, anchorDisc = true,
                lightRotation = Quaternion.Euler(48f, -12f, 0f),
                lightIntensity = 1.00f,
                fogStartOffset = 420f, fogEnd = NightFogEnd,
                rayIntensity = 0.52f, rayDensity = 0.75f, rayFalloff = 1.5f,
                bloomThreshold = 0.85f, bloomIntensity = 1.1f,
                temperature = 0f, postExposure = 0f, saturation = 0f, contrast = 4f,
                splitBalance = 0f, vignette = 0.27f, cloudGlow = 0.55f,
                firelightBoost = 1.8f,
                night = true,
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
            if (p.firelightBoost > 0f) Firelight.Grade(p.colorFilter, p.firelightBoost);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;

            if (p.night) NightLightingController.Launch(cam, TerrainKind.Flanders);
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
            sky.SetFloat("_NightSkyMix", p.night ? 0f : 1f);

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
