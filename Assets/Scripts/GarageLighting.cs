using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public static class GarageLighting
    {
        static readonly Color SunLightColor = new Color(1.00f, 0.96f, 0.90f);
        static readonly Color AmbientSkyColor = new Color(0.61f, 0.75f, 1.00f);
        static readonly Color AmbientEquatorColor = new Color(0.80f, 0.84f, 0.88f);
        static readonly Color AmbientGroundColor = new Color(0.47f, 0.41f, 0.34f);

        static readonly Quaternion SunLightRotation = Quaternion.Euler(55f, -92f, 0f);
        const float SunLightIntensity = 1.35f;

        const float ShadowStrength = 0.75f;
        const float ShadowNormalBias = 0.5f;
        const float ShadowDistance = 150f;

        public static void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSkyColor;
            RenderSettings.ambientEquatorColor = AmbientEquatorColor;
            RenderSettings.ambientGroundColor = AmbientGroundColor;

            TuneSunLight();

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, ShadowDistance);
        }

        static void TuneSunLight()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;

                light.color = SunLightColor;
                light.intensity = SunLightIntensity;
                light.transform.rotation = SunLightRotation;
                GraphicsOptions.Track(light, LightShadows.Soft);
                light.shadowStrength = ShadowStrength;
                light.shadowNormalBias = ShadowNormalBias;

                RenderSettings.sun = light;
                break;
            }
        }
    }
}
