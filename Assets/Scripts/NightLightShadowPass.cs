using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class NightLightShadowPass : ScriptableRenderPass
    {
        public static readonly int ShadowMapId = Shader.PropertyToID("_NL_ShadowMap");
        public static readonly int ShadowMatrixId = Shader.PropertyToID("_NL_ShadowMatrix");
        public static readonly int ShadowParamsId = Shader.PropertyToID("_NL_ShadowParams");
        public static readonly int ShadowLightId = Shader.PropertyToID("_NL_ShadowLight");
        public static readonly int ShadowSizeId = Shader.PropertyToID("_NL_ShadowSize");

        static readonly int CasterVPId = Shader.PropertyToID("_NL_CasterVP");
        static readonly int CasterLightId = Shader.PropertyToID("_NL_CasterLight");
        static readonly int CasterConeId = Shader.PropertyToID("_NL_CasterCone");
        static readonly int OccupancyId = Shader.PropertyToID("_NL_Occupancy");
        static readonly int OccupancySizeId = Shader.PropertyToID("_NL_OccupancySize");
        static readonly int RayStartId = Shader.PropertyToID("_NL_RayStart");
        static readonly int RayParamsId = Shader.PropertyToID("_NL_RayParams");
        static readonly ShaderTagId CasterTag = new ShaderTagId("ShadowCaster");
        static readonly ProfilingSampler OccupancySampler = new ProfilingSampler("NightLightOccupancy");
        static readonly ProfilingSampler RaySampler = new ProfilingSampler("NightLightShadowRays");
        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        const int OccupancyPass = 0;
        const int RayPass = 1;
        const float SpanMargin = 12f;
        const float ReachScale = 1.3f;
        const float RayStep = 1f;
        const float EdgeBias = 1f;

        readonly Material _material;
        readonly GraphicsFormat _occupancyFormat;
        readonly bool _raysSupported;

        Camera _camera;
        Matrix4x4 _view;
        Matrix4x4 _proj;
        Vector2 _originNdc;
        Vector2 _tipNdc;
        Vector3 _position;
        Vector2 _direction;
        float _range;
        float _outer;
        float _source;
        int _size;

        public NightLightShadowPass(Material material)
        {
            _material = material;
            _occupancyFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UNorm, GraphicsFormatUsage.Render)
                ? GraphicsFormat.R8G8_UNorm
                : GraphicsFormat.R8G8B8A8_UNorm;
            _raysSupported = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16_SFloat, GraphicsFormatUsage.Render);
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
        }

        class OccupancyData
        {
            public RendererListHandle casters;
            public Matrix4x4 casterVP;
            public Vector4 light;
            public Vector4 cone;
        }

        class RayData
        {
            public Material material;
            public TextureHandle occupancy;
            public Vector4 occupancySize;
            public Vector4 origin;
            public Vector4 rayParams;
            public Matrix4x4 screen;
            public Vector4 shadowParams;
            public Vector4 shadowSize;
        }

        public bool Prepare(Camera cam, NightLightingTier tier, NightLight2D light)
        {
            _camera = null;
            if (_material == null || !_raysSupported || cam == null || light == null || tier == null
                || tier.shadowMapSize <= 0)
                return false;

            _view = cam.worldToCameraMatrix;
            _proj = cam.projectionMatrix;
            Matrix4x4 screen = _proj * _view;

            _position = light.transform.position;
            _direction = light.PlaneDirection;
            _range = Mathf.Max(1f, light.range);
            var forward = new Vector3(_direction.x, _direction.y, 0f);
            if (!ToNdc(screen, _position, out _originNdc) || !ToNdc(screen, _position + forward * _range, out _tipNdc))
                return false;

            _outer = Mathf.Clamp(light.outerAngle, 0.2f, 179f);
            _source = Mathf.Max(0f, light.sourceRadius);
            _size = tier.shadowMapSize;
            _camera = cam;
            return true;
        }

        static bool ToNdc(Matrix4x4 screen, Vector3 world, out Vector2 ndc)
        {
            Vector4 c = screen * new Vector4(world.x, world.y, world.z, 1f);
            bool front = c.w > 1e-4f;
            ndc = front ? new Vector2(c.x / c.w, c.y / c.w) : Vector2.zero;
            return front;
        }

        static Vector2 ToPixels(Vector2 ndc, Vector2 size) =>
            Vector2.Scale(ndc * 0.5f + new Vector2(0.5f, 0.5f), size);

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_camera == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            RenderTextureDescriptor target = cameraData.cameraTargetDescriptor;

            int height = Mathf.Clamp(target.height, 1, _size);
            int width = Mathf.Max(1, Mathf.RoundToInt(height * (float)target.width / Mathf.Max(1, target.height)));
            var size = new Vector2(width, height);

            Vector2 origin = ToPixels(_originNdc, size);
            Vector2 axis = ToPixels(_tipNdc, size) - origin;
            float reach = axis.magnitude;
            axis = reach > 1e-3f ? axis / reach : Vector2.right;

            float corner = Mathf.Max(
                Mathf.Max(origin.magnitude, (origin - new Vector2(width, 0f)).magnitude),
                Mathf.Max((origin - new Vector2(0f, height)).magnitude, (origin - size).magnitude));
            float rayReach = Mathf.Max(1f, Mathf.Min(corner, reach * ReachScale));
            float span = Mathf.Min(359f, _outer + SpanMargin * 2f) * Mathf.Deg2Rad;

            var occupancyDesc = new TextureDesc(width, height)
            {
                name = "_NL_Occupancy",
                format = _occupancyFormat,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = true,
                clearColor = Color.clear,
                msaaSamples = MSAASamples.None,
            };
            TextureHandle occupancy = renderGraph.CreateTexture(occupancyDesc);

            var drawing = new DrawingSettings(CasterTag, new SortingSettings(_camera) { criteria = SortingCriteria.None })
            {
                overrideMaterial = _material,
                overrideMaterialPassIndex = OccupancyPass,
                perObjectData = PerObjectData.None,
            };
            var filtering = new FilteringSettings(RenderQueueRange.opaque, -1, NightLights.CasterRenderingLayer);
            RendererListHandle casters = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, drawing, filtering));

            using (var builder = renderGraph.AddRasterRenderPass<OccupancyData>("NightLightOccupancy", out var passData, OccupancySampler))
            {
                passData.casters = casters;
                passData.casterVP = GL.GetGPUProjectionMatrix(_proj, true) * _view;
                passData.light = new Vector4(_position.x, _position.y, _position.z, _source);
                passData.cone = new Vector4(_direction.x, _direction.y,
                    Mathf.Tan(_outer * 0.5f * Mathf.Deg2Rad), _range);

                builder.UseRendererList(casters);
                builder.SetRenderAttachment(occupancy, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (OccupancyData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalMatrix(CasterVPId, data.casterVP);
                    context.cmd.SetGlobalVector(CasterLightId, data.light);
                    context.cmd.SetGlobalVector(CasterConeId, data.cone);
                    context.cmd.DrawRendererList(data.casters);
                });
            }

            var rayDesc = new TextureDesc(_size, 1)
            {
                name = "_NL_ShadowMap",
                format = GraphicsFormat.R16G16_SFloat,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
            };
            TextureHandle rays = renderGraph.CreateTexture(rayDesc);

            using (var builder = renderGraph.AddRasterRenderPass<RayData>("NightLightShadowRays", out var passData, RaySampler))
            {
                passData.material = _material;
                passData.occupancy = occupancy;
                passData.occupancySize = new Vector4(width, height, 1f / width, 1f / height);
                passData.origin = new Vector4(origin.x, origin.y, axis.x, axis.y);
                passData.rayParams = new Vector4(span, rayReach, 1f / _size, RayStep);
                passData.screen = _proj * _view;
                passData.shadowParams = new Vector4(1f, EdgeBias, span, rayReach);
                passData.shadowSize = new Vector4(width, height, _size, 0f);

                builder.UseTexture(occupancy, AccessFlags.Read);
                builder.SetRenderAttachment(rays, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(rays, ShadowMapId);

                builder.SetRenderFunc(static (RayData data, RasterGraphContext context) =>
                {
                    Block.Clear();
                    Block.SetTexture(OccupancyId, data.occupancy);
                    Block.SetVector(OccupancySizeId, data.occupancySize);
                    Block.SetVector(RayStartId, data.origin);
                    Block.SetVector(RayParamsId, data.rayParams);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, RayPass,
                        MeshTopology.Triangles, 3, 1, Block);

                    context.cmd.SetGlobalMatrix(ShadowMatrixId, data.screen);
                    context.cmd.SetGlobalVector(ShadowLightId, data.origin);
                    context.cmd.SetGlobalVector(ShadowSizeId, data.shadowSize);
                    context.cmd.SetGlobalVector(ShadowParamsId, data.shadowParams);
                });
            }
        }
    }
}
