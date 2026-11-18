using UnityEngine;

namespace MetalRaptors
{
    public static class FlightSmoothing
    {
        public const float TurnAccelFactor = 3f;
        public const float LeanMaxDeg = 14f;
        public const float LeanResponse = 4f;
        public const float RollRamp = 0.2f;

        public static float StepTurnRate(float current, float desired, float response, float maxRate, float dt)
        {
            float delta = (desired - current) * (1f - Mathf.Exp(-response * dt));
            float cap = TurnAccelFactor * Mathf.Abs(maxRate) * dt;
            return current + Mathf.Clamp(delta, -cap, cap);
        }

        public static float StepLean(float leanDeg, float angularVelocity, float maxRate, float flipDeg, float dt)
        {
            float share = maxRate > 1e-4f ? Mathf.Clamp(-angularVelocity / maxRate, -1f, 1f) : 0f;
            float target = share * LeanMaxDeg * Mathf.Cos(flipDeg * Mathf.Deg2Rad);
            return leanDeg + (target - leanDeg) * (1f - Mathf.Exp(-LeanResponse * dt));
        }

        public static float RollProfile(float u)
        {
            u = Mathf.Clamp01(u);
            float peak = 1f / (1f - RollRamp);
            if (u < RollRamp) return 0.5f * peak * u * u / RollRamp;
            if (u > 1f - RollRamp)
            {
                float v = 1f - u;
                return 1f - 0.5f * peak * v * v / RollRamp;
            }
            return peak * (u - 0.5f * RollRamp);
        }
    }
}
