using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class MorningSky
    {
        public static readonly Color HazeColor = new Color(0.90f, 0.84f, 0.75f);
        public static readonly Color CloudColor = new Color(1.00f, 0.94f, 0.86f);
        static readonly Color ZenithColor = new Color(0.52f, 0.62f, 0.76f);
        static readonly Color SunColor = new Color(1.00f, 0.88f, 0.66f);
        static readonly Color SunLightColor = new Color(1.00f, 0.84f, 0.64f);
        static readonly Color AmbientSkyColor = new Color(0.56f, 0.67f, 0.90f);
        static readonly Color AmbientEquatorColor = new Color(0.84f, 0.80f, 0.80f);
        static readonly Color AmbientGroundColor = new Color(0.38f, 0.31f, 0.25f);

        static readonly Color RayColor = new Color(1.00f, 0.88f, 0.68f);
        const float RayIntensity = 0.85f;
        const float RayDensity = 0.85f;
        const float RayFalloff = 1.1f;

        const float SunViewportX = 0.80f;
        const float SunHorizonLift = 0.08f;

        static readonly Quaternion SunLightRotation = Quaternion.Euler(30f, -17f, 0f);
        const float SunLightIntensity = 1.25f;

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
                Debug.LogWarning("MorningSky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = HazeColor;
                return;
            }

            var sky = new Material(shader) { name = "Morning Sky (runtime)" };
            sky.SetColor("_TopColor", ZenithColor);
            sky.SetColor("_HorizonColor", HazeColor);
            sky.SetColor("_BottomColor", HazeColor);
            sky.SetFloat("_HorizonFalloff", 2.5f);
            sky.SetColor("_SunColor", SunColor);
            sky.SetFloat("_SunFalloff", 300f);
            sky.SetFloat("_SunIntensity", 4.5f);
            sky.SetFloat("_HaloFalloff", 7f);
            sky.SetFloat("_HaloIntensity", 0.35f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky, SunViewportX, SunHorizonLift, anchorSun: true);
            GodRays.Attach(cam, sky, RayColor, RayIntensity,
                density: RayDensity, radialFalloff: RayFalloff);
            AerialHaze.Attach(cam, sky);
        }

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

        static void BuildPostFx(Camera cam)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Morning Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.75f);

            GraphicsOptions.TrackBloom(bloom);

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(12f);

            var grade = profile.Add<ColorAdjustments>();
            grade.postExposure.Override(0.40f);
            grade.saturation.Override(8f);
            grade.contrast.Override(6f);

            var splitToning = profile.Add<SplitToning>();
            splitToning.shadows.Override(new Color(0.34f, 0.28f, 0.58f));
            splitToning.highlights.Override(new Color(1.00f, 0.70f, 0.34f));
            splitToning.balance.Override(-15f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.20f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var go = new GameObject("Morning Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
