using UnityEngine;

namespace MetalRaptors
{
    public static class FlightSteering
    {
        public static float EdgeSteer(float x, float heading, float minX, float maxX,
            float edgeMargin, float maxRate, float pilotRate)
        {
            float leftPen  = minX + edgeMargin - x;
            float rightPen = x - (maxX - edgeMargin);

            float headingX = Mathf.Cos(heading);

            if (leftPen > 0f && headingX < 0f)
            {
                float strength = Mathf.Clamp01(leftPen / edgeMargin);
                return TurnToward(heading, +1f) * maxRate * strength;
            }
            if (rightPen > 0f && headingX > 0f)
            {
                float strength = Mathf.Clamp01(rightPen / edgeMargin);
                return TurnToward(heading, -1f) * maxRate * strength;
            }
            return pilotRate;
        }

        public static float TurnToward(float heading, float targetXDir)
        {
            float target = targetXDir > 0f ? 0f : Mathf.PI;
            float delta = Mathf.DeltaAngle(heading * Mathf.Rad2Deg, target * Mathf.Rad2Deg);
            return Mathf.Sign(delta);
        }
    }
}
