using UnityEngine;

namespace MetalRaptors
{
    public static class Gunnery
    {
        public static Vector3 Intercept(Vector3 muzzle, Rigidbody target, float bulletSpeed,
            float lead)
        {
            if (target == null) return muzzle;

            Vector3 point = target.position;
            Vector3 velocity = target.linearVelocity;

            float t = 0f;
            for (int i = 0; i < 2; i++)
            {
                float distance = Vector3.Distance(muzzle, point + velocity * (t * lead));
                t = bulletSpeed > 0f ? distance / bulletSpeed : 0f;
            }
            return point + velocity * (t * lead);
        }

        public static bool OnCamera(Camera cam, Vector3 worldPoint)
        {
            if (cam == null) return true;

            Vector3 vp = cam.WorldToViewportPoint(worldPoint);
            return vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }
    }
}
