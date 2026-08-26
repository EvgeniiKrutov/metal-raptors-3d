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

        const float ReverseGuard = 0.15f;

        public static float SteerToHeading(float heading, float target, float maxRate,
            float lag, float angularVelocity)
        {
            if (lag <= 0f) return 0f;

            float error = Mathf.DeltaAngle(heading * Mathf.Rad2Deg, target * Mathf.Rad2Deg)
                          * Mathf.Deg2Rad;

            if (Mathf.PI - Mathf.Abs(error) < ReverseGuard && Mathf.Abs(angularVelocity) > 1e-3f)
                error = Mathf.Sign(angularVelocity) * Mathf.Abs(error);

            float residual = error - angularVelocity * lag;
            return Mathf.Clamp(residual / lag, -maxRate, maxRate);
        }

        public static float TurnToward(float heading, float targetXDir)
        {
            float target = targetXDir > 0f ? 0f : Mathf.PI;
            float delta = Mathf.DeltaAngle(heading * Mathf.Rad2Deg, target * Mathf.Rad2Deg);
            return Mathf.Sign(delta);
        }

        const float PushWeight = 2f;

        public static float Contain(float heading, Vector2 pos,
            float minX, float maxX, float sideMargin,
            float floorY, float floorMargin,
            float ceilingY, float ceilingMargin)
        {
            float push = Band(minX + sideMargin - pos.x, sideMargin)
                       - Band(pos.x - (maxX - sideMargin), sideMargin);
            float lift = Band(floorY - pos.y, floorMargin)
                       - Band(pos.y - (ceilingY - ceilingMargin), ceilingMargin);

            if (push == 0f && lift == 0f) return heading;

            var dir = new Vector2(Mathf.Cos(heading) + push * PushWeight,
                                  Mathf.Sin(heading) + lift * PushWeight);
            return dir.sqrMagnitude > 1e-6f ? Mathf.Atan2(dir.y, dir.x) : heading;
        }

        static float Band(float penetration, float width)
        {
            return width > 0f ? Mathf.Clamp01(penetration / width) : 0f;
        }
    }
}
