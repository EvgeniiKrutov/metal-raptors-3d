using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// The soft side-boundary steering shared by the player (<see cref="CubeController"/>) and
    /// the enemy fighters (<see cref="EnemyController"/>). Headings are radians in the XY
    /// play-plane, +Y (up) = π/2, matching both controllers.
    /// </summary>
    public static class FlightSteering
    {
        /// <summary>
        /// Soft side boundary: while the plane is inside the <paramref name="edgeMargin"/> band
        /// at an edge and its heading still carries it toward that edge, force the desired turn
        /// rate to bank it back toward the world centre. The forcing scales with how deep it has
        /// pushed into the band (0 at the band's inner lip, full rate at the very edge), so a
        /// shallow intrusion is nudged and a deep one is turned hard — it always comes about
        /// before leaving the world. Outside the bands the caller's own
        /// <paramref name="pilotRate"/> is returned unchanged.
        /// </summary>
        public static float EdgeSteer(float x, float heading, float minX, float maxX,
            float edgeMargin, float maxRate, float pilotRate)
        {
            // Signed depth into a band: >0 means inside it. Only one side can be active at once.
            float leftPen  = minX + edgeMargin - x;   // how far past the left band's inner lip
            float rightPen = x - (maxX - edgeMargin); // ...and the right band's

            // Direction of travel in X: cos(heading) > 0 heads right (+X), < 0 heads left (-X).
            float headingX = Mathf.Cos(heading);

            // Near the left edge and still drifting left -> turn toward +X (down from straight up
            // is CW = negative rate; but which way is "toward centre" depends on the current
            // heading, so steer by the shortest turn that flips headingX to point inward).
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

        /// <summary>
        /// Sign of the turn rate that rotates <paramref name="heading"/> toward pointing in
        /// <paramref name="targetXDir"/> (+1 = +X, -1 = -X) along the shorter arc. Positive is
        /// CCW (matches A/left); negative is CW (matches D/right).
        /// </summary>
        public static float TurnToward(float heading, float targetXDir)
        {
            // Target heading is 0 (points +X) or π (points -X). Wrap the difference to (-π, π]
            // and steer by its sign so we always take the short way round.
            float target = targetXDir > 0f ? 0f : Mathf.PI;
            float delta = Mathf.DeltaAngle(heading * Mathf.Rad2Deg, target * Mathf.Rad2Deg);
            return Mathf.Sign(delta);
        }
    }
}
