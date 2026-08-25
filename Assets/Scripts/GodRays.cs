using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    [RequireComponent(typeof(Camera))]
    public class GodRays : MonoBehaviour
    {
        static readonly int SunScreenPosId = Shader.PropertyToID("_SunScreenPos");
        static readonly int RayColorId = Shader.PropertyToID("_RayColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int DensityId = Shader.PropertyToID("_Density");
        static readonly int WeightId = Shader.PropertyToID("_Weight");
        static readonly int DecayId = Shader.PropertyToID("_Decay");
        static readonly int SampleCountId = Shader.PropertyToID("_SampleCount");
        static readonly int RadialFalloffId = Shader.PropertyToID("_RadialFalloff");

        Camera _cam;
        Material _sky;
        Material _mat;
        RayPass _pass;
        float _edgeFade = 0.35f;
        float _viewCutoff = -0.1f;
        Vector3 _sunDir = Vector3.forward;

        public static GodRays Attach(Camera cam, Material sky, Color color, float intensity,
            float density = 0.85f, float weight = 0.6f, float decay = 0.97f,
            int samples = 48, float radialFalloff = 1.1f)
        {
            if (GraphicsOptions.Mobile && !GraphicsOptions.GodRays) return null;

            var shader = Shader.Find("Hidden/StylizedGodRays");
            if (shader == null)
            {
                Debug.LogWarning("GodRays: Hidden/StylizedGodRays not found; no light shafts.");
                return null;
            }

            var stale = cam.GetComponent<GodRays>();
            if (stale != null)
            {
                stale.enabled = false;
                Destroy(stale);
            }

            var rays = cam.gameObject.AddComponent<GodRays>();
            rays._cam = cam;
            rays._sky = sky;

            rays._mat = new Material(shader) { name = "God Rays (runtime)" };
            rays._mat.SetColor(RayColorId, color);
            rays._mat.SetFloat(IntensityId, intensity);
            rays._mat.SetFloat(DensityId, density);
            rays._mat.SetFloat(WeightId, weight);
            rays._mat.SetFloat(DecayId, decay);
            rays._mat.SetFloat(SampleCountId, samples);
            rays._mat.SetFloat(RadialFalloffId, radialFalloff);

            rays._pass = new RayPass(rays._mat);
            return rays;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _cam || _mat == null || _pass == null) return;
            if (!GraphicsOptions.GodRays) return;

            _mat.SetVector(SunScreenPosId, TrackSun());

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.scriptableRenderer.EnqueuePass(_pass);
        }

        Vector4 TrackSun()
        {
            if (_sky != null)
            {
                Vector3 skyDir = (Vector3)_sky.GetVector("_SunDirection");
                if (skyDir.sqrMagnitude > 0.0001f) _sunDir = skyDir.normalized;
            }

            Vector3 vp = _cam.WorldToViewportPoint(_cam.transform.position + _sunDir * 10000f);
            if (vp.z <= 0f) return Vector4.zero;

            float dx = Mathf.Max(0f, Mathf.Max(-vp.x, vp.x - 1f));
            float dy = Mathf.Max(0f, Mathf.Max(-vp.y, vp.y - 1f));
            float outside = Mathf.Sqrt(dx * dx + dy * dy);
            float visibility = Mathf.Clamp01(1f - outside / Mathf.Max(_edgeFade, 1e-4f));

            float align = Vector3.Dot(_cam.transform.forward, _sunDir);
            visibility *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_viewCutoff, 1f, align));

            return new Vector4(vp.x, vp.y, visibility, 0f);
        }

        class RayPass : ScriptableRenderPass
        {
            static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

            readonly Material _material;

            public RayPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            class PassData
            {
                public Material material;
                public TextureHandle source;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var desc = renderGraph.GetTextureDesc(resources.cameraColor);
                desc.name = "_GodRaysColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("God Rays", out var passData))
                {
                    passData.material = _material;
                    passData.source = resources.cameraColor;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        RTHandle source = data.source;
                        Block.Clear();
                        Block.SetTexture(BlitTextureId, source);
                        Block.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0,
                            MeshTopology.Triangles, 3, 1, Block);
                    });
                }

                resources.cameraColor = destination;
            }
        }
    }
}
