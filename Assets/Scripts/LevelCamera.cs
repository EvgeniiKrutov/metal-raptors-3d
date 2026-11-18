using UnityEngine;

namespace MetalRaptors
{
    public static class LevelCamera
    {
        public const float BaseFieldOfView = 60f;

        public const float ReferenceAspect = 16f / 9f;

        public const float MobileZoom = 1.15f;

        public const float FightZoomOut = 1.1f;

        public static void Frame(Camera cam, float distance,
            out float halfViewWidth, out float halfViewHeight)
            => Frame(cam, distance, 1f, out halfViewWidth, out halfViewHeight);

        public static void Frame(Camera cam, float distance, float zoomOut,
            out float halfViewWidth, out float halfViewHeight)
        {
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float half = HalfTan(aspect, zoomOut);

            cam.fieldOfView = 2f * Mathf.Atan(half) * Mathf.Rad2Deg;

            halfViewHeight = distance * half;
            halfViewWidth = halfViewHeight * aspect;
        }

        public static float HalfHeight(Camera cam, float distance) => HalfHeight(cam, distance, 1f);

        public static float HalfHeight(Camera cam, float distance, float zoomOut) =>
            distance * HalfTan(Mathf.Max(0.1f, cam.aspect), zoomOut);

        static float HalfTan(float aspect, float zoomOut)
        {
            float half = Mathf.Tan(BaseFieldOfView * 0.5f * Mathf.Deg2Rad);

            half = Mathf.Min(half, half * ReferenceAspect / aspect);
            if (GraphicsOptions.Mobile) half /= MobileZoom;
            return half * Mathf.Max(0.01f, zoomOut);
        }
    }
}
