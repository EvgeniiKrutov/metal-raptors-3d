using UnityEngine;

namespace MetalRaptors
{
    public class PlaneFall
    {
        public const float DiveDeg = -38f;
        public const float DiveResponse = 1.1f;
        public const float SpeedGain = 20f;
        public const float RollRateDeg = 230f;
        public const float Timeout = 8f;

        float _heading;
        float _diveDeg;
        float _speed;
        float _roll;

        public float Heading => _heading;
        public float Roll => _roll;

        public static PlaneFall Begin(Rigidbody rb, float heading, float speed)
        {
            var fall = new PlaneFall
            {
                _heading = heading,
                _speed = Mathf.Max(0f, speed),
                _diveDeg = Mathf.Cos(heading) >= 0f ? DiveDeg : 180f - DiveDeg,
            };

            if (rb != null)
            {
                rb.useGravity = false;
                rb.angularVelocity = Vector3.zero;
            }

            return fall;
        }

        public void Step(Rigidbody rb, float dt)
        {
            _heading = Mathf.LerpAngle(_heading * Mathf.Rad2Deg, _diveDeg,
                1f - Mathf.Exp(-DiveResponse * dt)) * Mathf.Deg2Rad;
            _speed += SpeedGain * dt;
            _roll += RollRateDeg * dt;

            if (rb == null) return;

            rb.linearVelocity = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _speed;
        }
    }
}
