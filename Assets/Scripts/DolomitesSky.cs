using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class DolomitesSky
    {
        public const float FogEnd = 2000f;
        public const float NightFogEnd = 1850f;

        // The valley mist, sized off the ridges' green-to-stone line (docs/dolomites.md).
        const float MistTopY = 150f, MistClearY = 330f, MistStrength = 0.3f;
        const float MistFromZ = 500f, MistFullZ = 760f;

        class Palette
        {
            public Color haze, zenith, cloud;
            public Color disc, keyLight;
            public Color ambientSky, ambientEquator, ambientGround;
            public Color slope, rock, peak;
            public Color rayColor, colorFilter, shadowTone, highlightTone;

            public float horizonFalloff, discFalloff, discIntensity, haloFalloff, haloIntensity;
            public float discRadius, mariaIntensity, starIntensity;
            public float discViewportX, discViewportY;

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
                haze = new Color(0.93f, 0.88f, 0.80f),
                zenith = new Color(0.34f, 0.52f, 0.80f),
                cloud = new Color(1.00f, 0.96f, 0.90f),
                disc = new Color(1.00f, 0.92f, 0.76f),
                keyLight = new Color(1.00f, 0.94f, 0.84f),
                ambientSky = new Color(0.62f, 0.72f, 0.88f),
                ambientEquator = new Color(0.88f, 0.86f, 0.80f),
                ambientGround = new Color(0.46f, 0.46f, 0.38f),
                slope = new Color(0.28f, 0.40f, 0.22f),
                rock = new Color(0.40f, 0.40f, 0.43f),
                peak = new Color(0.88f, 0.86f, 0.84f),
                rayColor = new Color(1.00f, 0.94f, 0.80f),
                colorFilter = Color.white,
                shadowTone = new Color(0.30f, 0.38f, 0.54f),
                highlightTone = new Color(0.98f, 0.90f, 0.74f),
                horizonFalloff = 2.2f,
                discFalloff = 300f, discIntensity = 4.2f,
                haloFalloff = 10f, haloIntensity = 0.16f,
                discViewportX = 0.80f, discViewportY = 0.94f,
                lightRotation = Quaternion.Euler(42f, -16f, 0f),
                lightIntensity = 1.45f,
                fogStartOffset = 180f, fogEnd = FogEnd,
                rayIntensity = 0.30f, rayDensity = 0.80f, rayFalloff = 1.2f,
                bloomThreshold = 1.00f, bloomIntensity = 0.95f,
                temperature = 8f, postExposure = 0.45f, saturation = 4f, contrast = 5f,
                splitBalance = -8f, vignette = 0.12f, cloudGlow = 0.32f,
            },
            new Palette
            {
                haze = new Color(0.82f, 0.88f, 0.94f),
                zenith = new Color(0.17f, 0.42f, 0.82f),
                cloud = new Color(1.00f, 1.00f, 0.99f),
                disc = new Color(1.00f, 0.99f, 0.94f),
                keyLight = new Color(1.00f, 0.98f, 0.92f),
                ambientSky = new Color(0.58f, 0.74f, 0.94f),
                ambientEquator = new Color(0.90f, 0.94f, 0.96f),
                ambientGround = new Color(0.50f, 0.50f, 0.42f),
                slope = new Color(0.30f, 0.44f, 0.22f),
                rock = new Color(0.45f, 0.46f, 0.49f),
                peak = new Color(0.92f, 0.93f, 0.94f),
                rayColor = new Color(1.00f, 0.99f, 0.92f),
                colorFilter = Color.white,
                shadowTone = new Color(0.28f, 0.38f, 0.58f),
                highlightTone = new Color(1.00f, 0.96f, 0.86f),
                horizonFalloff = 1.7f,
                discFalloff = 700f, discIntensity = 5.5f,
                haloFalloff = 16f, haloIntensity = 0.13f,
                discViewportX = 0.50f, discViewportY = 0.96f,
                lightRotation = Quaternion.Euler(58f, 2f, 0f),
                lightIntensity = 1.55f,
                fogStartOffset = 300f, fogEnd = FogEnd,
                rayIntensity = 0.22f, rayDensity = 0.60f, rayFalloff = 1.9f,
                bloomThreshold = 1.10f, bloomIntensity = 0.75f,
                temperature = 2f, postExposure = 0.40f, saturation = 8f, contrast = 8f,
                splitBalance = -12f, vignette = 0.10f, cloudGlow = 0.24f,
            },
            new Palette
            {
                haze = new Color(0.96f, 0.78f, 0.64f),
                zenith = new Color(0.30f, 0.36f, 0.62f),
                cloud = new Color(0.99f, 0.84f, 0.76f),
                disc = new Color(1.00f, 0.80f, 0.54f),
                keyLight = new Color(1.00f, 0.84f, 0.64f),
                ambientSky = new Color(0.54f, 0.60f, 0.82f),
                ambientEquator = new Color(0.94f, 0.82f, 0.74f),
                ambientGround = new Color(0.46f, 0.42f, 0.36f),
                slope = new Color(0.26f, 0.36f, 0.20f),
                rock = new Color(0.42f, 0.37f, 0.38f),
                peak = new Color(0.94f, 0.82f, 0.74f),
                rayColor = new Color(1.00f, 0.78f, 0.52f),
                colorFilter = Color.white,
                shadowTone = new Color(0.26f, 0.32f, 0.56f),
                highlightTone = new Color(1.00f, 0.76f, 0.48f),
                horizonFalloff = 2.8f,
                discFalloff = 200f, discIntensity = 4.6f,
                haloFalloff = 8f, haloIntensity = 0.22f,
                discViewportX = 0.20f, discViewportY = 0.93f,
                lightRotation = Quaternion.Euler(36f, -22f, 0f),
                lightIntensity = 1.35f,
                fogStartOffset = 250f, fogEnd = FogEnd,
                rayIntensity = 0.38f, rayDensity = 0.85f, rayFalloff = 1.1f,
                bloomThreshold = 0.90f, bloomIntensity = 1.15f,
                temperature = 18f, postExposure = 0.42f, saturation = 6f, contrast = 4f,
                splitBalance = -16f, vignette = 0.14f, cloudGlow = 0.42f,
            },
            new Palette
            {
                haze = new Color(0.22f, 0.26f, 0.36f),
                zenith = new Color(0.04f, 0.07f, 0.14f),
                cloud = new Color(0.48f, 0.54f, 0.66f),
                disc = new Color(0.92f, 0.95f, 1.00f),
                keyLight = new Color(0.68f, 0.78f, 0.98f),
                ambientSky = new Color(0.30f, 0.36f, 0.52f),
                ambientEquator = new Color(0.32f, 0.36f, 0.48f),
                ambientGround = new Color(0.20f, 0.22f, 0.26f),
                slope = new Color(0.17f, 0.24f, 0.18f),
                rock = new Color(0.24f, 0.27f, 0.34f),
                peak = new Color(0.62f, 0.68f, 0.80f),
                rayColor = new Color(0.74f, 0.84f, 1.00f),
                colorFilter = new Color(0.70f, 0.76f, 0.94f),
                shadowTone = new Color(0.26f, 0.32f, 0.56f),
                highlightTone = new Color(0.64f, 0.76f, 1.00f),
                horizonFalloff = 0.70f,
                discFalloff = 60f, discIntensity = 2.6f,
                haloFalloff = 14f, haloIntensity = 0.16f,
                discRadius = 1.8f, mariaIntensity = 0.25f, starIntensity = 1.6f,
                discViewportX = 0.74f, discViewportY = 0.92f,
                lightRotation = Quaternion.Euler(52f, -12f, 0f),
                lightIntensity = 1.20f,
                fogStartOffset = 220f, fogEnd = NightFogEnd,
                rayIntensity = 0.18f, rayDensity = 0.75f, rayFalloff = 1.5f,
                bloomThreshold = 0.85f, bloomIntensity = 1.1f,
                temperature = -18f, postExposure = 2.0f, saturation = -8f, contrast = 3f,
                splitBalance = 8f, vignette = 0.16f, cloudGlow = 0.55f,
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

        public static Color MountainSlope(Daytime daytime) => For(daytime).slope;

        public static Color MountainRock(Daytime daytime) => For(daytime).rock;

        public static Color MountainPeak(Daytime daytime) => For(daytime).peak;

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
                Debug.LogWarning("DolomitesSky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = p.haze;
                return;
            }

            var sky = new Material(shader) { name = "Dolomites Sky (runtime)" };
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
            sky.SetVector("_SunDirection", cam.ViewportPointToRay(
                new Vector3(p.discViewportX, p.discViewportY, 1f)).direction);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.AtEyeLevel(cam, sky, p.discViewportX, p.discViewportY);
            GodRays.Attach(cam, sky, p.rayColor, p.rayIntensity,
                density: p.rayDensity, radialFalloff: p.rayFalloff);
            AerialHaze.Attach(cam, sky);
            GroundHaze.Attach(cam, MistTopY, MistClearY, MistStrength, MistFromZ, MistFullZ);
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
            profile.name = "Dolomites Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(p.bloomThreshold);
            bloom.intensity.Override(p.bloomIntensity);
            bloom.scatter.Override(0.65f);

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

            var go = new GameObject($"Dolomites Post FX ({DaytimeNames.For(daytime)})");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
