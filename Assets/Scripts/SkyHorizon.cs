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
            float edgeY = ProceduralTerrain.BaseLevel, float edgeZ = ProceduralTerrain.Depth)
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
                Vector3 camPos = _cam.transform.position;
                var edge = new Vector3(camPos.x, _edgeY, _edgeZ);
                _sky.SetFloat(HorizonSlopeId,
                    (_edgeY - camPos.y) / Mathf.Max(1f, _edgeZ - camPos.z));
                horizonViewportY = _cam.WorldToViewportPoint(edge).y;
            }

            if (!_anchorSun) return;
            Vector3 sunDir = _cam.ViewportPointToRay(
                new Vector3(_sunViewportX, horizonViewportY + _sunLift, 1f)).direction;
            _sky.SetVector(SunDirectionId, sunDir);
        }
    }
}
