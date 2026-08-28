using UnityEngine;

namespace MetalRaptors
{
    public static class LevelCamera
    {
        public const float BaseFieldOfView = 60f;

        public const float ReferenceAspect = 16f / 9f;

        public const float MobileZoom = 1.15f;

        public static void Frame(Camera cam, float distance,
            out float halfViewWidth, out float halfViewHeight)
        {
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float half = Mathf.Tan(BaseFieldOfView * 0.5f * Mathf.Deg2Rad);

            half = Mathf.Min(half, half * ReferenceAspect / aspect);
            if (GraphicsOptions.Mobile) half /= MobileZoom;

            cam.fieldOfView = 2f * Mathf.Atan(half) * Mathf.Rad2Deg;

            halfViewHeight = distance * half;
            halfViewWidth = halfViewHeight * aspect;
        }
    }
}
