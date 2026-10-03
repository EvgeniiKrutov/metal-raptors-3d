#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class NightLightDebugPass : ScriptableRenderPass
    {
        static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int RayOriginId = Shader.PropertyToID("_NL_RayOrigin");
        static readonly int RayCornerId = Shader.PropertyToID("_NL_RayCorner");
        static readonly int RayRightId = Shader.PropertyToID("_NL_RayRight");
        static readonly int RayUpId = Shader.PropertyToID("_NL_RayUp");
        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        readonly Material _material;

        public NightLightDebugPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        class PassData
        {
            public Material material;
            public Vector4 origin;
            public Vector4 corner;
            public Vector4 right;
            public Vector4 up;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || _material == null) return;

            Camera cam = frameData.Get<UniversalCameraData>().camera;
            if (cam == null) return;

            Vector3 eye = cam.transform.position;
            Vector3 corner = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 1f)) - eye;
            Vector3 right = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 1f)) - eye - corner;
            Vector3 up = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 1f)) - eye - corner;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("NightLightMask Overlay", out var passData))
            {
                passData.material = _material;
                passData.origin = eye;
                passData.corner = corner;
                passData.right = right;
                passData.up = up;

                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    Block.Clear();
                    Block.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    Block.SetVector(RayOriginId, data.origin);
                    Block.SetVector(RayCornerId, data.corner);
                    Block.SetVector(RayRightId, data.right);
                    Block.SetVector(RayUpId, data.up);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0,
                        MeshTopology.Triangles, 3, 1, Block);
                });
            }
        }
    }
}
#endif
