using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    [RequireComponent(typeof(Camera))]
    public class ShotMist : MonoBehaviour
    {
        const int GroundRes = 256;

        static readonly int ColorId = Shader.PropertyToID("_ShotMistColor");
        static readonly int BandId = Shader.PropertyToID("_ShotMistBand");
        static readonly int DepthId = Shader.PropertyToID("_ShotMistDepth");
        static readonly int PatchId = Shader.PropertyToID("_ShotMistPatch");
        static readonly int GroundId = Shader.PropertyToID("_ShotMistGround");
        static readonly int GroundRectId = Shader.PropertyToID("_ShotMistGroundRect");
        static readonly int EyeId = Shader.PropertyToID("_ShotMistEye");
        static readonly int RayCornerId = Shader.PropertyToID("_ShotMistRayCorner");
        static readonly int RayRightId = Shader.PropertyToID("_ShotMistRayRight");
        static readonly int RayUpId = Shader.PropertyToID("_ShotMistRayUp");

        Camera _cam;
        Material _mat;
        Texture2D _ground;
        MistPass _pass;

        public static ShotMist Attach(Camera cam, Func<float, float, float> groundY, Rect area,
            Color color, Vector4 band, Vector4 depth, Vector4 patch)
        {
            var shader = Shader.Find("Hidden/ShotMist");
            if (shader == null)
            {
                Debug.LogWarning("ShotMist: Hidden/ShotMist not found; no ground mist.");
                return null;
            }

            var mist = cam.gameObject.AddComponent<ShotMist>();
            mist._cam = cam;
            mist._ground = BakeGround(groundY, area);
            mist._mat = new Material(shader) { name = "Shot Mist (runtime)" };
            mist._mat.SetColor(ColorId, color);
            mist._mat.SetVector(BandId, band);
            mist._mat.SetVector(DepthId, depth);
            mist._mat.SetVector(PatchId, patch);
            mist._mat.SetTexture(GroundId, mist._ground);
            mist._mat.SetVector(GroundRectId, new Vector4(area.x, area.y, area.width, area.height));
            mist._pass = new MistPass(mist._mat);
            return mist;
        }

        static Texture2D BakeGround(Func<float, float, float> groundY, Rect area)
        {
            var texture = new Texture2D(GroundRes, GroundRes, TextureFormat.RFloat, false, true)
            {
                name = "Shot Mist Ground",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var heights = new Color[GroundRes * GroundRes];
            for (int iz = 0; iz < GroundRes; iz++)
            {
                float z = area.y + area.height * (iz + 0.5f) / GroundRes;
                for (int ix = 0; ix < GroundRes; ix++)
                {
                    float x = area.x + area.width * (ix + 0.5f) / GroundRes;
                    heights[iz * GroundRes + ix] = new Color(groundY(x, z), 0f, 0f, 0f);
                }
            }

            texture.SetPixels(heights);
            texture.Apply(false, true);
            return texture;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_ground != null) Destroy(_ground);
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _cam || _mat == null || _pass == null) return;

            Vector3 eye = _cam.transform.position;
            Vector3 corner = _cam.ViewportToWorldPoint(new Vector3(0f, 0f, 1f)) - eye;
            Vector3 right = _cam.ViewportToWorldPoint(new Vector3(1f, 0f, 1f)) - eye - corner;
            Vector3 up = _cam.ViewportToWorldPoint(new Vector3(0f, 1f, 1f)) - eye - corner;
            _mat.SetVector(EyeId, eye);
            _mat.SetVector(RayCornerId, corner);
            _mat.SetVector(RayRightId, right);
            _mat.SetVector(RayUpId, up);

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.scriptableRenderer.EnqueuePass(_pass);
        }

        class MistPass : ScriptableRenderPass
        {
            static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

            readonly Material _material;

            public MistPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 2;
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

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Shot Mist", out var passData))
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
