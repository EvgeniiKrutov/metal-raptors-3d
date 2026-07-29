using UnityEngine;

namespace MetalRaptors
{
    public class SkyHorizon : MonoBehaviour
    {
        Camera _cam;
        Material _sky;
        float _sunViewportX;
        float _sunLift;
        bool _anchorSun;

        public static void Attach(Camera cam, Material sky, float sunViewportX = 0.5f,
            float sunLift = 0f, bool anchorSun = false)
        {
            var horizon = new GameObject("Sky Horizon").AddComponent<SkyHorizon>();
            horizon._cam = cam;
            horizon._sky = sky;
            horizon._sunViewportX = sunViewportX;
            horizon._sunLift = sunLift;
            horizon._anchorSun = anchorSun;
        }

        void LateUpdate()
        {
            if (_cam == null || _sky == null) return;

            Vector3 camPos = _cam.transform.position;
            var edge = new Vector3(camPos.x, ProceduralTerrain.BaseLevel, ProceduralTerrain.Depth);
            _sky.SetFloat("_HorizonLevel", (edge - camPos).normalized.y);

            if (!_anchorSun) return;
            float edgeViewportY = _cam.WorldToViewportPoint(edge).y;
            Vector3 sunDir = _cam.ViewportPointToRay(
                new Vector3(_sunViewportX, edgeViewportY + _sunLift, 1f)).direction;
            _sky.SetVector("_SunDirection", sunDir);
        }
    }
}
