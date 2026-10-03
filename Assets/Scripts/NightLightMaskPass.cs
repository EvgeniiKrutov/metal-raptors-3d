using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class NightLightMaskPass : ScriptableRenderPass
    {
        public static readonly int MaskId = Shader.PropertyToID("_NightLightMask");
        public static readonly int UVMatrixId = Shader.PropertyToID("_NL_MaskUVMatrix");
        public static readonly int DecodeId = Shader.PropertyToID("_NL_MaskDecode");
        public static readonly int PlaneZId = Shader.PropertyToID("_NL_PlaneZ");
        public static readonly int DepthRangeId = Shader.PropertyToID("_NL_DepthRange");

        public const float GuardScale = 1.6f;

        static readonly int VPId = Shader.PropertyToID("_NL_MaskVP");
        static readonly int EncodeId = Shader.PropertyToID("_NL_MaskEncode");
        static readonly int CountId = Shader.PropertyToID("_NL_Count");
        static readonly int AId = Shader.PropertyToID("_NL_A");
        static readonly int BId = Shader.PropertyToID("_NL_B");
        static readonly int CId = Shader.PropertyToID("_NL_C");
        static readonly int DId = Shader.PropertyToID("_NL_D");

        static readonly ProfilingSampler Sampler = new ProfilingSampler("NightLightMask");
        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        static readonly Vector4[] A = new Vector4[NightLights.MaxLights];
        static readonly Vector4[] B = new Vector4[NightLights.MaxLights];
        static readonly Vector4[] C = new Vector4[NightLights.MaxLights];
        static readonly Vector4[] D = new Vector4[NightLights.MaxLights];

        readonly Material _material;
        readonly bool _lightweight;

        public NightLightingTier Tier;

        public bool ShadowOn;

        public NightLightMaskPass(Material material, bool lightweight)
        {
            _material = material;
            _lightweight = lightweight;
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
        }

        class PassData
        {
            public Material material;
            public int count;
            public Matrix4x4 maskVP;
            public Matrix4x4 uvMatrix;
            public float encode;
            public float decode;
            public float planeZ;
            public float depthRange;
            public bool shadowOn;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            Camera cam = cameraData.camera;
            if (cam == null) return;

            int width = 1;
            int height = 1;
            GraphicsFormat format = GraphicsFormat.R8G8B8A8_UNorm;
            float encode = 0f;
            float decode = 0f;
            int count = 0;
            float depthRange = 1f;
            Matrix4x4 area = Matrix4x4.identity;

            if (!_lightweight && Tier != null)
            {
                RenderTextureDescriptor target = cameraData.cameraTargetDescriptor;
                width = Mathf.Max(1, Mathf.RoundToInt(target.width * Tier.maskScale));
                height = Mathf.Max(1, Mathf.RoundToInt(target.height * Tier.maskScale));

                bool hdr = Tier.hdrMask && SystemInfo.IsFormatSupported(
                    GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render);
                format = hdr ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm;
                encode = hdr ? 1f : 0.25f;
                decode = hdr ? 1f : 4f;

                Rect plane = NightLights.PlaneArea(cam, NightLights.PlaneZ, GuardScale);
                area = Matrix4x4.Ortho(plane.xMin, plane.xMax, plane.yMin, plane.yMax, -1f, 1f);
                count = NightLights.Collect(plane, NightLights.PlaneZ, Tier.maxLights, A, B, C, D, out depthRange);
            }

            var desc = new TextureDesc(width, height)
            {
                name = "_NightLightMask",
                format = format,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = true,
                clearColor = Color.clear,
                msaaSamples = MSAASamples.None,
            };
            TextureHandle mask = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("NightLightMask", out var passData, Sampler))
            {
                passData.material = _material;
                passData.count = count;
                passData.maskVP = GL.GetGPUProjectionMatrix(area, true);
                passData.uvMatrix = area;
                passData.encode = encode;
                passData.decode = decode;
                passData.planeZ = NightLights.PlaneZ;
                passData.depthRange = depthRange;
                passData.shadowOn = ShadowOn && !_lightweight;

                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(mask, MaskId);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalMatrix(UVMatrixId, data.uvMatrix);
                    context.cmd.SetGlobalFloat(DecodeId, data.decode);
                    context.cmd.SetGlobalFloat(PlaneZId, data.planeZ);
                    context.cmd.SetGlobalFloat(DepthRangeId, data.depthRange);
                    context.cmd.SetGlobalFloat(CountId, data.count);
                    if (!data.shadowOn)
                        context.cmd.SetGlobalVector(NightLightShadowPass.ShadowParamsId, Vector4.zero);

                    if (data.count <= 0 || data.material == null) return;

                    Block.Clear();
                    Block.SetVectorArray(AId, A);
                    Block.SetVectorArray(BId, B);
                    Block.SetVectorArray(CId, C);
                    Block.SetVectorArray(DId, D);
                    Block.SetMatrix(VPId, data.maskVP);
                    Block.SetFloat(EncodeId, data.encode);
                    Block.SetFloat(DepthRangeId, data.depthRange);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0,
                        MeshTopology.Triangles, 6, data.count, Block);
                });
            }
        }
    }
}
