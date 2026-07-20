using System;
using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// An enemy fighter — a mirrored Fokker Dr.1 (it attacks from the right and flies left) —
    /// flying the same constant-speed physics as the player's <see cref="CubeController"/>,
    /// driven by the sibling repo's FighterPlane AI ported 1:1. States:
    ///
    ///   * Attack  - chase the player with lead-prediction aim, guns firing when lined up.
    ///   * Fly     - break away toward high altitude, weaving, before the next attack run.
    ///   * Evade   - taking a hit knocks it into a corkscrew roll, a jittering dash away from
    ///               the player, and an unroll back into the attack.
    ///   * Recover - too close to the ground: abort everything and pull up at 70° until safe.
    ///   * Return  - drifted off camera: fly straight back toward the player.
    ///
    /// The soft side boundaries steer it away from the world edges exactly like the player
    /// (shared <see cref="FlightSteering"/>), and the same hard ceiling applies. A world-space
    /// health bar hangs above the plane; at zero health the fighter explodes and is removed
    /// from the map. Ramming is handled by <see cref="CubeController"/>, which destroys both.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        const float ShotVolume = 0.15f;          // half the player's 0.3 — quieter, and further away
        const float RecoverClimbAngleDeg = 70f;  // sibling RECOVER_CLIMB_ANGLE_DEG

        // Health bar geometry (metres, world space).
        const float BarWidth = 36f;
        const float BarHeight = 3.2f;
        const float BarLiftMargin = 8f; // bar centre sits this far above the top of the plane

        /// <summary>Raised once when this fighter is destroyed, however it died.</summary>
        public event Action<EnemyController> OnDestroyed;

        /// <summary>Hit points left; starts at <see cref="EnemyConfig.health"/>.</summary>
        public float CurrentHealth { get; private set; }

        enum AiState { Attack, Fly, Evade, Recover, Return }
        enum EvadePhase { Roll, Jitter, Unroll }

        EnemyConfig _config;
        Rigidbody _target;   // the player's physics body
        Rigidbody _rb;
        Collider _collider;
        Camera _cam;
        float _bodyRadius;   // half the plane model's longest extent — sizes the muzzle, explosion and health bar

        float _heading;         // radians; +Y (up) = π/2 — same convention as CubeController
        float _angularVelocity; // rad/s, eased toward the desired rate
        bool _dead;
        bool _standDown;        // level over: cease fire and just cruise

        // World bounds, supplied by LevelController at spawn.
        float _minX, _maxX, _groundY, _ceilingY, _edgeMargin;

        // ---- AI state (sibling FighterPlane fields) ----
        AiState _state = AiState.Attack;
        float _stateTimer;
        EvadePhase _evadePhase;
        float _evadeHeading;
        float _evadeRollSign = 1f;
        float _evadeRollAccum;
        float _jitterTimer;
        float _jitterOffset;
        float _flyWeaveT;
        float _flyBaseX;
        float _fireCooldown;

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;

        Transform _bar;          // health-bar root; deliberately NOT a child, so it never rotates
        Transform _barFillPivot; // sits at the bar's left edge; its X scale is the health fraction
        Renderer _barFill;

        /// <param name="groundY">
        /// World Y the AI treats as the ground when judging altitude (the terrain's highest
        /// point when the level is procedural, so the margins hold over every hill).
        /// </param>
        public void Initialize(EnemyConfig config, Rigidbody target,
            float minX, float maxX, float groundY, float ceilingY, float edgeMargin)
        {
            _config = config;
            _target = target;
            _minX = minX;
            _maxX = maxX;
            _groundY = groundY;
            _ceilingY = ceilingY;
            _edgeMargin = edgeMargin;

            CurrentHealth = Mathf.Max(1f, config.health);
            _stateTimer = config.attackDuration; // sibling opens in ATTACK with a full timer

            // Face the player so the opening move makes sense from any spawn point.
            Vector3 to = (target != null ? target.position : Vector3.zero) - transform.position;
            _heading = Mathf.Atan2(to.y, to.x);

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;
            _rb.mass = Mathf.Max(0.0001f, config.mass);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // The plane model hangs off this body, so its convex mesh collider (added by
            // LevelController.BuildPlaneModel) lives on a child, not on the body itself.
            _collider = GetComponentInChildren<Collider>();
            _bodyRadius = MeasureBodyRadius();
            _bulletTemplate = Bullet.BuildTemplate(Bullet.RoundColor); // same brass as the player's rounds

            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D, same reasoning as PlaneShooter

            _cam = Camera.main;

            BuildHealthBar();
            ApplyRotation();
        }

        /// <summary>The level ended (win or crash): cease fire and just cruise around.</summary>
        public void StandDown() => _standDown = true;

        // ---------------------------------------------------------------- flight + AI loop

        void FixedUpdate()
        {
            if (_dead || _config == null) return;

            float dt = Time.fixedDeltaTime;
            _stateTimer = Mathf.Max(0f, _stateTimer - dt);
            _jitterTimer = Mathf.Max(0f, _jitterTimer - dt);
            _fireCooldown -= dt;

            // ---- State selection (sibling FighterPlane.updateAI) ----
            if (_standDown)
            {
                if (_state != AiState.Fly) EnterFly(transform.position.x);
                _flyWeaveT += dt;
            }
            else if (CheckGroundAvoidance())
            {
                // Recover holds until the pull-up reaches a safe altitude (see TickState).
            }
            else if (!IsOnCamera(transform.position))
            {
                _state = AiState.Return;
            }
            else
            {
                if (_state == AiState.Return) EnterAttack();
                TickState(dt);
            }

            // ---- Steering: ease toward the AI's heading, but never out of the world ----
            SteerToHeading(ComputeHeading(dt), dt);

            // ---- Constant-speed forward flight, ceiling-clamped like the player ----
            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _config.flySpeed;
            Vector3 pos = _rb.position;
            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            _rb.linearVelocity = vel;
            if (pos.y > _ceilingY) { pos.y = _ceilingY; _rb.position = pos; }

            // ---- Guns: the sibling fires in every state except FLY and RETURN ----
            if (!_standDown && _state != AiState.Fly && _state != AiState.Return)
                UpdateFiring();
        }

        /// <summary>Below the minimum altitude nothing else matters: pull up (sibling
        /// checkGroundAvoidance).</summary>
        bool CheckGroundAvoidance()
        {
            if (transform.position.y - _groundY >= _config.minAltitudeMargin) return false;
            _state = AiState.Recover;
            return true;
        }

        /// <summary>Timed transitions between states (sibling tickState).</summary>
        void TickState(float dt)
        {
            if (_state == AiState.Recover)
            {
                if (transform.position.y - _groundY >= _config.safeAltitudeMargin) EnterAttack();
                return;
            }

            if (_state == AiState.Attack && _stateTimer <= 0f)
            {
                EnterFly(_target != null ? _target.position.x : transform.position.x);
                return;
            }

            if (_state == AiState.Fly && _stateTimer <= 0f)
            {
                EnterAttack();
                return;
            }

            if (_state == AiState.Evade && _evadePhase == EvadePhase.Jitter && _stateTimer <= 0f)
            {
                _evadePhase = EvadePhase.Unroll;
                _evadeRollAccum = 0f;
            }

            if (_state == AiState.Fly) _flyWeaveT += dt;
        }

        void EnterAttack()
        {
            _state = AiState.Attack;
            _stateTimer = _config.attackDuration;
        }

        void EnterFly(float baseX)
        {
            _state = AiState.Fly;
            _stateTimer = _config.flyDuration;
            _flyWeaveT = 0f;
            _flyBaseX = baseX;
        }

        void EnterEvade()
        {
            // Flee heading: straight away from the player (sibling: angle from target to self).
            Vector3 away = transform.position
                         - (_target != null ? _target.position : transform.position - Vector3.right);
            _evadeHeading = Mathf.Atan2(away.y, away.x);
            _evadePhase = EvadePhase.Roll;
            _evadeRollSign = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            _evadeRollAccum = 0f;
            _jitterTimer = 0f;
            _jitterOffset = 0f;
            _stateTimer = _config.evadeDuration;
            _state = AiState.Evade;
        }

        /// <summary>Where the AI wants to point right now (sibling computeHeading).</summary>
        float ComputeHeading(float dt)
        {
            switch (_state)
            {
                case AiState.Recover:
                {
                    // Climb hard at 70°, keeping whatever horizontal direction it already had.
                    float climb = RecoverClimbAngleDeg * Mathf.Deg2Rad;
                    return Mathf.Cos(_heading) >= 0f ? climb : Mathf.PI - climb;
                }

                case AiState.Evade:
                {
                    if (_evadePhase == EvadePhase.Roll || _evadePhase == EvadePhase.Unroll)
                    {
                        // A full 360° corkscrew: demand a heading ~π off to the chosen side and
                        // count turn progress until the loop closes; the unroll spins it back.
                        float sign = _evadePhase == EvadePhase.Roll ? _evadeRollSign : -_evadeRollSign;
                        _evadeRollAccum += _config.rotationSpeed * Mathf.Deg2Rad * dt;
                        if (_evadeRollAccum >= Mathf.PI * 2f)
                        {
                            if (_evadePhase == EvadePhase.Roll)
                            {
                                _evadePhase = EvadePhase.Jitter;
                                _evadeRollAccum = 0f;
                            }
                            else
                            {
                                EnterAttack();
                                return HeadingTo(PredictIntercept());
                            }
                        }
                        return _heading + sign * Mathf.PI * 0.9f;
                    }

                    // Jitter phase: dash away from the player, re-rolling a random heading
                    // offset jitterHz times a second so aimed fire keeps missing.
                    if (_jitterTimer <= 0f)
                    {
                        _jitterOffset = UnityEngine.Random.Range(-1f, 1f)
                                      * _config.jitterAmplitude * Mathf.Deg2Rad;
                        _jitterTimer = 1f / Mathf.Max(0.01f, _config.jitterHz);
                    }
                    return _evadeHeading + _jitterOffset;
                }

                case AiState.Fly:
                {
                    // Break away high over where the player was, weaving side to side.
                    float targetY = _groundY + (_ceilingY - _groundY) * _config.flyAltitudeFactor;
                    float weaveX = _flyBaseX + Mathf.Sin(_flyWeaveT * Mathf.PI * 2f * _config.weaveHz)
                                             * _config.weaveAmplitude;
                    return HeadingTo(new Vector2(weaveX, targetY));
                }

                case AiState.Return:
                    return _target != null ? HeadingTo((Vector2)_target.position) : _heading;

                case AiState.Attack:
                default:
                    return HeadingTo(PredictIntercept());
            }
        }

        /// <summary>Turn-rate steering toward a heading (sibling steerToHeading +
        /// applyTurnRate), with the shared soft side boundary overriding the AI near an
        /// edge exactly as it overrides the player's stick.</summary>
        void SteerToHeading(float targetHeading, float dt)
        {
            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad;
            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, targetHeading * Mathf.Rad2Deg)
                        * Mathf.Deg2Rad;
            float desiredRate = Mathf.Clamp(dt > 0f ? error / dt : 0f, -maxRate, maxRate);

            desiredRate = FlightSteering.EdgeSteer(_rb.position.x, _heading,
                _minX, _maxX, _edgeMargin, maxRate, desiredRate);

            float approach = 1f - Mathf.Exp(-(_config.turnResponsiveness / _rb.mass) * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
            ApplyRotation();
        }

        void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg);
        }

        float HeadingTo(Vector2 point)
        {
            return Mathf.Atan2(point.y - transform.position.y, point.x - transform.position.x);
        }

        /// <summary>Half the longest side of the plane model's combined renderer bounds, so the
        /// muzzle, explosion and health bar all scale to whatever size the model is built at.</summary>
        float MeasureBodyRadius()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 15f;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f;
        }

        // ---------------------------------------------------------------- guns

        /// <summary>Fires when the player is on screen, in range, and the nose is within the
        /// aim threshold of the lead-predicted intercept (sibling updateFiring).</summary>
        void UpdateFiring()
        {
            if (_target == null || !IsOnCamera(_target.position)) return;

            if (Vector2.Distance(transform.position, _target.position) > _config.maxFireRange)
                return;

            Vector2 aim = PredictIntercept();
            float aimErrorDeg = Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg,
                HeadingTo(aim) * Mathf.Rad2Deg));
            if (aimErrorDeg > _config.fireAngleThreshold) return;

            if (_fireCooldown > 0f) return;
            _fireCooldown = Mathf.Max(0.01f, _config.fireRate);

            Vector3 dir = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f);
            // From the plane's nose, clear of its own collider.
            Vector3 muzzle = transform.position + dir * (_bodyRadius + 6f);
            // Same -90° trick as PlaneShooter: lays the tracer's long axis along the heading.
            var go = Instantiate(_bulletTemplate, muzzle,
                transform.rotation * Quaternion.Euler(0f, 0f, -90f));
            go.name = "EnemyBullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, _config.bulletSpeed, _config.damage, _collider);

            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume);
        }

        /// <summary>Two-pass iterative intercept: aim where the player will be by the time a
        /// round can get there (sibling predictIntercept).</summary>
        Vector2 PredictIntercept()
        {
            if (_target == null) return transform.position;

            Vector2 tp = _target.position;
            Vector2 tv = _target.linearVelocity;
            float t = 0f;
            for (int i = 0; i < 2; i++)
            {
                float d = Vector2.Distance(transform.position, tp + tv * (t * _config.leadFactor));
                t = _config.bulletSpeed > 0f ? d / _config.bulletSpeed : 0f;
            }
            return tp + tv * (t * _config.leadFactor);
        }

        bool IsOnCamera(Vector3 worldPos)
        {
            if (_cam == null) return true;
            Vector3 vp = _cam.WorldToViewportPoint(worldPos);
            return vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        // ---------------------------------------------------------------- damage + death

        public void TakeDamage(float amount)
        {
            if (_dead) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            UpdateHealthBar();

            if (CurrentHealth <= 0f)
            {
                Explode();
                return;
            }

            // Sibling onDamaged: a hit knocks it out of Attack/Fly into the corkscrew evade.
            if (_state == AiState.Attack || _state == AiState.Fly) EnterEvade();
        }

        /// <summary>Destroys the fighter in a fireball and removes it from the map. Also called
        /// by <see cref="CubeController"/> when the player rams it.</summary>
        public void Explode()
        {
            if (_dead) return;
            _dead = true;

            Explosion.Spawn(transform.position, _bodyRadius > 0f ? _bodyRadius * 2f : 30f);
            OnDestroyed?.Invoke(this);
            Destroy(gameObject); // OnDestroy takes the health bar with it
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_dead) return;

            // Bullets apply their damage via TakeDamage; the impact itself isn't a crash.
            if (collision.gameObject.GetComponent<Bullet>() != null) return;

            // Ramming the player is the player's case: CubeController explodes both planes.
            if (collision.gameObject.GetComponentInParent<CubeController>() != null) return;

            // Anything else solid — the ground the AI failed to dodge, another fighter — kills it.
            Explode();
        }

        // ---------------------------------------------------------------- health bar

        /// <summary>
        /// A world-space bar floating above the cube: a dark backplate and an emissive fill
        /// whose left-anchored pivot is scaled by the health fraction. The bar lives outside
        /// the fighter's hierarchy so the cube's banking never tilts it; it just follows.
        /// </summary>
        void BuildHealthBar()
        {
            _bar = new GameObject("EnemyHealthBar").transform;

            var back = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                Vector3.zero, new Vector3(BarWidth, BarHeight, 0.5f),
                new Color(0.06f, 0.06f, 0.06f), emissive: false, keepCollider: false);
            back.name = "Back";
            back.transform.SetParent(_bar, false);

            _barFillPivot = new GameObject("FillPivot").transform;
            _barFillPivot.SetParent(_bar, false);
            // Nudged toward the camera (-Z) so the fill always draws in front of the backplate.
            _barFillPivot.localPosition = new Vector3(-BarWidth / 2f, 0f, -0.5f);

            var fill = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                Vector3.zero, new Vector3(BarWidth - 1f, BarHeight - 0.8f, 0.4f),
                new Color(0.25f, 0.9f, 0.3f), emissive: true, keepCollider: false);
            fill.name = "Fill";
            fill.transform.SetParent(_barFillPivot, false);
            fill.transform.localPosition = new Vector3((BarWidth - 1f) / 2f, 0f, 0f);
            _barFill = fill.GetComponent<Renderer>();

            UpdateHealthBar();
            PlaceHealthBar();
        }

        void UpdateHealthBar()
        {
            if (_barFillPivot == null || _config == null) return;

            float frac = Mathf.Clamp01(CurrentHealth / Mathf.Max(1f, _config.health));
            Vector3 s = _barFillPivot.localScale;
            s.x = frac;
            _barFillPivot.localScale = s;

            // Green at full health through to red near death.
            var color = Color.Lerp(new Color(0.95f, 0.2f, 0.12f), new Color(0.25f, 0.9f, 0.3f), frac);
            var mat = _barFill.sharedMaterial; // unique per fill, see UIFactory.CreatePrimitive3D
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_EmissionColor", color * 2f);
        }

        void PlaceHealthBar()
        {
            if (_bar != null)
                _bar.position = transform.position + Vector3.up * (_bodyRadius + BarLiftMargin);
        }

        void LateUpdate()
        {
            if (!_dead) PlaceHealthBar();
        }

        void OnDestroy()
        {
            if (_bar != null) Destroy(_bar.gameObject);
        }
    }
}
