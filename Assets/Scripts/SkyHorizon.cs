using UnityEngine;

namespace MetalRaptors
{
    public class SkyHorizon : MonoBehaviour
    {
        const float EyeLevelViewportY = 0.5f;

        static readonly int HorizonSlopeId = Shader.PropertyToID("_HorizonSlope");
        static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");

        Camera _cam;
        Material _sky;
        float _sunViewportX;
        float _sunLift;
        bool _anchorSun;
        bool _eyeLevel;
        float _edgeY;
        float _edgeZ;

        public static void Attach(Camera cam, Material sky, float sunViewportX = 0.5f,
            float sunLift = 0f, bool anchorSun = false,
            float edgeY = ProceduralTerrain.BaseLevel, float edgeZ = 0f)
        {
            var horizon = Create(cam, sky, sunViewportX, sunLift, anchorSun);
            horizon._edgeY = edgeY;
            horizon._edgeZ = edgeZ;
        }

        public static void AtEyeLevel(Camera cam, Material sky, float sunViewportX = 0.5f,
            float sunLift = 0f, bool anchorSun = false)
            => Create(cam, sky, sunViewportX, sunLift, anchorSun)._eyeLevel = true;

        static SkyHorizon Create(Camera cam, Material sky, float sunViewportX,
            float sunLift, bool anchorSun)
        {
            var horizon = new GameObject("Sky Horizon").AddComponent<SkyHorizon>();
            horizon._cam = cam;
            horizon._sky = sky;
            horizon._sunViewportX = sunViewportX;
            horizon._sunLift = sunLift;
            horizon._anchorSun = anchorSun;
            return horizon;
        }

        void LateUpdate()
        {
            if (_cam == null || _sky == null) return;

            float horizonViewportY;

            if (_eyeLevel)
            {
                _sky.SetFloat(HorizonSlopeId, 0f);
                horizonViewportY = EyeLevelViewportY;
            }
            else
            {
                float edgeZ = _edgeZ > 0f ? _edgeZ : ProceduralTerrain.LandEdgeZ;
                Vector3 camPos = _cam.transform.position;
                var edge = new Vector3(camPos.x, _edgeY, edgeZ);
                _sky.SetFloat(HorizonSlopeId,
                    (_edgeY - camPos.y) / Mathf.Max(1f, edgeZ - camPos.z));
                horizonViewportY = _cam.WorldToViewportPoint(edge).y;
            }

            if (!_anchorSun) return;
            _sky.SetVector(SunDirectionId,
                ViewportDirection(_cam, new Vector2(_sunViewportX, horizonViewportY + _sunLift)));
        }

        static Vector3 ViewportDirection(Camera cam, Vector2 viewport)
        {
            if (cam.orthographic) return cam.transform.forward;

            var ndc = new Vector3(viewport.x * 2f - 1f, viewport.y * 2f - 1f, 0.95f);
            Vector3 view = cam.projectionMatrix.inverse.MultiplyPoint(ndc);
            return (cam.transform.rotation * new Vector3(view.x, view.y, -view.z)).normalized;
        }
    }
}
