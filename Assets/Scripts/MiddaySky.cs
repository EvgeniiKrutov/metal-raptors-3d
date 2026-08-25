using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class MiddaySky
    {
        public static readonly Color HazeColor = new Color(0.78f, 0.85f, 0.93f);
        public static readonly Color CloudColor = new Color(1.00f, 1.00f, 1.00f);
        static readonly Color ZenithColor = new Color(0.24f, 0.46f, 0.82f);
        static readonly Color SunColor = new Color(1.00f, 0.97f, 0.90f);
        static readonly Color SunLightColor = new Color(1.00f, 0.96f, 0.90f);
        static readonly Color AmbientSkyColor = new Color(0.61f, 0.75f, 1.00f);
        static readonly Color AmbientEquatorColor = new Color(0.80f, 0.84f, 0.88f);
        static readonly Color AmbientGroundColor = new Color(0.47f, 0.41f, 0.34f);

        static readonly Vector2 SunViewportAnchor = new Vector2(0.50f, 0.85f);

        static readonly Color RayColor = new Color(1.00f, 0.97f, 0.90f);
        const float RayIntensity = 0.45f;
        const float RayDensity = 0.65f;
        const float RayFalloff = 1.8f;

        static readonly Quaternion SunLightRotation = Quaternion.Euler(58f, 0f, 0f);
        const float SunLightIntensity = 1.35f;

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
                Debug.LogWarning("MiddaySky: Custom/GradientSkybox not found; using flat sky.");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = HazeColor;
                return;
            }

            var sky = new Material(shader) { name = "Midday Sky (runtime)" };
            sky.SetColor("_TopColor", ZenithColor);
            sky.SetColor("_HorizonColor", HazeColor);
            sky.SetColor("_BottomColor", HazeColor);
            sky.SetFloat("_HorizonFalloff", 1.8f);
            sky.SetColor("_SunColor", SunColor);
            Vector3 sunDir = cam.ViewportPointToRay(
                new Vector3(SunViewportAnchor.x, SunViewportAnchor.y, 1f)).direction;
            sky.SetVector("_SunDirection", sunDir);
            sky.SetFloat("_SunFalloff", 800f);
            sky.SetFloat("_SunIntensity", 6f);
            sky.SetFloat("_HaloFalloff", 14f);
            sky.SetFloat("_HaloIntensity", 0.22f);
            sky.SetFloat("_Exposure", 1f);

            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;

            SkyHorizon.Attach(cam, sky);
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
            profile.name = "Midday Post FX (runtime)";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.6f);

            GraphicsOptions.TrackBloom(bloom);

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.temperature.Override(-4f);

            var grade = profile.Add<ColorAdjustments>();
            grade.postExposure.Override(0.35f);
            grade.saturation.Override(10f);
            grade.contrast.Override(10f);

            var splitToning = profile.Add<SplitToning>();
            splitToning.shadows.Override(new Color(0.32f, 0.34f, 0.62f));
            splitToning.highlights.Override(new Color(1.00f, 0.82f, 0.55f));
            splitToning.balance.Override(-25f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var go = new GameObject("Midday Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
