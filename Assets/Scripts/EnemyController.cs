using System;
using UnityEngine;

namespace MetalRaptors
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        const float ShotVolume = 0.18f;
        const float RecoverClimbAngleDeg = 70f;
        const float CeilingMargin = 130f;
        const float ReturnSpeedFactor = 1.35f;
        const float BreakRoomCap = 300f;
        const float BreakRoomTie = 60f;

        const float CircleSeconds = 2.5f;
        const float CircleRateFraction = 0.5f;
        const float EvadeClimbRoom = 140f;
        const float EvadeDiveRoom = 110f;
        const float EvadeBandGive = 120f;

        const float CollisionDamage = 10f;
        const float CollisionCooldown = 0.5f;

        const float SnapFireConeDeg = 26f;
        const float SnapWindowFactor = 2f;
        const float DefaultTargetRadius = 15f;

        const float SmokeHealthThreshold = 30f;

        const float BandPushMargin = 80f;
        const float BandPushFraction = 0.4f;
        const float GroundProbe = 400f;
        const float PressReach = 80f;
        const float PressBreakFraction = 0.6f;
        const float ReversalCooldown = 2.5f;
        const float DiveRangeFactor = 1.6f;
        const float ManoeuvreTopMargin = 40f;
        const float DiveZoomSeconds = 3f;
        const float DiveCornerReach = 90f;
        const float DiveCornerInset = 70f;
        const float DiveSideMargin = 40f;
        const float ManoeuvreFloorLift = 40f;
        const float DiveTurnFactor = 1.7f;
        const float AimReach = 150f;

        const float TailStandoff = 95f;
        const float TailSlotTolerance = 55f;
        const float TailEnterConeDeg = 75f;
        const float TailBreakConeDeg = 105f;
        const float TailBreakSeconds = 1.1f;
        const float TailRangeFactor = 1.4f;
        const float TailCloseRange = 220f;
        const float TailChaseFactor = 1.3f;
        const float TailHandover = 60f;
        const float TailLockReach = 260f;
        const float TailLockHeadingDeg = 45f;
        const float TailLockGive = 1.6f;
        const float TailDescentDeg = 35f;
        const float TailGunLineFactor = 6f;

        const float TurnChoiceAngleDeg = 30f;
        const float TurnCheckInterval = 0.35f;
        const float TurnSimStep = 0.15f;
        const float TurnSimHorizon = 2f;
        const float TurnClimbAngleDeg = 55f;
        const float TurnClimbGain = 60f;

        const float BarWidth = 36f;
        const float BarHeight = 3.2f;
        const float BarLiftMargin = 8f;

        public event Action<EnemyController> OnDestroyed;

        public float CurrentHealth { get; private set; }

        public bool IsAlive => !_dead && !_falling;

        public Collider Hitbox => _collider;

        public float ModelSize => _bodyRadius > 0f ? _bodyRadius * 2f : 30f;

        enum AiState { Attack, Fly, Evade, Recover, Return, DiveClimb, DiveRun, DiveZoom, Tail }

        EnemyConfig _config;
        Rigidbody _target;
        Rigidbody _rb;
        Collider _collider;
        Camera _cam;
        PlaneShooter _shooter;
        float _bodyRadius;
        float _targetRadius = DefaultTargetRadius;

        float _heading;
        float _angularVelocity;
        bool _dead;
        bool _falling;
        PlaneFall _fall;
        bool _reported;
        float _fallTimer;
        bool _standDown;

        float _minX, _maxX, _groundY, _ceilingY, _edgeMargin;
        float _wallX = float.NegativeInfinity;

        AiState _state = AiState.Attack;
        float _stateTimer;
        float _evadeCooldown;
        float _circleTimer;
        float _circleDir;
        EvadeMove _lastEvade = EvadeMove.Break;
        readonly EvadeMove[] _evadePool = new EvadeMove[5];
        float _flyWeaveT;
        float _flyBaseX;
        float _tailLost;
        bool _tailLocked;
        float _fireCooldown;
        float _lastCollisionTime = -999f;
        ShakeEffect _shake;
        SmokeTrail _smoke;
        PlaneFire _fire;
        readonly PlaneRoll _roll = new PlaneRoll(true);

        readonly EnemyLoop _loop = new EnemyLoop();
        readonly EnemyDepthDodge _dodge = new EnemyDepthDodge();
        readonly EnemyEvade _evade = new EnemyEvade();

        float _deck;
        float _baseZ;
        float _speed;
        float _engageSpeed;
        float _reversalCooldown;
        float _dodgeCooldown;
        float _diveCooldown;
        float _pressTimer;
        float _pressHold;
        float _turnDir;
        float _turnCheck;
        bool _turnClimb;
        float _diveSide;
        bool _onCamera = true;
        bool _appeared;
        WingStreaks _streaks;

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;

        Transform _bar;
        Transform _barFillPivot;
        Renderer _barFill;

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
            _deck = groundY;

            CurrentHealth = Mathf.Max(1f, config.health);
            _stateTimer = config.attackDuration;
            _speed = config.flySpeed;
            _baseZ = transform.position.z;

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

            _collider = GetComponentInChildren<Collider>();
            _shake = GetComponentInChildren<ShakeEffect>();
            _bodyRadius = MeasureRadius(gameObject);
            _targetRadius = target != null
                ? Mathf.Clamp(MeasureRadius(target.gameObject), 8f, 40f)
                : DefaultTargetRadius;
            if (!Scouting) _streaks = WingStreaks.Mount(gameObject, transform);
            _smoke = gameObject.AddComponent<SmokeTrail>();
            _bulletTemplate = Bullet.BuildTemplate(Bullet.RoundColor);

            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            _cam = Camera.main;
            _shooter = target != null ? target.GetComponent<PlaneShooter>() : null;

            BuildHealthBar();
            ApplyRotation();
        }

        public void StandDown()
        {
            _standDown = true;
            SetStreaks(false);
            _evade.Cancel();
            _loop.Cancel();
        }

        public void SetBounds(float minX, float maxX)
        {
            _minX = minX;
            _maxX = maxX;
        }

        public void SetLeftWall(float x) => _wallX = Mathf.Max(_wallX, x);

        EnemyRole Role => _config != null ? _config.role : EnemyRole.Fighter;

        static EnemyController _runDown;

        bool Scouting => Role == EnemyRole.Scout;

        public bool RunningDown => _state == AiState.Tail && _runDown == this && _tailLocked;

        bool Reversing => _loop.Active;

        bool Diving => _state == AiState.DiveClimb || _state == AiState.DiveRun
                    || _state == AiState.DiveZoom;

        public bool OffPlane => _dodge.Clear;

        float GroundRef => Scouting ? _deck : _groundY;

        void FixedUpdate()
        {
            if (_dead || _config == null) return;

            float dt = Time.fixedDeltaTime;

            if (_falling)
            {
                TickFall(dt);
                return;
            }

            _stateTimer = Mathf.Max(0f, _stateTimer - dt);
            _evadeCooldown = Mathf.Max(0f, _evadeCooldown - dt);
            _reversalCooldown = Mathf.Max(0f, _reversalCooldown - dt);
            _diveCooldown = Mathf.Max(0f, _diveCooldown - dt);
            _fireCooldown -= dt;

            _onCamera = IsOnCamera(transform.position);
            _appeared |= _onCamera;

            TickDeck();
            TickDodge(dt);
            TickPress(dt);
            TickCircle(dt);

            if (_standDown)
            {
                if (_state != AiState.Fly) EnterFly(transform.position.x);
                _flyWeaveT += dt;
            }
            else if (CheckGroundAvoidance())
            {
                CancelReversal();
            }
            else if (!_onCamera && _state != AiState.Recover && !Diving)
            {
                _state = AiState.Return;
                CancelReversal();
            }
            else
            {
                if (_state == AiState.Return) EnterAttack();
                if (!Reversing) TickState(dt);
            }

            if (Reversing) DriveReversal(dt);
            else
            {
                float desired = Contain(ComputeHeading());
                if (WantsReversal(desired)) BeginReversal(desired);
                else SteerToHeading(KeepNoseUp(ChooseTurn(desired, dt)), dt);
            }

            ApplyVelocity(dt);

            if (!_standDown && _state != AiState.Return) UpdateFiring();
        }

        void TickFall(float dt)
        {
            _fall.Step(_rb, dt);
            _heading = _fall.Heading;
            ApplyRotation();

            _fallTimer += dt;
            if (_fallTimer >= PlaneFall.Timeout) RemoveWreck();
        }

        void TickDeck()
        {
            if (!Scouting)
            {
                _deck = _groundY;
                return;
            }

            _deck = TerrainAt(_rb.position.x);
        }

        float TerrainAt(float x)
        {
            var from = new Vector3(x, _rb.position.y + GroundProbe, _rb.position.z);

            return Physics.Raycast(from, Vector3.down, out RaycastHit info, GroundProbe * 3f,
                1 << ProceduralTerrain.GroundLayer, QueryTriggerInteraction.Ignore)
                ? info.point.y
                : _groundY;
        }

        void TickDodge(float dt)
        {
            _dodgeCooldown = Mathf.Max(0f, _dodgeCooldown - dt);

            if (!_dodge.Active)
            {
                if (!CanDodge()) return;
                BeginDodge();
            }

            _dodge.Step(dt);
            if (_dodge.Active) return;

            _dodgeCooldown = _config.dodgeCooldown;
            ReturnToPlane();
        }

        void ReturnToPlane()
        {
            _rb.constraints |= RigidbodyConstraints.FreezePositionZ;

            Vector3 pos = _rb.position;
            pos.z = _baseZ;
            _rb.position = pos;
        }

        void TickPress(float dt)
        {
            if (!Scouting || _standDown || _target == null)
            {
                _pressTimer = 0f;
                _pressHold = 0f;
                return;
            }

            if (_pressHold > 0f)
            {
                _pressHold -= dt;
                if (TargetDistance() <= _config.maxFireRange * PressBreakFraction) _pressHold = 0f;
                if (_pressHold <= 0f) _pressTimer = 0f;
                return;
            }

            bool outOfReach = TargetDistance() > _config.maxFireRange
                || _target.position.y > BandCeiling() + PressReach;

            _pressTimer = outOfReach ? _pressTimer + dt : 0f;
            if (_pressTimer >= _config.pressDelay) _pressHold = _config.pressDuration;
        }

        bool CheckGroundAvoidance()
        {
            if (RunningDown) return false;
            if (transform.position.y - GroundRef >= _config.minAltitudeMargin) return false;
            EndDive();
            _state = AiState.Recover;
            return true;
        }

        void TickState(float dt)
        {
            if (_state == AiState.Recover)
            {
                if (transform.position.y - GroundRef >= _config.safeAltitudeMargin) EnterAttack();
                return;
            }

            if (_state == AiState.DiveClimb)
            {
                if (_stateTimer <= 0f || AtDiveTop()) EnterDiveRun();
                return;
            }

            if (_state == AiState.DiveRun)
            {
                if (_stateTimer <= 0f || AtDiveBottom()) EnterDiveZoom();
                return;
            }

            if (_state == AiState.DiveZoom)
            {
                if (_stateTimer <= 0f
                    || transform.position.y >= AltitudeBands.Floor(AltitudeBand.High, _groundY, _ceilingY))
                {
                    EndDive();
                    EnterAttack();
                }
                return;
            }

            if (_state == AiState.Evade)
            {
                if (_stateTimer <= 0f)
                {
                    _evadeCooldown = _config.evadeCooldown;
                    EnterAttack();
                    return;
                }

                _evade.Step(dt, transform.position,
                    _target != null ? (Vector2)_target.position : (Vector2)transform.position);
                return;
            }

            if (_state == AiState.Tail)
            {
                TickTail(dt);
                return;
            }

            if (WantsDive())
            {
                EnterDiveClimb();
                return;
            }

            if (_evadeCooldown <= 0f && WantsTail())
            {
                EnterTail();
                return;
            }

            if (_state == AiState.Attack && _stateTimer <= 0f)
            {
                if (TargetDistance() <= _config.maxFireRange)
                    EnterFly(_target != null ? _target.position.x : transform.position.x);
                else
                    EnterAttack();
                return;
            }

            if (_state == AiState.Fly && _stateTimer <= 0f)
            {
                EnterAttack();
                return;
            }

            if (_evadeCooldown <= 0f && UnderThreat())
            {
                EnterEvade(circling: false);
                return;
            }

            if (_evadeCooldown <= 0f && _appeared && _circleTimer >= CircleSeconds)
            {
                EnterEvade(circling: true);
                return;
            }

            if (_state == AiState.Fly) _flyWeaveT += dt;
        }

        void EnterAttack()
        {
            _state = AiState.Attack;
            _tailLocked = false;
            _stateTimer = _config.attackDuration;
        }

        void EnterTail()
        {
            _state = AiState.Tail;
            _stateTimer = 0f;
            _tailLost = 0f;
            _tailLocked = false;
        }

        void CancelDodge()
        {
            if (!_dodge.Active) return;

            _dodge.Cancel();
            ReturnToPlane();
        }

        void ReleaseDodge()
        {
            if (!_dodge.Active) return;

            _dodge.Release();
            if (_dodge.Active) return;

            _dodgeCooldown = _config.dodgeCooldown;
            ReturnToPlane();
        }

        bool WantsTail()
        {
            if (!_appeared || _standDown || _target == null) return false;
            if (_state != AiState.Attack && _state != AiState.Fly) return false;
            if (Diving || UnderThreat()) return false;
            if (TargetDistance() > _config.maxFireRange * TailRangeFactor) return false;
            if (TailOffAngle() > TailEnterConeDeg) return false;

            return ClaimRunDown();
        }

        bool ClaimRunDown()
        {
            if (_runDown != null && (!_runDown.IsAlive || _runDown._state != AiState.Tail))
                _runDown = null;

            if (_runDown == this) return true;
            if (_runDown != null
                && TargetDistance() >= _runDown.TargetDistance() - TailHandover) return false;

            _runDown = this;
            return true;
        }

        void TickTail(float dt)
        {
            if (_target == null || UnderThreat()
                || TargetDistance() > _config.maxFireRange * TailRangeFactor)
            {
                EnterAttack();
                return;
            }

            bool lost = TailOffAngle() > TailBreakConeDeg || (RunningDown && OffGunLine());
            _tailLost = lost ? _tailLost + dt : 0f;
            if (_tailLost >= TailBreakSeconds) { EnterAttack(); return; }

            TickTailLock();
        }

        void TickTailLock()
        {
            float behind = TailBehind();
            if (behind <= 0f) { _tailLocked = false; return; }

            if (_tailLocked)
            {
                _tailLocked = behind <= TailLockReach * TailLockGive;
                return;
            }

            if (behind > TailLockReach || !NoseOnTrack()) return;

            _tailLocked = true;
            ReleaseDodge();
        }

        bool OffGunLine()
        {
            float range = TargetDistance();
            if (range < 1f) return false;

            float errorDeg = Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg,
                HeadingTo((Vector2)_target.position) * Mathf.Rad2Deg));

            return range * Mathf.Sin(errorDeg * Mathf.Deg2Rad)
                > _targetRadius * TailGunLineFactor;
        }

        float TailBehind()
        {
            if (_target == null) return 0f;

            Vector2 run = _target.linearVelocity;
            if (run.sqrMagnitude < 1f) return 0f;

            return Vector2.Dot((Vector2)transform.position - (Vector2)_target.position,
                -run.normalized);
        }

        bool NoseOnTrack()
        {
            Vector2 run = _target.linearVelocity;
            if (run.sqrMagnitude < 1f) return false;

            float track = Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, track))
                <= TailLockHeadingDeg;
        }

        float TailOffAngle()
        {
            if (_target == null) return 180f;

            Vector2 run = _target.linearVelocity;
            if (run.sqrMagnitude < 1f) return 0f;

            return Vector2.Angle(-run, (Vector2)transform.position - (Vector2)_target.position);
        }

        Vector2 TailSlot()
        {
            if (_target == null) return transform.position;

            Vector2 run = _target.linearVelocity;
            Vector2 back = run.sqrMagnitude > 1f
                ? run.normalized
                : new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            return (Vector2)_target.position - back * TailStandoff;
        }

        float TailHeading()
        {
            if (RunningDown && _target != null)
                return EaseDescent(HeadingTo((Vector2)_target.position));

            Vector2 slot = ClampToBand(TailSlot());
            float toSlot = HeadingTo(slot);

            float gap = Vector2.Distance(transform.position, slot);
            if (gap >= TailSlotTolerance) return toSlot;

            Vector2 run = _target != null ? (Vector2)_target.linearVelocity : Vector2.zero;
            if (run.sqrMagnitude < 1f) return toSlot;

            float match = Mathf.Atan2(run.y, run.x);
            float blend = gap / TailSlotTolerance;
            return Mathf.LerpAngle(match * Mathf.Rad2Deg, toSlot * Mathf.Rad2Deg, blend)
                   * Mathf.Deg2Rad;
        }

        static float EaseDescent(float heading)
        {
            float sink = Mathf.Sin(heading);
            float limit = -Mathf.Sin(TailDescentDeg * Mathf.Deg2Rad);
            if (sink >= limit) return heading;

            float run = (Mathf.Cos(heading) >= 0f ? 1f : -1f)
                        * Mathf.Cos(TailDescentDeg * Mathf.Deg2Rad);
            return Mathf.Atan2(limit, run);
        }

        float TailSpeed(float speed)
        {
            if (_target == null) return speed;

            float run = ((Vector2)_target.linearVelocity).magnitude;
            if (run < 1f) return speed;

            float gap = RunningDown
                ? Mathf.Max(0f, TargetDistance() - TailStandoff)
                : Vector2.Distance(transform.position, ClampToBand(TailSlot()));
            float closing = Mathf.Clamp01(gap / TailCloseRange);
            return Mathf.Lerp(run, Mathf.Max(speed, run * TailChaseFactor), closing);
        }

        void EnterFly(float baseX)
        {
            _state = AiState.Fly;
            _stateTimer = _config.flyDuration;
            _flyWeaveT = 0f;
            _flyBaseX = baseX;
        }

        bool WantsDive()
        {
            if (!_appeared) return false;
            if (Scouting || _target == null) return false;
            if (_diveCooldown > 0f) return false;
            if (_state != AiState.Attack && _state != AiState.Fly) return false;

            if (_target.position.y - _groundY > _config.diveTriggerHeight) return false;

            return TargetDistance() <= _config.maxFireRange * DiveRangeFactor;
        }

        void EnterDiveClimb()
        {
            _state = AiState.DiveClimb;
            _stateTimer = _config.diveClimbSeconds;
            _diveSide = _target != null && _target.position.x > transform.position.x ? -1f : 1f;
            SetStreaks(true);
        }

        void EnterDiveRun()
        {
            _state = AiState.DiveRun;
            _stateTimer = _config.diveRunSeconds;
            _reversalCooldown = 0f;
        }

        void EnterDiveZoom()
        {
            _state = AiState.DiveZoom;
            _stateTimer = DiveZoomSeconds;
        }

        void EndDive()
        {
            if (!Diving) return;
            _diveCooldown = _config.diveCooldown;
            SetStreaks(false);
        }

        void SetStreaks(bool on)
        {
            if (_streaks != null) _streaks.SetEmitting(on);
        }

        bool CameraBounds(out Vector3 min, out Vector3 max)
        {
            min = max = Vector3.zero;
            if (_cam == null) return false;

            float depth = transform.position.z - _cam.transform.position.z;
            if (depth <= 0f) return false;

            min = _cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            max = _cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            return true;
        }

        float DiveEdgeX(float side)
        {
            if (!CameraBounds(out Vector3 min, out Vector3 max))
                return side > 0f ? _maxX : _minX;

            float view = (side > 0f ? max.x : min.x) - side * DiveCornerInset;
            return side > 0f ? Mathf.Min(_maxX, view) : Mathf.Max(_minX, view);
        }

        float DiveTopY()
        {
            float roof = _ceilingY - ManoeuvreTopMargin;
            return CameraBounds(out Vector3 _, out Vector3 max)
                ? Mathf.Min(roof, max.y - DiveCornerInset)
                : roof;
        }

        float DiveBottomY() => _groundY + _config.minAltitudeMargin + ManoeuvreFloorLift;

        Vector2 DiveRunAim()
        {
            float bottom = DiveBottomY();
            if (_target == null || PastTargetX()) return new Vector2(DiveEdgeX(-_diveSide), bottom);

            Vector3 aim = _target.position;
            return new Vector2(aim.x,
                Mathf.Max(bottom, Mathf.Min(aim.y, transform.position.y)));
        }

        bool PastTargetX()
        {
            float run = -_diveSide;
            return run > 0f ? transform.position.x >= _target.position.x
                            : transform.position.x <= _target.position.x;
        }

        bool AtDiveTop() =>
            transform.position.y >= DiveTopY() - DiveCornerReach && AtDiveEdge(_diveSide);

        bool AtDiveBottom() =>
            transform.position.y <= DiveBottomY() + DiveCornerReach && AtDiveEdge(-_diveSide);

        bool AtDiveEdge(float side)
        {
            float edge = DiveEdgeX(side);
            return side > 0f ? transform.position.x >= edge - DiveCornerReach
                             : transform.position.x <= edge + DiveCornerReach;
        }

        void EnterEvade(bool circling)
        {
            Vector3 away = transform.position
                         - (_target != null ? _target.position : transform.position - Vector3.right);
            float awayHeading = Mathf.Atan2(away.y, away.x);
            float breakAngle = _config.evadeBreakAngle * Mathf.Deg2Rad;

            float high = awayHeading + breakAngle;
            float low = awayHeading - breakAngle;
            float roomUp = Mathf.Max(0f, BandCeiling() - transform.position.y);
            float roomDown = Mathf.Max(0f, transform.position.y - BandFloor());

            float highRoom = BreakRoom(high, roomUp, roomDown);
            float lowRoom = BreakRoom(low, roomUp, roomDown);

            bool breakHigh = Mathf.Abs(highRoom - lowRoom) < BreakRoomTie
                ? UnityEngine.Random.value < 0.5f
                : highRoom > lowRoom;

            var plan = new EvadePlan
            {
                move = PickEvade(circling),
                side = breakHigh ? 1f : -1f,
                breakHeading = breakHigh ? high : low,
                breakSeconds = _config.evadeDuration,
                jitterAmplitude = _config.jitterAmplitude,
                jitterHz = _config.jitterHz,
            };

            _evade.Begin(plan, _heading, transform.position,
                _target != null ? (Vector2)_target.position : (Vector2)transform.position);

            _circleTimer = 0f;
            _circleDir = 0f;
            _stateTimer = _evade.Seconds;
            _state = AiState.Evade;
        }

        float EvadeRoof() =>
            Mathf.Min(_ceilingY - ManoeuvreTopMargin, BandCeiling() + EvadeBandGive);

        float EvadeFloor() =>
            Mathf.Max(GroundRef + _config.minAltitudeMargin + ManoeuvreFloorLift,
                BandFloor() - EvadeBandGive);

        EvadeMove PickEvade(bool circling)
        {
            float y = transform.position.y;
            int count = 0;

            if (!circling) _evadePool[count++] = EvadeMove.Break;
            _evadePool[count++] = EvadeMove.Scissors;
            _evadePool[count++] = EvadeMove.Extend;
            if (EvadeRoof() - y >= EvadeClimbRoom) _evadePool[count++] = EvadeMove.Chandelle;
            if (y - EvadeFloor() >= EvadeDiveRoom) _evadePool[count++] = EvadeMove.SplitDive;

            int index = UnityEngine.Random.Range(0, count);
            if (count > 1 && _evadePool[index] == _lastEvade) index = (index + 1) % count;

            _lastEvade = _evadePool[index];
            return _lastEvade;
        }

        void TickCircle(float dt)
        {
            bool fighting = !_standDown && _target != null
                         && (_state == AiState.Attack || _state == AiState.Fly);

            if (!fighting || TargetDistance() > _config.threatRange)
            {
                _circleTimer = 0f;
                _circleDir = 0f;
                return;
            }

            float rate = _angularVelocity;
            if (Mathf.Abs(rate) < _config.rotationSpeed * Mathf.Deg2Rad * CircleRateFraction)
            {
                _circleTimer = 0f;
                _circleDir = 0f;
                return;
            }

            if (_circleDir == 0f || Mathf.Sign(rate) != _circleDir)
            {
                _circleDir = Mathf.Sign(rate);
                _circleTimer = 0f;
                return;
            }

            _circleTimer += dt;
        }

        static float BreakRoom(float heading, float roomUp, float roomDown)
        {
            return Mathf.Min(Mathf.Sin(heading) > 0f ? roomUp : roomDown, BreakRoomCap);
        }

        bool UnderThreat()
        {
            if (_target == null) return false;

            Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;
            if (distance < 1f || distance > _config.threatRange) return false;

            Vector2 aim = _target.linearVelocity;
            if (aim.sqrMagnitude < 1f) return false;
            if (Vector2.Angle(aim, -toTarget) > _config.threatCone) return false;

            var nose = new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            return Vector2.Angle(nose, toTarget) > _config.threatTailAngle;
        }

        float TargetDistance()
        {
            return _target != null
                ? Vector2.Distance(transform.position, _target.position)
                : float.MaxValue;
        }

        float BandFloor()
        {
            if (_state == AiState.DiveRun || _state == AiState.DiveZoom)
                return _groundY + _config.minAltitudeMargin;

            if (Scouting) return _deck + _config.safeAltitudeMargin;

            return Mathf.Max(AltitudeBands.Floor(AltitudeBand.High, _groundY, _ceilingY),
                _groundY + _config.safeAltitudeMargin);
        }

        float BandCeiling()
        {
            if (_state == AiState.DiveClimb) return _ceilingY - ManoeuvreTopMargin;

            float roof;
            if (Scouting)
            {
                float mid = AltitudeBands.Ceiling(AltitudeBand.Mid, _groundY, _ceilingY);
                roof = _deck + _config.deckCeilingMargin;
                roof = _pressHold > 0f ? Mathf.Max(roof, mid) : Mathf.Min(roof, mid);
            }
            else
            {
                roof = AltitudeBands.Ceiling(AltitudeBand.High, _groundY, _ceilingY);
            }

            return Mathf.Min(roof, _ceilingY - CeilingMargin);
        }

        float Contain(float heading)
        {
            float floor = BandFloor();
            float roof = Mathf.Max(floor + 1f, BandCeiling());

            if (_state == AiState.Evade)
            {
                if (_evade.Move == EvadeMove.Chandelle) roof = EvadeRoof();
                else if (_evade.Move == EvadeMove.SplitDive) floor = EvadeFloor();
                roof = Mathf.Max(floor + 1f, roof);
            }

            float margin = Mathf.Min(BandPushMargin, (roof - floor) * BandPushFraction);
            float floorMargin = Scouting
                ? Mathf.Max(margin, _config.safeAltitudeMargin - _config.minAltitudeMargin)
                : margin;

            if (RunningDown) { floorMargin = 0f; margin = 0f; }

            float minX = Diving ? DiveEdgeX(-1f) : _minX;
            float maxX = Diving ? DiveEdgeX(1f) : _maxX;
            float sideMargin = Diving ? DiveSideMargin : _edgeMargin;

            return FlightSteering.Contain(heading, _rb.position,
                minX, maxX, sideMargin,
                floor, floorMargin, roof, margin);
        }

        float ComputeHeading()
        {
            switch (_state)
            {
                case AiState.Recover:
                {
                    float climb = RecoverClimbAngleDeg * Mathf.Deg2Rad;
                    return Mathf.Cos(_heading) >= 0f ? climb : Mathf.PI - climb;
                }

                case AiState.DiveClimb:
                    return HeadingTo(new Vector2(DiveEdgeX(_diveSide), DiveTopY()));

                case AiState.DiveRun:
                    return HeadingTo(DiveRunAim());

                case AiState.DiveZoom:
                {
                    float climb = RecoverClimbAngleDeg * Mathf.Deg2Rad;
                    return Mathf.Cos(_heading) >= 0f ? climb : Mathf.PI - climb;
                }

                case AiState.Evade:
                    return _evade.Heading;

                case AiState.Fly:
                {
                    float floor = BandFloor();
                    float roof = Mathf.Max(floor, BandCeiling());
                    float perch = (_target != null ? _target.position.y : transform.position.y)
                                + _config.flyPerchHeight;
                    float targetY = Mathf.Clamp(perch, floor, roof);
                    float weaveX = _flyBaseX + Mathf.Sin(_flyWeaveT * Mathf.PI * 2f * _config.weaveHz)
                                             * _config.weaveAmplitude;
                    return HeadingTo(new Vector2(weaveX, targetY));
                }

                case AiState.Return:
                    return _target != null
                        ? HeadingTo(ClampToBand((Vector2)_target.position))
                        : _heading;

                case AiState.Tail:
                    return TailHeading();

                case AiState.Attack:
                default:
                    return HeadingTo(ClampToBand(PredictIntercept()));
            }
        }

        Vector2 ClampToBand(Vector2 point)
        {
            float floor = BandFloor();
            float roof = Mathf.Max(floor, BandCeiling()) + AimReach;
            return new Vector2(point.x, Mathf.Clamp(point.y, floor, roof));
        }

        bool WantsReversal(float desired)
        {
            if (!_appeared) return false;
            if (Scouting || _reversalCooldown > 0f || _standDown) return false;
            if (_state == AiState.Recover || _state == AiState.Return) return false;
            if (_state == AiState.Tail) return false;
            if (Diving && _state != AiState.DiveRun) return false;

            float error = Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg,
                desired * Mathf.Rad2Deg));
            return error >= _config.reversalAngle;
        }

        void BeginReversal(float desired)
        {
            _reversalCooldown = ReversalCooldown;
            _loop.Begin(_heading, _config.loopSeconds);
        }

        void CancelReversal() => _loop.Cancel();

        void DriveReversal(float dt)
        {
            _loop.Step(dt);
            _heading = _loop.Heading;

            _angularVelocity = 0f;
            _roll.Tick(dt, _heading, false, _config.rotationSpeed);
            ApplyRotation();
        }

        float ChooseTurn(float desired, float dt)
        {
            if (!Scouting || RunningDown || _standDown || _state == AiState.Recover)
            {
                _turnDir = 0f;
                _turnClimb = false;
                return desired;
            }

            if (_turnClimb)
            {
                if (transform.position.y - _deck
                    < _config.safeAltitudeMargin + TurnClimbGain) return ClimbHeading();
                _turnClimb = false;
            }

            _turnCheck -= dt;

            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, desired * Mathf.Rad2Deg);
            if (Mathf.Abs(error) < TurnChoiceAngleDeg)
            {
                _turnDir = 0f;
                return desired;
            }

            if (_turnCheck > 0f) return desired;
            _turnCheck = TurnCheckInterval;

            if (_turnDir != 0f)
            {
                if (TurnClear(desired, _turnDir)) return desired;
                _turnDir = 0f;
            }

            float shortest = Mathf.Sign(error);
            if (TurnClear(desired, shortest))
            {
                _turnDir = 0f;
                return desired;
            }

            if (TurnClear(desired, -shortest))
            {
                _turnDir = -shortest;
                return desired;
            }

            _turnDir = 0f;
            _turnClimb = true;
            return ClimbHeading();
        }

        float KeepNoseUp(float heading)
        {
            if (!Scouting || RunningDown) return heading;
            if (_rb.position.y - _deck >= _config.safeAltitudeMargin) return heading;
            if (Mathf.Sin(heading) >= 0f) return heading;

            _turnDir = 0f;
            return Mathf.Cos(heading) >= 0f ? 0f : Mathf.PI;
        }

        float ClimbHeading()
        {
            float climb = TurnClimbAngleDeg * Mathf.Deg2Rad;
            return Mathf.Cos(_heading) >= 0f ? climb : Mathf.PI - climb;
        }

        bool TurnClear(float target, float dir)
        {
            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad;
            float travel = FlightSpeed() * TurnSimStep;
            float ease = _config.turnResponsiveness / Mathf.Max(0.0001f, _rb.mass);
            float floor = _config.safeAltitudeMargin;

            Vector2 p = _rb.position;
            float h = _heading;
            float t = 0f;
            int steps = Mathf.CeilToInt(TurnSimHorizon / TurnSimStep);

            for (int i = 0; i < steps; i++)
            {
                t += TurnSimStep;
                h += TurnLimitAt(h, dir, maxRate) * dir * TurnSimStep
                   * (1f - Mathf.Exp(-ease * t));
                p += new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * travel;

                if (p.y - TerrainAt(p.x) < floor) return false;

                if (Mathf.Abs(Mathf.DeltaAngle(h * Mathf.Rad2Deg, target * Mathf.Rad2Deg))
                    < TurnChoiceAngleDeg) break;
            }

            return true;
        }

        void SteerToHeading(float targetHeading, float dt)
        {
            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad;
            if (Diving) maxRate *= DiveTurnFactor;

            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, targetHeading * Mathf.Rad2Deg)
                        * Mathf.Deg2Rad;
            if (_turnDir != 0f && Mathf.Sign(error) != _turnDir)
                error += _turnDir * Mathf.PI * 2f;

            float rawRate = dt > 0f ? error / dt : 0f;
            float limit = TurnLimitAt(_heading, Mathf.Sign(rawRate), maxRate);
            float desiredRate = Mathf.Clamp(rawRate, -limit, limit);

            float approach = 1f - Mathf.Exp(-(_config.turnResponsiveness / _rb.mass) * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
            _roll.Tick(dt, _heading, PlaneRoll.Steady(_angularVelocity, maxRate),
                _config.rotationSpeed);
            ApplyRotation();
        }

        float TurnLimitAt(float heading, float dir, float maxRate)
        {
            if (!Scouting) return maxRate;

            float rightward = Mathf.Clamp01(-Mathf.Sin(heading) * dir);
            return maxRate * Mathf.Lerp(1f, Mathf.Clamp01(_config.turnBias), rightward);
        }

        void ApplyVelocity(float dt)
        {
            float speed = UpdateSpeed(dt);

            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * speed;
            Vector3 pos = _rb.position;
            float wall = _wallX + _bodyRadius;

            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            if (pos.x <= wall && vel.x < 0f) vel.x = 0f;
            if (_dodge.Active && dt > 0f) vel.z = (_dodge.Z - pos.z) / dt;

            _rb.linearVelocity = vel;

            bool clamped = false;
            if (pos.y > _ceilingY) { pos.y = _ceilingY; clamped = true; }
            if (pos.x < wall) { pos.x = wall; clamped = true; }
            if (clamped) _rb.position = pos;
        }

        float UpdateSpeed(float dt)
        {
            float cruise = _config.flySpeed;

            if (Scouting)
            {
                _speed = cruise;
            }
            else
            {
                float cap = TopSpeed;
                float floor = Diving
                    ? Mathf.Min(cap, cruise * Mathf.Max(1f, _config.diveSpeedMultiplier))
                    : cruise;

                _speed += -Mathf.Sin(_heading) * _config.diveAcceleration * dt;
                _speed -= (_speed - cruise) * _config.speedDrag * dt;
                _speed = Mathf.Clamp(_speed, floor, cap);
            }

            _engageSpeed = Mathf.Lerp(_engageSpeed, EngageTarget(),
                1f - Mathf.Exp(-_config.engageResponse * dt));

            return FlightSpeed();
        }

        float FlightSpeed()
        {
            float speed = Mathf.Max(_speed, _engageSpeed);
            if (_state == AiState.Return)
                speed = Mathf.Max(speed, _config.flySpeed * ReturnSpeedFactor);
            if (_state == AiState.Tail) speed = TailSpeed(speed);
            if (Scouting) speed = Mathf.Min(speed, TopSpeed);
            return Mathf.Max(1f, speed);
        }

        float TopSpeed => _config.flySpeed * Mathf.Max(1f, _config.maxSpeedMultiplier);

        float EngageTarget()
        {
            if (_target == null || _standDown) return 0f;

            Vector2 to = (Vector2)_target.position - (Vector2)_rb.position;
            if (to.magnitude <= _config.engageRange) return 0f;

            Vector2 run = _target.linearVelocity;
            if (Vector2.Dot(run, to) <= 0f) return 0f;

            return run.magnitude * _config.engageFactor;
        }

        void ApplyRotation()
        {
            float roll = _roll.Angle + _dodge.Bank + (_fall != null ? _fall.Roll : 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg)
                               * Quaternion.Euler(roll, 0f, 0f);
        }

        float HeadingTo(Vector2 point)
        {
            return Mathf.Atan2(point.y - transform.position.y, point.x - transform.position.x);
        }

        static float MeasureRadius(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return DefaultTargetRadius;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f;
        }

        void UpdateFiring()
        {
            if (_dodge.Active) return;

            if (_state == AiState.DiveRun)
            {
                if (!_onCamera || Reversing) return;
            }
            else
            {
                if (_target == null || !IsOnCamera(_target.position)) return;

                if (TargetDistance() > _config.maxFireRange) return;

                if (!HasFiringSolution(PredictIntercept())
                    && !HasFiringSolution(_target.position)) return;
            }

            if (_fireCooldown > 0f) return;
            _fireCooldown = Mathf.Max(0.01f, _config.fireRate);

            Vector3 dir = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f);
            Vector3 muzzle = transform.position + dir * (_bodyRadius + 6f);
            var go = Instantiate(_bulletTemplate, muzzle,
                transform.rotation * Quaternion.Euler(0f, 0f, -90f));
            go.name = "EnemyBullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, _config.bulletSpeed, _config.damage, _collider,
                fromEnemy: true);

            MuzzleFlash.Spawn(muzzle, dir, _bodyRadius);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Sfx);
        }

        bool HasFiringSolution(Vector2 point)
        {
            float range = Vector2.Distance(transform.position, point);
            if (range < 1f) return false;

            float errorDeg = Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg,
                HeadingTo(point) * Mathf.Rad2Deg));
            if (errorDeg > Mathf.Max(_config.fireAngleThreshold, SnapFireConeDeg)) return false;

            return range * Mathf.Sin(errorDeg * Mathf.Deg2Rad)
                <= _targetRadius * SnapWindowFactor;
        }

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

        public void TakeDamage(float amount)
        {
            if (_dead || _falling || OffPlane) return;
            ApplyDamage(amount);
            if (_falling || _dodge.Active) return;

            if (_evadeCooldown <= 0f
                && (_state == AiState.Attack || _state == AiState.Fly)) EnterEvade(circling: false);
        }

        bool CanDodge()
        {
            if (!_appeared) return false;
            if (!Scouting || RunningDown || _standDown || _dodge.Active
                || _dodgeCooldown > 0f) return false;
            if (CurrentHealth > _config.health * _config.dodgeHealthFraction) return false;
            return UnderAim();
        }

        bool UnderAim()
        {
            if (_shooter == null || _target == null || !_shooter.Firing) return false;

            Vector2 to = (Vector2)_rb.position - (Vector2)_target.position;
            float range = to.magnitude;
            if (range < 1f || range > _config.dodgeAimRange) return false;

            return Vector2.Angle(_target.transform.right, to) <= _config.dodgeAimCone;
        }

        void BeginDodge()
        {
            _rb.constraints &= ~RigidbodyConstraints.FreezePositionZ;
            _dodge.Begin(_baseZ, _config.dodgeDepth, _config.dodgeBank, _config.dodgeRoll,
                _config.dodgeOut, _config.dodgeHold, _config.dodgeBack);
        }

        void ApplyDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            UpdateHealthBar();
            if (CurrentHealth < SmokeHealthThreshold && _smoke != null) _smoke.Arm(ModelSize);

            if (CurrentHealth <= 0f) BeginFall();
        }

        void BeginFall()
        {
            if (_dead || _falling) return;
            _falling = true;

            SetStreaks(false);
            CancelReversal();
            CancelDodge();

            if (_smoke != null) _smoke.Ignite(ModelSize);
            _fire = PlaneFire.Ignite(gameObject, ModelSize);

            _fall = PlaneFall.Begin(_rb, _heading, _config.flySpeed);
            if (_bar != null) Destroy(_bar.gameObject);

            ReportDestroyed();
        }

        void RemoveWreck()
        {
            _dead = true;
            if (_smoke != null) _smoke.Clear();

            ReportDestroyed();
            Destroy(gameObject);
        }

        void ReportDestroyed()
        {
            if (_reported) return;
            _reported = true;
            OnDestroyed?.Invoke(this);
        }

        public void Explode()
        {
            if (_dead) return;
            _dead = true;

            Explosion.Spawn(transform.position, ModelSize);
            if (_smoke != null) _smoke.Clear();
            if (_fire != null) _fire.Extinguish();

            if (_rb != null) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            if (_collider != null) _collider.enabled = false;
            if (_bar != null) Destroy(_bar.gameObject);

            ReportDestroyed();
            Destroy(gameObject, Explosion.RemovalDelay);
        }

        void OnTriggerEnter(Collider other)
        {
            if (RunningDown) return;
            if (other.gameObject.layer == BattlefieldProps.Layer) Scrape();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_dead) return;

            if (collision.gameObject.GetComponent<Bullet>() != null) return;

            if (collision.gameObject.GetComponent<Bomb>() != null) return;

            Explode();
        }

        public bool Scrape()
        {
            if (_dead || _falling || OffPlane) return false;
            if (Time.time - _lastCollisionTime < CollisionCooldown) return false;
            _lastCollisionTime = Time.time;

            ApplyDamage(CollisionDamage);
            if (!_falling)
            {
                if (_shake != null) _shake.Play();
                Sparks.Spawn(transform.position, ModelSize);
            }
            return true;
        }

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

            var color = Color.Lerp(new Color(0.95f, 0.2f, 0.12f), new Color(0.25f, 0.9f, 0.3f), frac);
            var mat = _barFill.sharedMaterial;
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
            if (_runDown == this) _runDown = null;
            if (_bar != null) Destroy(_bar.gameObject);
        }
    }
}
