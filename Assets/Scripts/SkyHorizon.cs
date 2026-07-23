using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Glues the gradient skybox's horizon band to the map's fogged far edge: every frame it
    /// recomputes the direction from the (never-rotating, but vertically moving) camera to
    /// the far-edge line and feeds it to the sky material's _HorizonLevel, so the land's
    /// visible edge and the sky's horizon are the same line at any flight altitude. With
    /// <c>anchorSun</c> it also re-aims the sun to ride just above that line, so a low sun
    /// sets/dawns at the actual horizon. Details: docs/atmospheres.md.
    /// </summary>
    public class SkyHorizon : MonoBehaviour
    {
        Camera _cam;
        Material _sky;
        float _sunViewportX;
        float _sunLift;
        bool _anchorSun;

        /// <summary>Creates the tracker for <paramref name="sky"/>. <paramref name="sunViewportX"/>
        /// is the sun's screen column, <paramref name="sunLift"/> how far above the horizon the
        /// disc rides (viewport fraction); both only matter with <paramref name="anchorSun"/>.</summary>
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

            // The perceived horizon: the terrain's mean-height line at the land's far edge,
            // fully fogged there, so the band blends into it without a seam.
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
