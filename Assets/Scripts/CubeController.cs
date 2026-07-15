using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    /// <summary>
    /// Player-cube flight, ported 1:1 from the metal-raptors sibling repo
    /// (Plane.applyTurnRate + PhysicsSystem.updateFlight):
    ///
    ///   * Constant forward speed - velocity = flySpeed * (cos θ, sin θ), gravity off. Never stops.
    ///   * A / D steer only        - they set the desired turn rate; the actual rate eases in with
    ///                               mass-based lag, so a heavier cube feels sluggish (W/S unused).
    ///   * Soft side boundaries    - nosing into the band near either edge steers the cube back
    ///                               toward the middle, so it banks away instead of leaving the world.
    ///   * Hard ceiling            - the cube cannot climb past the top of the world.
    ///
    /// The cube lives in the XY play-plane (Z frozen); its heading is a rotation about Z.
    /// Hitting the solid ground raises <see cref="OnCrashed"/>; entering the goal trigger raises
    /// <see cref="OnReachedGoal"/>. Both fire once.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CubeController : MonoBehaviour
    {
        public event Action OnCrashed;
        public event Action OnReachedGoal;

        CubeConfig _config;
        Rigidbody _rb;

        float _heading;         // radians; +Y (up) = π/2
        float _angularVelocity; // radians/second, eased toward the desired rate
        bool _active;

        // World bounds, supplied by LevelController at spawn.
        float _minX, _maxX, _worldWidth, _ceilingY, _edgeMargin;

        /// <param name="startHeadingRad">Initial heading in radians (π/2 = straight up).</param>
        /// <param name="edgeMargin">
        /// Width of the soft-boundary band inside each side edge; inside it the cube is steered
        /// back toward the centre so it turns away from the world edge instead of leaving it.
        /// </param>
        public void Initialize(CubeConfig config, float startHeadingRad, float minX, float maxX, float ceilingY, float edgeMargin)
        {
            _config     = config;
            _heading    = startHeadingRad;
            _minX       = minX;
            _maxX       = maxX;
            _worldWidth = maxX - minX;
            _ceilingY   = ceilingY;
            _edgeMargin = edgeMargin;

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;
            _rb.mass = Mathf.Max(0.0001f, config.mass);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ApplyRotation();
            _active = true;
        }

        /// <summary>Freezes the cube (used when the level ends in a crash or a win).</summary>
        public void Stop()
        {
            _active = false;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
        }

        void FixedUpdate()
        {
            if (!_active || _config == null) return;

            float dt = Time.fixedDeltaTime;

            // ---- Steering (sibling Plane.applyTurnRate) ----
            var kb = Keyboard.current;
            bool left  = kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed);
            bool right = kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed);

            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad;              // rad/s
            float desiredRate = (left ? maxRate : 0f) - (right ? maxRate : 0f); // A = turn left (CCW), D = right (CW)

            // ---- Soft side boundaries ----
            // Near either edge, override the pilot's input and turn the cube back toward the
            // middle, so it banks away from the world edge instead of flying off it.
            desiredRate = EdgeSteer(_rb.position.x, maxRate, desiredRate);

            float approach = 1f - Mathf.Exp(-(_config.turnResponsiveness / _rb.mass) * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
            ApplyRotation();

            // ---- Constant-speed forward flight (sibling PhysicsSystem.updateFlight) ----
            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _config.flySpeed;

            Vector3 pos = _rb.position;

            // Hard ceiling: allow steering along it, but no further climb.
            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            _rb.linearVelocity = vel;

            // Clamp to the ceiling; the sides are handled by the soft boundary above.
            if (pos.y > _ceilingY) { pos.y = _ceilingY; _rb.position = pos; }
        }

        /// <summary>
        /// Soft side boundary: while the cube is inside the <see cref="_edgeMargin"/> band at an
        /// edge and its heading still carries it toward that edge, force the desired turn rate to
        /// bank it back toward the world centre. The forcing scales with how deep it has pushed
        /// into the band (0 at the band's inner lip, full rate at the very edge), so a shallow
        /// intrusion is nudged and a deep one is turned hard — it always comes about before
        /// leaving the world. Outside the bands the pilot's own <paramref name="pilotRate"/> is
        /// returned unchanged.
        /// </summary>
        float EdgeSteer(float x, float maxRate, float pilotRate)
        {
            // Signed depth into a band: >0 means inside it. Only one side can be active at once.
            float leftPen  = _minX + _edgeMargin - x; // how far past the left band's inner lip
            float rightPen = x - (_maxX - _edgeMargin); // ...and the right band's

            // Direction of travel in X: cos(heading) > 0 heads right (+X), < 0 heads left (-X).
            float headingX = Mathf.Cos(_heading);

            // Near the left edge and still drifting left -> turn toward +X (down from straight up
            // is CW = negative rate; but which way is "toward centre" depends on the current
            // heading, so steer by the shortest turn that flips headingX to point inward).
            if (leftPen > 0f && headingX < 0f)
            {
                float strength = Mathf.Clamp01(leftPen / _edgeMargin);
                return TurnToward(+1f) * maxRate * strength;
            }
            if (rightPen > 0f && headingX > 0f)
            {
                float strength = Mathf.Clamp01(rightPen / _edgeMargin);
                return TurnToward(-1f) * maxRate * strength;
            }
            return pilotRate;
        }

        /// <summary>
        /// Sign of the turn rate that rotates the current heading toward pointing in
        /// <paramref name="targetXDir"/> (+1 = +X, -1 = -X) along the shorter arc. Positive is
        /// CCW (matches A/left); negative is CW (matches D/right).
        /// </summary>
        float TurnToward(float targetXDir)
        {
            // Target heading is 0 (points +X) or π (points -X). Wrap the difference to (-π, π]
            // and steer by its sign so we always take the short way round.
            float target = targetXDir > 0f ? 0f : Mathf.PI;
            float delta = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, target * Mathf.Rad2Deg);
            return Mathf.Sign(delta);
        }

        void ApplyRotation()
        {
            // Heading is an angle in the XY plane -> rotation about Z (the axis the camera looks down).
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_active) OnCrashed?.Invoke();
        }

        void OnTriggerEnter(Collider other)
        {
            if (_active) OnReachedGoal?.Invoke();
        }
    }
}
