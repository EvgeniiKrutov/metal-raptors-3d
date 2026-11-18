using UnityEngine;

namespace MetalRaptors
{
    public static class LevelScroll
    {
        public static float Sign { get; private set; } = 1f;

        public static float Velocity { get; private set; }

        public static float Lookback { get; private set; }

        public static void Reset()
        {
            Sign = 1f;
            Velocity = 0f;
            Lookback = 0f;
        }

        public static void Face(float sign) => Sign = sign < 0f ? -1f : 1f;

        public static void AllowLookback(float distance) => Lookback = Mathf.Max(0f, distance);

        public static void Track(float velocity) => Velocity = velocity;

        public static float Drift(float leftSpeed) => -leftSpeed - Velocity;

        public static float Pace(float leftSpeed, float minDrift)
        {
            if (Mathf.Abs(Drift(leftSpeed)) >= minDrift) return leftSpeed;

            float faster = minDrift - Velocity;
            float slower = -minDrift - Velocity;
            if (slower <= 0f) return faster;
            return Mathf.Abs(faster - leftSpeed) <= Mathf.Abs(slower - leftSpeed) ? faster : slower;
        }
    }
}
