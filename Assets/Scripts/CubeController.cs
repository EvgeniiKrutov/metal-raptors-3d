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
    ///   * Horizontal wrap         - leaving the right edge re-enters on the left and vice-versa.
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
        float _minX, _maxX, _worldWidth, _ceilingY;

        /// <param name="startHeadingRad">Initial heading in radians (π/2 = straight up).</param>
        public void Initialize(CubeConfig config, float startHeadingRad, float minX, float maxX, float ceilingY)
        {
            _config     = config;
            _heading    = startHeadingRad;
            _minX       = minX;
            _maxX       = maxX;
            _worldWidth = maxX - minX;
            _ceilingY   = ceilingY;

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

            // Clamp to the ceiling and wrap horizontally.
            bool teleport = false;
            if (pos.y > _ceilingY) { pos.y = _ceilingY; teleport = true; }
            if (pos.x > _maxX) { pos.x -= _worldWidth; teleport = true; }
            else if (pos.x < _minX) { pos.x += _worldWidth; teleport = true; }
            if (teleport) _rb.position = pos;
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
