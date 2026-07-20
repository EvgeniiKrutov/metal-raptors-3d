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
    /// Hitting the solid ground raises <see cref="OnCrashed"/> (once).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CubeController : MonoBehaviour, IDamageable
    {
        public event Action OnCrashed;

        /// <summary>Raised once when health hits zero and the plane starts falling.</summary>
        public event Action OnShotDown;

        /// <summary>Hit points left; starts at <see cref="PlayerConfig.health"/>.</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>Full hit points, for the HUD's health bar.</summary>
        public float MaxHealth { get; private set; }

        // Extra downward pull while shot down, on top of gravity, so the death dive
        // doesn't take ages from high altitude.
        const float FallExtraGravity = 20f;
        const float ExplosionSize = 60f; // matches the plane model's on-screen size

        // A plane-to-plane scrape costs this much health (on both planes) instead of the old
        // ram-kills-both. Planes no longer physically collide (LevelController turns off their
        // mutual collisions and detects the overlap itself), so the cooldown is what keeps one
        // encounter — which spans several frames of overlap — to a single hit.
        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;

        PlayerConfig _config;
        Rigidbody _rb;
        ShakeEffect _shake; // wobbles the visible model on a scrape; the body flies straight on

        float _heading;         // radians; +Y (up) = π/2
        float _angularVelocity; // radians/second, eased toward the desired rate
        bool _active;
        bool _falling;          // health hit zero: gravity owns the plane until it hits something
        float _lastCollisionTime = -999f; // last plane-to-plane scrape, for the collision-damage debounce

        // World bounds, supplied by LevelController at spawn.
        float _minX, _maxX, _worldWidth, _ceilingY, _edgeMargin;

        /// <param name="startHeadingRad">Initial heading in radians (π/2 = straight up).</param>
        /// <param name="edgeMargin">
        /// Width of the soft-boundary band inside each side edge; inside it the cube is steered
        /// back toward the centre so it turns away from the world edge instead of leaving it.
        /// </param>
        public void Initialize(PlayerConfig config, float startHeadingRad, float minX, float maxX, float ceilingY, float edgeMargin)
        {
            _config     = config;
            _heading    = startHeadingRad;
            _minX       = minX;
            _maxX       = maxX;
            _worldWidth = maxX - minX;
            _ceilingY   = ceilingY;
            _edgeMargin = edgeMargin;

            CurrentHealth = Mathf.Max(1f, config.health);
            MaxHealth = CurrentHealth;

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;
            _rb.mass = Mathf.Max(0.0001f, config.mass);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _shake = GetComponentInChildren<ShakeEffect>(); // lives on the plane model child

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

            if (_falling)
            {
                // Shot down: no steering, no thrust — just gravity (plus a little extra pull)
                // carrying the leftover momentum down to the ground.
                _rb.AddForce(Vector3.down * FallExtraGravity, ForceMode.Acceleration);
                return;
            }

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
            desiredRate = FlightSteering.EdgeSteer(_rb.position.x, _heading,
                _minX, _maxX, _edgeMargin, maxRate, desiredRate);

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

        void ApplyRotation()
        {
            // Heading is an angle in the XY plane -> rotation about Z (the axis the camera looks down).
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Enemy fire lands here (via <see cref="IDamageable"/>); at zero health the plane
        /// stops flying and falls out of the sky — the crash itself happens when it hits
        /// the ground (see <see cref="OnCollisionEnter"/>).
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (!_active || _falling) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (CurrentHealth <= 0f) BeginFall();
        }

        /// <summary>
        /// A plane-to-plane scrape, driven by <see cref="LevelController"/>'s overlap check: shave
        /// off <see cref="CollisionDamage"/> and shiver the model, at most once per
        /// <see cref="CollisionCooldown"/> so one encounter is one hit. Returns whether the hit
        /// actually landed (false while on cooldown or already down), so the caller only shakes the
        /// camera on a real player scrape.
        /// </summary>
        public bool Scrape()
        {
            if (!_active || _falling) return false;
            if (Time.time - _lastCollisionTime < CollisionCooldown) return false;
            _lastCollisionTime = Time.time;

            TakeDamage(CollisionDamage);
            if (_shake != null) _shake.Play();
            // Cosmetic scrape feedback at the plane's position — collider-free, deals no damage.
            Sparks.Spawn(transform.position, ExplosionSize);
            return true;
        }

        /// <summary>
        /// Shot down: hand the plane to gravity. It keeps its forward momentum, tumbles about
        /// the view axis, and crashes for real on whatever it hits on the way down.
        /// </summary>
        void BeginFall()
        {
            _falling = true;
            OnShotDown?.Invoke();

            _rb.useGravity = true;
            _rb.angularVelocity = new Vector3(0f, 0f,
                (UnityEngine.Random.value < 0.5f ? -1f : 1f) * 2.5f);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!_active) return;

            // Bullets are not a crash: they already applied their damage via TakeDamage.
            if (collision.gameObject.GetComponent<Bullet>() != null) return;

            // Brushing an enemy is a scrape, never a crash. Planes share a layer whose
            // self-collisions are off (LevelController.DisablePlanePlaneCollisions), so a plane
            // contact normally never reaches here — but swallow it if one slips through, so a brush
            // can't fail the level. The scrape's damage, model shake and sparks come from
            // LevelController.CheckPlaneScrapes; the only solid thing left to hit is the ground.
            if (collision.gameObject.GetComponentInParent<EnemyController>() != null) return;

            // A shot-down plane goes up in flames when its fall ends.
            if (_falling)
            {
                Explosion.Spawn(transform.position, ExplosionSize);
                HideModel();
            }

            OnCrashed?.Invoke();
        }

        /// <summary>Removes the plane from view after it explodes (the body object itself
        /// survives so the camera's follow target stays valid).</summary>
        void HideModel()
        {
            foreach (Transform child in transform)
                child.gameObject.SetActive(false);
        }
    }
}
