using UnityEngine;

namespace MetalRaptors
{
    public struct PlaneEnergy
    {
        public const float StallFraction = 0.75f;
        public const float StallPitchDeg = 30f;
        public const float NoseDownDeg = 70f;

        public float Speed;

        public void Reset(float cruise) => Speed = cruise;

        public float Step(float headingRad, float cruise, float gravity, float drag,
            float minSpeed, float maxSpeed, float dt)
        {
            Speed += -Mathf.Sin(headingRad) * gravity * dt;
            Speed -= (Speed - cruise) * drag * dt;
            Speed = Mathf.Clamp(Speed, minSpeed, Mathf.Max(minSpeed, maxSpeed));
            return Speed;
        }

        public static float Strength(float headingRad, float speed, float cruise, float minFraction)
        {
            if (cruise <= 0f) return 0f;
            if (speed >= StallFraction * cruise) return 0f;
            if (Mathf.Sin(headingRad) <= Mathf.Sin(StallPitchDeg * Mathf.Deg2Rad)) return 0f;

            return Mathf.InverseLerp(StallFraction * cruise, minFraction * cruise, speed);
        }

        public static float NoseDownRate(float headingRad, float speed, float cruise, float minFraction)
        {
            float strength = Strength(headingRad, speed, cruise, minFraction);
            if (strength <= 0f) return 0f;

            float rate = strength * NoseDownDeg * Mathf.Deg2Rad;
            return Mathf.Cos(headingRad) >= 0f ? -rate : rate;
        }
    }
}
