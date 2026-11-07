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
        public const float HomeResponse = 2.4f;
        public const float HomeGain = 70f;
        public const float HomeTopSpeed = 430f;
        public const float HomeLock = 150f;
        const float HomeMinEta = 0.2f;

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

        public void Home(Rigidbody rb, Vector3 target, float dt)
        {
            Vector3 from = rb != null ? rb.position : target;
            Vector2 gap = new Vector2(target.x - from.x, target.y - from.y);
            float want = Mathf.Atan2(gap.y, gap.x) * Mathf.Rad2Deg;
            _heading = gap.sqrMagnitude <= HomeLock * HomeLock
                ? want * Mathf.Deg2Rad
                : Mathf.LerpAngle(_heading * Mathf.Rad2Deg, want,
                    1f - Mathf.Exp(-HomeResponse * dt)) * Mathf.Deg2Rad;
            _speed = Mathf.Min(_speed + HomeGain * dt, Mathf.Max(_speed, HomeTopSpeed));
            _roll += RollRateDeg * dt;

            if (rb == null) return;

            float eta = Mathf.Max(HomeMinEta, gap.magnitude / Mathf.Max(1f, _speed));
            rb.linearVelocity = new Vector3(Mathf.Cos(_heading) * _speed, Mathf.Sin(_heading) * _speed,
                (target.z - from.z) / eta);
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
