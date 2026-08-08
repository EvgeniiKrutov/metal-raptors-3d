using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    [RequireComponent(typeof(Camera))]
    public class AerialHaze : MonoBehaviour
    {
        static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");
        static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        static readonly int HorizonFalloffId = Shader.PropertyToID("_HorizonFalloff");
        static readonly int HorizonSlopeId = Shader.PropertyToID("_HorizonSlope");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SunFalloffId = Shader.PropertyToID("_SunFalloff");
        static readonly int SunIntensityId = Shader.PropertyToID("_SunIntensity");
        static readonly int HaloFalloffId = Shader.PropertyToID("_HaloFalloff");
        static readonly int HaloIntensityId = Shader.PropertyToID("_HaloIntensity");
        static readonly int DiscRadiusId = Shader.PropertyToID("_DiscRadius");
        static readonly int DiscEdgeId = Shader.PropertyToID("_DiscEdge");
        static readonly int MariaIntensityId = Shader.PropertyToID("_MariaIntensity");

        static readonly int FogColorId = Shader.PropertyToID("_HazeFogColor");
        static readonly int FogRangeId = Shader.PropertyToID("_HazeFogRange");
        static readonly int RayCornerId = Shader.PropertyToID("_HazeRayCorner");
        static readonly int RayRightId = Shader.PropertyToID("_HazeRayRight");
        static readonly int RayUpId = Shader.PropertyToID("_HazeRayUp");

        static readonly int[] Floats =
        {
            HorizonFalloffId, HorizonSlopeId, ExposureId, SunFalloffId, SunIntensityId,
            HaloFalloffId, HaloIntensityId, DiscRadiusId, DiscEdgeId, MariaIntensityId,
        };

        static readonly int[] Colors = { TopColorId, HorizonColorId, BottomColorId, SunColorId };

        Camera _cam;
        Material _sky;
        Material _mat;
        HazePass _pass;

        public static AerialHaze Attach(Camera cam, Material sky)
        {
            var shader = Shader.Find("Hidden/AerialHaze");
            if (shader == null)
            {
                Debug.LogWarning("AerialHaze: Hidden/AerialHaze not found; fog stays flat.");
                return null;
            }

            var stale = cam.GetComponent<AerialHaze>();
            if (stale != null)
            {
                stale.enabled = false;
                Destroy(stale);
            }

            var haze = cam.gameObject.AddComponent<AerialHaze>();
            haze._cam = cam;
            haze._sky = sky;
            haze._mat = new Material(shader) { name = "Aerial Haze (runtime)" };
            haze._pass = new HazePass(haze._mat);
            return haze;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _cam || _mat == null || _pass == null || _sky == null) return;
            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.Linear) return;

            float start = RenderSettings.fogStartDistance;
            float end = RenderSettings.fogEndDistance;
            if (end - start < 1e-3f) return;

            CopySky();
            _mat.SetColor(FogColorId, RenderSettings.fogColor);
            _mat.SetVector(FogRangeId, new Vector4(start, end, 1f / (end - start), 0f));
            SetViewRays();

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.scriptableRenderer.EnqueuePass(_pass);
        }

        void CopySky()
        {
            foreach (int id in Colors) _mat.SetColor(id, _sky.GetColor(id));
            foreach (int id in Floats) _mat.SetFloat(id, _sky.GetFloat(id));
            _mat.SetVector(SunDirectionId, _sky.GetVector(SunDirectionId));
        }

        void SetViewRays()
        {
            Vector3 eye = _cam.transform.position;
            Vector3 corner = _cam.ViewportToWorldPoint(new Vector3(0f, 0f, 1f)) - eye;
            Vector3 right = _cam.ViewportToWorldPoint(new Vector3(1f, 0f, 1f)) - eye - corner;
            Vector3 up = _cam.ViewportToWorldPoint(new Vector3(0f, 1f, 1f)) - eye - corner;

            _mat.SetVector(RayCornerId, corner);
            _mat.SetVector(RayRightId, right);
            _mat.SetVector(RayUpId, up);
        }

        class HazePass : ScriptableRenderPass
        {
            static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

            readonly Material _material;

            public HazePass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            class PassData
            {
                public Material material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Aerial Haze", out var passData))
                {
                    passData.material = _material;

                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(resources.cameraColor, 0, AccessFlags.ReadWrite);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        Block.Clear();
                        Block.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0,
                            MeshTopology.Triangles, 3, 1, Block);
                    });
                }
            }
        }
    }
}
