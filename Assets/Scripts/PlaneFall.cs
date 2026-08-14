using UnityEngine;

namespace MetalRaptors
{
    public static class PlaneFall
    {
        public const float Gravity = 150f;
        public const float InitialDrop = 25f;
        public const float HorizontalDrag = 1.5f;
        public const float SpinRate = 2.5f;
        public const float Timeout = 8f;

        public static void Begin(Rigidbody rb)
        {
            if (rb == null) return;

            rb.useGravity = false;
            Vector3 v = rb.linearVelocity;
            v.y -= InitialDrop;
            rb.linearVelocity = v;
            rb.angularVelocity = new Vector3(0f, 0f,
                (Random.value < 0.5f ? -1f : 1f) * SpinRate);
        }

        public static void Step(Rigidbody rb, float dt)
        {
            if (rb == null) return;

            Vector3 v = rb.linearVelocity;
            v.y -= Gravity * dt;
            v.x = Mathf.MoveTowards(v.x, 0f, Mathf.Abs(v.x) * HorizontalDrag * dt);
            rb.linearVelocity = v;
        }
    }
}
