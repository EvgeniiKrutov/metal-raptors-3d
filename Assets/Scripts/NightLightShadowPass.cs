using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class NightLightShadowPass : ScriptableRenderPass
    {
        public const int MaxRows = NightLights.MaxShadowLights;

        public static readonly int ShadowMapId = Shader.PropertyToID("_NL_ShadowMap");
        public static readonly int ShadowMatrixId = Shader.PropertyToID("_NL_ShadowMatrix");
        public static readonly int ShadowParamsId = Shader.PropertyToID("_NL_ShadowParams");
        public static readonly int ShadowSizeId = Shader.PropertyToID("_NL_ShadowSize");
        public static readonly int ShadowRowsId = Shader.PropertyToID("_NL_ShadowRows");
        public static readonly int ShadowSpansId = Shader.PropertyToID("_NL_ShadowSpans");

        static readonly int CasterVPId = Shader.PropertyToID("_NL_CasterVP");
        static readonly int CasterLightsId = Shader.PropertyToID("_NL_CasterLights");
        static readonly int CasterConesId = Shader.PropertyToID("_NL_CasterCones");
        static readonly int CasterNearId = Shader.PropertyToID("_NL_CasterNear");
        static readonly int CasterCountId = Shader.PropertyToID("_NL_CasterCount");
        static readonly int OccupancyId = Shader.PropertyToID("_NL_Occupancy");
        static readonly int OccupancySizeId = Shader.PropertyToID("_NL_OccupancySize");
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
        const float Darkness = 0.85f;

        struct Row
        {
            public Vector3 position;
            public Vector2 direction;
            public Vector2 originNdc;
            public Vector2 tipNdc;
            public float range;
            public float outer;
            public float source;
            public float near;
        }

        readonly Material _material;
        readonly bool _raysSupported;
        readonly NightLight2D[] _picks = new NightLight2D[MaxRows];
        readonly Row[] _rows = new Row[MaxRows];

        Camera _camera;
        Matrix4x4 _view;
        Matrix4x4 _proj;
        int _count;
        int _size;

        public NightLightShadowPass(Material material)
        {
            _material = material;
            _raysSupported = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render);
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
        }

        class OccupancyData
        {
            public RendererListHandle casters;
            public Matrix4x4 casterVP;
            public readonly Vector4[] lights = new Vector4[MaxRows];
            public readonly Vector4[] cones = new Vector4[MaxRows];
            public Vector4 near;
            public int count;
        }

        class RayData
        {
            public Material material;
            public TextureHandle occupancy;
            public Vector4 occupancySize;
            public Vector4 rayParams;
            public readonly Vector4[] starts = new Vector4[MaxRows];
            public readonly Vector4[] spans = new Vector4[MaxRows];
            public Matrix4x4 screen;
            public Vector4 shadowParams;
            public Vector4 shadowSize;
        }

        public bool Prepare(Camera cam, NightLightingTier tier, int budget)
        {
            _camera = null;
            _count = 0;

            if (_material == null || !_raysSupported || cam == null || tier == null
                || tier.shadowMapSize <= 0 || budget <= 0)
            {
                NightLights.AssignShadowRows(_picks, 0);
                return false;
            }

            _view = cam.worldToCameraMatrix;
            _proj = cam.projectionMatrix;
            Matrix4x4 screen = _proj * _view;

            Rect area = NightLights.PlaneArea(cam, NightLights.PlaneZ, 1f);
            int picked = NightLights.PickShadowLights(area, NightLights.PlaneZ, budget, _picks);

            for (int i = 0; i < picked; i++)
            {
                NightLight2D light = _picks[i];
                var row = new Row
                {
                    position = light.transform.position,
                    direction = light.PlaneDirection,
                    range = Mathf.Max(1f, light.ShadowRange),
                    outer = Mathf.Clamp(light.outerAngle, 0.2f, 179f),
                    source = Mathf.Max(0f, light.sourceRadius),
                    near = Mathf.Max(0f, light.shadowNear),
                };

                var forward = new Vector3(row.direction.x, row.direction.y, 0f);
                if (!ToNdc(screen, row.position, out row.originNdc)
                    || !ToNdc(screen, row.position + forward * row.range, out row.tipNdc))
                    continue;

                _rows[_count] = row;
                _picks[_count] = light;
                _count++;
            }

            NightLights.AssignShadowRows(_picks, _count);
            if (_count == 0) return false;

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
            if (_camera == null || _count == 0) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            RenderTextureDescriptor target = cameraData.cameraTargetDescriptor;

            int height = Mathf.Clamp(target.height, 1, _size);
            int width = Mathf.Max(1, Mathf.RoundToInt(height * (float)target.width / Mathf.Max(1, target.height)));
            var size = new Vector2(width, height);

            var occupancyDesc = new TextureDesc(width, height)
            {
                name = "_NL_Occupancy",
                format = GraphicsFormat.R8G8B8A8_UNorm,
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
                passData.count = _count;
                passData.near = Vector4.zero;
                for (int i = 0; i < MaxRows; i++)
                {
                    if (i >= _count)
                    {
                        passData.lights[i] = Vector4.zero;
                        passData.cones[i] = Vector4.zero;
                        continue;
                    }

                    Row row = _rows[i];
                    passData.lights[i] = new Vector4(row.position.x, row.position.y, row.position.z, row.source);
                    passData.cones[i] = new Vector4(row.direction.x, row.direction.y,
                        Mathf.Tan(row.outer * 0.5f * Mathf.Deg2Rad), row.range);
                    passData.near[i] = row.near;
                }

                builder.UseRendererList(casters);
                builder.SetRenderAttachment(occupancy, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (OccupancyData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalMatrix(CasterVPId, data.casterVP);
                    context.cmd.SetGlobalVectorArray(CasterLightsId, data.lights);
                    context.cmd.SetGlobalVectorArray(CasterConesId, data.cones);
                    context.cmd.SetGlobalVector(CasterNearId, data.near);
                    context.cmd.SetGlobalFloat(CasterCountId, data.count);
                    context.cmd.DrawRendererList(data.casters);
                });
            }

            var rayDesc = new TextureDesc(_size, _count)
            {
                name = "_NL_ShadowMap",
                format = GraphicsFormat.R16G16B16A16_SFloat,
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
                passData.rayParams = new Vector4(1f / _size, RayStep, 0f, 0f);
                passData.screen = _proj * _view;
                passData.shadowParams = new Vector4(_count, EdgeBias, Darkness, 0f);
                passData.shadowSize = new Vector4(width, height, _size, _count);

                for (int i = 0; i < MaxRows; i++)
                {
                    if (i >= _count)
                    {
                        passData.starts[i] = Vector4.zero;
                        passData.spans[i] = Vector4.zero;
                        continue;
                    }

                    Row row = _rows[i];
                    Vector2 origin = ToPixels(row.originNdc, size);
                    Vector2 axis = ToPixels(row.tipNdc, size) - origin;
                    float reach = axis.magnitude;
                    axis = reach > 1e-3f ? axis / reach : Vector2.right;

                    float corner = Mathf.Max(
                        Mathf.Max(origin.magnitude, (origin - new Vector2(width, 0f)).magnitude),
                        Mathf.Max((origin - new Vector2(0f, height)).magnitude, (origin - size).magnitude));
                    float rayReach = Mathf.Max(1f, Mathf.Min(corner, reach * ReachScale));
                    float span = Mathf.Min(359f, row.outer + SpanMargin * 2f) * Mathf.Deg2Rad;

                    passData.starts[i] = new Vector4(origin.x, origin.y, axis.x, axis.y);
                    passData.spans[i] = new Vector4(span, rayReach, 0f, 0f);
                }

                builder.UseTexture(occupancy, AccessFlags.Read);
                builder.SetRenderAttachment(rays, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(rays, ShadowMapId);

                builder.SetRenderFunc(static (RayData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVectorArray(ShadowRowsId, data.starts);
                    context.cmd.SetGlobalVectorArray(ShadowSpansId, data.spans);

                    Block.Clear();
                    Block.SetTexture(OccupancyId, data.occupancy);
                    Block.SetVector(OccupancySizeId, data.occupancySize);
                    Block.SetVector(RayParamsId, data.rayParams);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, RayPass,
                        MeshTopology.Triangles, 3, 1, Block);

                    context.cmd.SetGlobalMatrix(ShadowMatrixId, data.screen);
                    context.cmd.SetGlobalVector(ShadowSizeId, data.shadowSize);
                    context.cmd.SetGlobalVector(ShadowParamsId, data.shadowParams);
                });
            }
        }
    }
}
