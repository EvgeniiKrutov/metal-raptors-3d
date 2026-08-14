using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    [RequireComponent(typeof(Camera))]
    public class GroundHaze : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_GroundHazeColor");
        static readonly int BandId = Shader.PropertyToID("_GroundHazeBand");
        static readonly int DepthId = Shader.PropertyToID("_GroundHazeDepth");
        static readonly int EyeId = Shader.PropertyToID("_GroundHazeEye");
        static readonly int RayCornerId = Shader.PropertyToID("_GroundHazeRayCorner");
        static readonly int RayRightId = Shader.PropertyToID("_GroundHazeRayRight");
        static readonly int RayUpId = Shader.PropertyToID("_GroundHazeRayUp");

        Camera _cam;
        Material _mat;
        HazePass _pass;
        Vector4 _band;
        float _fromZ, _fullZ;

        // fromZ / fullZ are world Z planes, turned into eye depth each frame — every camera in
        // this game looks down +Z with an identity rotation, so the two differ only by the eye.
        public static GroundHaze Attach(Camera cam, float bandTop, float bandClear,
            float strength, float fromZ, float fullZ)
        {
            var shader = Shader.Find("Hidden/GroundHaze");
            if (shader == null)
            {
                Debug.LogWarning("GroundHaze: Hidden/GroundHaze not found; no valley mist.");
                return null;
            }

            var stale = cam.GetComponent<GroundHaze>();
            if (stale != null)
            {
                stale.enabled = false;
                Destroy(stale);
            }

            var haze = cam.gameObject.AddComponent<GroundHaze>();
            haze._cam = cam;
            haze._mat = new Material(shader) { name = "Ground Haze (runtime)" };
            haze._band = new Vector4(bandTop, bandClear, strength, 0f);
            haze._fromZ = fromZ;
            haze._fullZ = fullZ;
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
            if (cam != _cam || _mat == null || _pass == null) return;
            if (_band.z <= 0f || _fullZ - _fromZ < 1e-3f) return;

            float eyeZ = _cam.transform.position.z;
            _mat.SetColor(ColorId, RenderSettings.fogColor);
            _mat.SetVector(BandId, _band);
            _mat.SetVector(DepthId, new Vector4(_fromZ - eyeZ, _fullZ - eyeZ, 0f, 0f));
            SetViewRays();

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.scriptableRenderer.EnqueuePass(_pass);
        }

        void SetViewRays()
        {
            Vector3 eye = _cam.transform.position;
            Vector3 corner = _cam.ViewportToWorldPoint(new Vector3(0f, 0f, 1f)) - eye;
            Vector3 right = _cam.ViewportToWorldPoint(new Vector3(1f, 0f, 1f)) - eye - corner;
            Vector3 up = _cam.ViewportToWorldPoint(new Vector3(0f, 1f, 1f)) - eye - corner;

            _mat.SetVector(EyeId, eye);
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
                // One event after AerialHaze, so the mist is not brightened back out by it.
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 1;
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

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Ground Haze", out var passData))
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
