using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class NightSky
    {
        public static readonly Color HazeColor = new Color(0.201f, 0.137f, 0.411f);
        public static readonly Color CloudColor = new Color(0.40f, 0.42f, 0.58f);
        static readonly Color ZenithColor = new Color(0.045f, 0.033f, 0.151f);
        static readonly Color MoonColor = new Color(0.650f, 0.594f, 1.000f);
        static readonly Color MoonLightColor = new Color(0.60f, 0.68f, 0.90f);
        static readonly Color AmbientSkyColor = new Color(0.21f, 0.21f, 0.39f);
        static readonly Color AmbientEquatorColor = new Color(0.23f, 0.21f, 0.35f);
        static readonly Color AmbientGroundColor = new Color(0.12f, 0.10f, 0.17f);

        const float MoonViewportX = 0.74f;
        const float MoonHorizonLift = 0.30f;

        const float StarRiseHeight = 0.12f;
        const float StarFullHeight = 0.62f;

        const float FirelightBoost = 2.05f;

        const float MoonIntensity = 6.9f;
        const float MoonHaloIntensity = 0.61f;

        static readonly Color RayColor = new Color(0.549f, 0.526f, 1.000f);
        const float RayIntensity = 0.83f;
        const float RayDensity = 0.75f;
        const float RayFalloff = 1.5f;

        static readonly Quaternion MoonLightRotation = Quaternion.Euler(50f, -14f, 0f);
        const float MoonLightIntensity = 1f;

        public static void Apply(Camera cam, Weather weather)
        {
            BuildSkybox(cam);
            TuneMoonLight();
            BuildPostFx(cam);
            Firelight.Grade(Color.white, FirelightBoost);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSkyColor;
            RenderSettings.ambientEquatorColor = AmbientEquatorColor;
            RenderSettings.ambientGroundColor = AmbientGroundColor;

            NightLightingController.Launch(cam, TerrainKind.Verdun);
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
            sky.SetFloat("_SunIntensity", MoonIntensity);
            sky.SetFloat("_DiscRadius", 1.8f);
            sky.SetFloat("_DiscEdge", 0.12f);
            sky.SetFloat("_MariaIntensity", 0.25f);
            sky.SetFloat("_HaloFalloff", 8f);
            sky.SetFloat("_HaloIntensity", MoonHaloIntensity);
            sky.SetFloat("_StarIntensity", 1.6f);
            sky.SetFloat("_StarScale", 80f);
            sky.SetFloat("_StarHorizon", StarRiseHeight);
            sky.SetFloat("_StarZenith", StarFullHeight);
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_NightSkyMix", 0f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky, MoonViewportX, MoonHorizonLift, anchorSun: true);
            GodRays.Attach(cam, sky, RayColor, RayIntensity,
                density: RayDensity, radialFalloff: RayFalloff);
            AerialHaze.Attach(cam, sky);
        }

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
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.7f);

            GraphicsOptions.TrackBloom(bloom);

            var grade = profile.Add<ColorAdjustments>();
            grade.contrast.Override(4f);

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
