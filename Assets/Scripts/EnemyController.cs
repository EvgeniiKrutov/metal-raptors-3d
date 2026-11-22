using System;
using UnityEngine;

namespace MetalRaptors
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyController : MonoBehaviour, IDamageable, IScoreTarget
    {
        const float ShotVolume = 0.18f;
        const float RecoverClimbAngleDeg = 70f;
        const float RecoverLookaheadSec = 1f;
        const int RecoverProbeSteps = 6;
        const float RecoverSideBias = 15f;
        const float RunDownClearance = 30f;
        const float RunDownLookaheadSec = 0.6f;
        const int UrgencyProbeSteps = 8;
        const float UrgencyLeadSec = 0.4f;
        const float UrgencyMinLookSec = 0.6f;
        const float UrgencyMaxLookSec = 2.2f;
        const float UrgencyBlendGain = 1.5f;
        const float UrgencyReversalCancel = 0.5f;
        const float PullOutStep = 0.1f;
        const float PullOutHorizon = 2.5f;
        const float PullOutCommitSec = 0.1f;
        const float PullOutDoneDeg = 10f;
        const float PullOutSoftGap = 40f;
        const float AvoidClimbDeg = 35f;
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

        const int BurstMin = 3, BurstMax = 5;
        const float BurstGapMin = 1.5f, BurstGapMax = 2.5f;
        const float FirstBurstSec = 1.5f;
        const float BurstSpreadDeg = 4f;
        const float BurstTrueChance = 0.4f;
        const float BurstTrueDeg = 1.5f;
        const float BurstMissMinDeg = 6f, BurstMissMaxDeg = 10f;
        const float BurstBreakFactor = 2f;

        const float SmokeHealthThreshold = 30f;
        const float BlazeScale = 0.55f;

        const float BandPushMargin = 80f;
        const float BandPushFraction = 0.4f;
        const float GroundProbe = 400f;
        const float PressReach = 80f;
        const float PressBreakFraction = 0.6f;
        const float ReversalCooldown = 2.5f;
        const float ManoeuvreTopMargin = 40f;
        const float ManoeuvreFloorLift = 40f;
        const float StrikeRangeFactor = 1.6f;
        const float StrikeNoseLead = 60f;
        const float StrikeLockDeg = 5f;
        const int StrikeLeadPasses = 2;
        const float StrikeBoostResponse = 3.5f;
        const float AimReach = 150f;

        const float TailStandoff = 95f;
        const float TailSlotTolerance = 55f;
        const float TailEnterConeDeg = 75f;
        const float TailBreakConeDeg = 105f;
        const float TailBreakSeconds = 1.1f;
        const float TailRangeFactor = 1.4f;
        const float TailCloseRange = 220f;
        const float TailChaseFactor = 1.3f;
        const float TailLockReach = 260f;
        const float TailLockHeadingDeg = 45f;
        const float TailLockGive = 1.6f;
        const float TailDescentDeg = 35f;
        const float TailGunLineFactor = 6f;

        const float ScrollFloor = 1f;
        const float ScrollResponse = 3f;
        const float FlyLeadFraction = 0.5f;
        const float FlyBehindFraction = 0.35f;

        const float TurnChoiceAngleDeg = 30f;
        const float TurnCheckInterval = 0.35f;
        const float TurnSimStep = 0.15f;
        const float TurnSimHorizon = 2f;
        const float TurnClimbAngleDeg = 55f;
        const float TurnClimbGain = 60f;

        const float BarWidth = 36f;
        const float BarHeight = 3.2f;
        const float BarLiftMargin = 8f;

        const float ScriptRollRate = 300f;

        const float AceSpeedResponse = 2f;
        const float PlayerGunRange = 600f;
        const float PlayerLineClearDeg = 24f;

        const float SectorFront = 0.5f;
        const float SectorBehind = 0.45f;
        const float SectorHeadOn = 0.6f;
        const float SectorLift = 160f;
        const float SectorReach = 110f;
        const float SectorMaxSec = 3f;
        const float SectorEngageFraction = 0.5f;

        const float CutOffBehindDot = -0.5f;
        const float CutOffLead = 0.6f;
        const float CutOffMaxSec = 4f;
        const int RangeSlots = 7;
        const float RangeStep = 0.25f;
        const float BaitRestSec = 4f;
        const float GunEyeSec = 0.25f;
        const float RookieSectorMaxSec = 1.5f;
        const float RookieAttackFactor = 1.5f;
        const float RookieEngagedDeg = 45f;
        const float DiveOutRestSec = 4f;
        const float FeintRestSec = 3f;
        const float LandReach = 26f;
        const float HomeTimeout = 14f;

        public event Action<EnemyController> OnDestroyed;

        public float CurrentHealth { get; private set; }

        public bool IsAlive => !_dead && !_falling;

        public bool Falling => _falling && !_dead;

        public bool ByPlayer => _byPlayer;

        public Collider Hitbox => _collider;

        public float ModelSize => _bodyRadius > 0f ? _bodyRadius * 2f : 30f;

        public float Heading => _heading;

        public float AngularVelocity => _angularVelocity;

        public float MaxHealth => _config != null ? Mathf.Max(1f, _config.health) : 1f;

        public float MaxTurnRate => _config != null ? _config.rotationSpeed * Mathf.Deg2Rad : 0f;

        public float EnergySpeed => _energy.Speed;

        public PilotLevel Pilot { get; private set; } = PilotLevel.Regular;

        PilotSkill Skill { get; set; } = PilotSkills.For(PilotLevel.Regular);

        public bool Exposed { get; set; }

        public bool Throttling { get; set; }

        public bool Featured { get; set; }

        public bool Pursuing { get; set; }

        public bool HoldFire { get; set; }

        public bool StandingDown => _standDown;

        public static bool AimedFire { get; set; }

        public float EnginePitch { get; set; } = 1f;

        public float HealthFloor { get; set; }

        public float Spin { get; set; }

        public bool Blazing { get; private set; }

        public bool BossFight { get; set; }

        public bool Free => !_dead && !_falling && !_scripted && !_standDown && _config != null;

        public bool HoldsTail => _state == AiState.Tail;

        public float TargetRange => TargetDistance();

        public bool Evading => _barrel.Active;

        public float BodyLow => _bodyLow;

        public float BodyHigh => _bodyHigh;

        public float BodyBack => _bodyBack;

        public float BodyFront => _bodyFront;

        static readonly Color SputterColor = new Color(0.92f, 0.92f, 0.9f, 0.6f);

        enum AiState { Attack, Fly, Evade, Recover, Return, StrikeAim, StrikeRun, Tail, Tactic }

        EnemyConfig _config;
        Rigidbody _target;
        Rigidbody _pace;
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
        bool _byPlayer;
        float _fallTimer;
        Vector3? _fallTarget;
        Action _landed;
        bool _standDown;

        float _minX, _maxX, _groundY, _ceilingY, _edgeMargin;
        float _scroll;

        AiState _state = AiState.Attack;
        float _stateTimer;
        float _evadeCooldown;
        float _circleTimer;
        float _circleDir;
        EvadeMove _lastEvade = EvadeMove.Break;
        readonly EvadeMove[] _evadePool = new EvadeMove[5];
        float _flyWeaveT;
        float _flyAnchorX;
        float _flyLeadX;
        bool _flyTrack;
        float _tailLost;
        bool _tailLocked;
        float _fireCooldown;
        int _burstLeft;
        float _burstGap;
        float _burstError;
        int _dodgeRoll;
        float _seenFor;
        float _lastCollisionTime = -999f;
        ShakeEffect _shake;
        SmokeTrail _smoke;
        SmokeTrail _sputter;
        PlaneFire _fire;
        readonly PlaneRoll _roll = new PlaneRoll(true);
        PlaneEnergy _energy;
        float _lean;
        float _urgency;
        float _avoidHeading;
        float _avoidSide = 1f;
        float _pullOut = float.MaxValue;

        readonly TargetView _view = new TargetView();
        float _decideTimer;
        float _hitPending;

        Vector2 SeenPos { get; set; }

        Vector2 SeenVel { get; set; }

        Vector2 AimPos { get; set; }

        Vector2 AimVel { get; set; }

        bool Deciding { get; set; }

        readonly EnemyLoop _loop = new EnemyLoop();
        readonly EnemyDepthDodge _dodge = new EnemyDepthDodge();
        readonly EnemyEvade _evade = new EnemyEvade();
        readonly EnemyBarrel _barrel = new EnemyBarrel();
        float _barrelHold;
        float _aceSpeed;

        readonly RookieTactics _rookie = new RookieTactics();
        readonly RegularTactics _regular = new RegularTactics();
        readonly VeteranTactics _vet = new VeteranTactics();
        float _tacticHeading;
        bool _tacticalEvade;
        float _tailHeld;
        float _onSix;
        float _diveOutRest;
        float _feintRest;
        bool _baiting;
        float _baitRest;
        bool _cutOff;
        float _flyLeadFraction;
        ApproachSector _sector;
        bool _sectorReached = true;
        float _sectorTimer;
        float _halfView = 400f;
        readonly float[] _rangeLog = new float[RangeSlots];
        int _rangeHead;
        int _rangeCount;
        float _rangeTimer;

        float _deck;
        float _baseZ;
        bool _scripted;
        Vector3 _scriptPos;
        float _speed;
        float _engageSpeed;
        float _reversalCooldown;
        float _dodgeCooldown;
        float _strikeCooldown;
        float _strikeHeading;
        float _boost = 1f;
        float _pressTimer;
        float _pressHold;
        float _turnDir;
        float _turnCheck;
        bool _turnClimb;
        float _recoverSide = 1f;
        bool _onCamera = true;
        bool _appeared;
        WingStreaks _streaks;

        GameObject _bulletTemplate;
        Vector3 _gunLocal;
        bool _gunMounted;
        Vector3? _noseLocal;
        Vector3? _engineLocal;
        float _bodyLow;
        float _bodyHigh;
        float _bodyBack;
        float _bodyFront;
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
            _aceSpeed = config.flySpeed;
            _energy.Reset(config.flySpeed);
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
            MeasureBand();
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

            ResetView();
            BuildHealthBar();
            ApplyRotation();
        }

        void ResetView()
        {
            Vector2 pos = _target != null ? (Vector2)_target.position : (Vector2)transform.position;
            Vector2 vel = _target != null ? (Vector2)_target.linearVelocity : Vector2.zero;
            _view.Reset(pos, vel);
            SeenPos = pos;
            SeenVel = vel;
            AimPos = pos;
            AimVel = vel;
            Deciding = false;
            _decideTimer = 0f;
            _hitPending = 0f;
            _burstLeft = 0;
            _burstGap = 0f;
            _rangeCount = 0;
            _rangeHead = 0;
            _rangeTimer = 0f;
        }

        void TickView(float dt)
        {
            if (_target != null)
            {
                _view.Record(Time.time, _target.position, _target.linearVelocity);
                _view.Sample(Time.time, Skill.reactionSec, out Vector2 pos, out Vector2 vel);
                SeenPos = pos;
                SeenVel = vel;
                _view.Sample(Time.time, Mathf.Min(Skill.reactionSec, GunEyeSec),
                    out Vector2 aimPos, out Vector2 aimVel);
                AimPos = aimPos;
                AimVel = aimVel;
            }

            _decideTimer -= dt;
            Deciding = _decideTimer <= 0f;
            if (Deciding)
                _decideTimer = Skill.reactionSec * UnityEngine.Random.Range(0.75f, 1.25f);

            _halfView = MeasureHalfView();
            _baitRest = Mathf.Max(0f, _baitRest - dt);
            _diveOutRest = Mathf.Max(0f, _diveOutRest - dt);
            _feintRest = Mathf.Max(0f, _feintRest - dt);
            _rookie.Tick(dt);
            TickRange(dt);
        }

        void TickRange(float dt)
        {
            _rangeTimer -= dt;
            if (_rangeTimer > 0f) return;

            _rangeTimer = RangeStep;
            _rangeHead = (_rangeHead + 1) % RangeSlots;
            _rangeLog[_rangeHead] = TargetDistance();
            _rangeCount = Mathf.Min(_rangeCount + 1, RangeSlots);
        }

        bool RangeOpening()
        {
            if (_rangeCount < RangeSlots) return false;
            return TargetDistance() >= _rangeLog[(_rangeHead + 1) % RangeSlots];
        }

        float MeasureHalfView()
        {
            if (_cam != null)
            {
                Transform cam = _cam.transform;
                float depth = Vector3.Dot(transform.position - cam.position, cam.forward);
                if (depth > 1f)
                {
                    float left = _cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
                    float right = _cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x;
                    float half = (right - left) * 0.5f;
                    if (half > 1f) return half;
                }
            }
            return Mathf.Max(1f, (_maxX - _minX) * 0.5f);
        }

        void CancelTactics()
        {
            _rookie.Cancel();
            _regular.Cancel();
            _vet.Reset();
            _tacticalEvade = false;
            _tailHeld = 0f;
            _onSix = 0f;
            _baiting = false;
            _cutOff = false;
            _flyLeadFraction = 0f;
            _sector = ApproachSector.None;
            _sectorReached = true;
            _sectorTimer = 0f;
        }

        public void AssignPilot(PilotLevel level)
        {
            Pilot = level;
            Skill = PilotSkills.For(level);
            EnemyWing.Join(this);
            CancelTactics();
            _rookie.Reset();
        }

        public void Restore(float headingRad, float health)
        {
            _heading = headingRad;
            CurrentHealth = Mathf.Clamp(health, 1f, Mathf.Max(1f, _config.health));
            ResetView();
            UpdateHealthBar();
            if (CurrentHealth < SmokeHealthThreshold && _smoke != null) _smoke.Arm(ModelSize);
            ApplyRotation();
        }

        public void StandDown()
        {
            _standDown = true;
            SetStreaks(false);
            _evade.Cancel();
            _loop.Cancel();
            CancelAbility();
            EnemyWing.Release(this);
            CancelTactics();
        }

        public void SetBounds(float minX, float maxX)
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                float rate = Mathf.Max(0f, (minX - _minX) / dt);
                _scroll = Mathf.Lerp(_scroll, rate, 1f - Mathf.Exp(-ScrollResponse * dt));
            }

            _minX = minX;
            _maxX = maxX;
        }

        public void MountNose(Vector3 local) => _noseLocal = local;

        public void PaceWith(Rigidbody pace) => _pace = pace;

        public Vector3 NosePoint => transform.TransformPoint(_noseLocal ?? Vector3.zero);

        public void MountEngine(Vector3? local)
        {
            _engineLocal = local;
            if (local.HasValue && _smoke != null) _smoke.Mount = local.Value;
        }

        Vector3? FireLocal => _engineLocal ?? _noseLocal;

        public void MountGun(Vector3 local)
        {
            _gunLocal = local;
            _gunMounted = true;
        }

        public void Fire(float bulletSpeed, float damage)
        {
            if (_dead || _falling || _bulletTemplate == null) return;
            Shoot(bulletSpeed, damage);
        }

        public void Blaze()
        {
            if (_dead || _falling || Blazing) return;

            Blazing = true;
            if (_fire == null) _fire = PlaneFire.Ignite(gameObject, ModelSize * BlazeScale, FireLocal);
            if (_smoke == null) return;

            if (FireLocal.HasValue) _smoke.Mount = FireLocal.Value;
            _smoke.Arm(ModelSize);
        }

        public void Sputter(bool on)
        {
            if (!on)
            {
                if (_sputter != null) _sputter.Settle();
                return;
            }

            if (_dead || _falling) return;
            if (_sputter == null)
            {
                _sputter = gameObject.AddComponent<SmokeTrail>();
                _sputter.Tint(SputterColor);
                if (_engineLocal.HasValue) _sputter.Mount = _engineLocal.Value;
            }
            _sputter.Arm(ModelSize);
        }

        public void Reappear(float x)
        {
            if (_dead || _falling || _config == null || _scripted) return;

            EnemyConfigs.SpawnBand(_config, _groundY, _ceilingY, out float minY, out float maxY);

            CancelDodge();
            CancelReversal();
            StopAbility();
            _evade.Cancel();
            EndStrike();
            EnemyWing.Release(this);
            CancelTactics();
            _rookie.Reset();
            _tailLocked = false;

            var pos = new Vector3(x, UnityEngine.Random.Range(minY, maxY), _baseZ);
            _rb.position = pos;
            transform.position = pos;
            _rb.linearVelocity = Vector3.zero;

            _speed = _config.flySpeed;
            _aceSpeed = _config.flySpeed;
            _energy.Reset(_config.flySpeed);
            _engageSpeed = 0f;
            _boost = 1f;
            _angularVelocity = 0f;
            _lean = 0f;
            _heading = _target != null
                ? Mathf.Atan2(_target.position.y - pos.y, _target.position.x - pos.x)
                : Mathf.PI;

            _onCamera = false;
            _appeared = false;
            ResetView();
            EnterAttack();
            ApplyRotation();
        }

        public EnemyRole Role => _config != null ? _config.role : EnemyRole.Fighter;

        bool Scouting => Role == EnemyRole.Scout;

        bool Ace => EnemyRoles.IsAce(Role);

        bool Contouring => Scouting || Ace;

        public bool RunningDown => _state == AiState.Tail && EnemyWing.HasTail(this) && _tailLocked;

        bool Reversing => _loop.Active;

        bool Striking => _state == AiState.StrikeAim || _state == AiState.StrikeRun;

        static PlayerConfig _rBoost;

        static PlayerConfig RBoost
        {
            get
            {
                if (_rBoost == null) _rBoost = Resources.Load<PlayerConfig>("PlayerConfig");
                if (_rBoost == null) _rBoost = ScriptableObject.CreateInstance<PlayerConfig>();
                return _rBoost;
            }
        }

        public bool OffPlane => _dodge.Clear;

        public Rigidbody Body => _rb;

        float GroundRef => Contouring ? _deck : _groundY;

        float GroundAt(float x) => Contouring ? TerrainAt(x) : _groundY;

        public void BeginScript()
        {
            if (_dead || _falling || _scripted || _rb == null) return;

            _scripted = true;
            _scriptPos = _rb.position;
            CancelDodge();
            CancelReversal();
            StopAbility();
            _evade.Cancel();
            EndStrike();
            SetStreaks(false);
            EnemyWing.Release(this);
            CancelTactics();
            _rookie.Reset();
            _tailLocked = false;

            _rb.linearVelocity = Vector3.zero;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.isKinematic = true;
        }

        public void ScriptTo(Vector3 position, float headingRad)
        {
            if (!_scripted) return;

            _scriptPos = new Vector3(position.x, position.y, _baseZ);
            _heading = headingRad;
            _angularVelocity = 0f;
            _lean = 0f;
            _roll.Settle(headingRad);
            ApplyRotation();
        }

        public void ScriptSteer(Vector3 position, float headingRad, float dt)
        {
            if (!_scripted) return;

            float turn = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, headingRad * Mathf.Rad2Deg);
            _angularVelocity = dt > 0f ? turn * Mathf.Deg2Rad / dt : 0f;
            _scriptPos = new Vector3(position.x, position.y, _baseZ);
            _heading = headingRad;
            _roll.Flip(headingRad, ScriptRollRate);
            _roll.Tick(dt, headingRad, false, ScriptRollRate);
            StepLean(MaxTurnRate, dt);
            ApplyRotation();
        }

        public void EndScript()
        {
            if (!_scripted) return;
            _scripted = false;

            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _speed = _config.flySpeed;
            _energy.Reset(_config.flySpeed);
            _rb.linearVelocity = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * _speed;

            _onCamera = true;
            _appeared = true;
            ResetView();
            CancelTactics();
            _rookie.Reset();
            EnterAttack();
        }

        public void HideHealthBar()
        {
            if (_bar != null) Destroy(_bar.gameObject);
            _bar = null;
        }

        void FixedUpdate()
        {
            if (_dead || _config == null) return;

            if (_scripted)
            {
                _rb.MovePosition(_scriptPos);
                if (Pursuing && !_standDown && !_falling) PursueFire(Time.fixedDeltaTime);
                return;
            }

            float dt = Time.fixedDeltaTime;

            if (_falling)
            {
                TickFall(dt);
                return;
            }

            _stateTimer = Mathf.Max(0f, _stateTimer - dt);
            _evadeCooldown = Mathf.Max(0f, _evadeCooldown - dt);
            _reversalCooldown = Mathf.Max(0f, _reversalCooldown - dt);
            _strikeCooldown = Mathf.Max(0f, _strikeCooldown - dt);
            _fireCooldown -= dt;
            _burstGap -= dt;

            _onCamera = IsOnCamera(transform.position);
            _appeared |= _onCamera;

            TickView(dt);
            TickDeck();
            TickDodge(dt);
            TickPress(dt);
            TickCircle(dt);
            TickAbility(dt);
            TickHit(dt);

            _avoidSide = ClearerSide();
            _avoidHeading = AvoidHeading();
            _pullOut = PullOutClearance();
            _urgency = Mathf.Max(GroundUrgency(), PullOutUrgency());

            if (_standDown)
            {
                if (_state != AiState.Fly) EnterFly(transform.position.x, track: false);
                _flyWeaveT += dt;
            }
            else if (CheckGroundAvoidance())
            {
                CancelReversal();
            }
            else if (!_onCamera && !Ace && _state != AiState.Recover && !Striking
                     && _state != AiState.Tactic)
            {
                _state = AiState.Return;
                CancelReversal();
            }
            else
            {
                if (_state == AiState.Return) EnterAttack();
                if (!Reversing) TickState(dt);
            }

            if (Reversing && _urgency > UrgencyReversalCancel) CancelReversal();

            if (Reversing) DriveReversal(dt);
            else
            {
                float desired = Contain(ComputeHeading());
                if (_urgency > 0f && _state != AiState.Recover)
                    desired =Mathf.LerpAngle(desired * Mathf.Rad2Deg, _avoidHeading * Mathf.Rad2Deg,
                        Mathf.SmoothStep(0f, 1f, _urgency * UrgencyBlendGain)) * Mathf.Deg2Rad;
                if (WantsReversal(desired)) BeginReversal(desired);
                else SteerToHeading(KeepNoseUp(ChooseTurn(desired, dt)), dt);
            }

            ApplyVelocity(dt);

            if (!_standDown && _state != AiState.Return && _state != AiState.Recover)
                UpdateFiring();
        }

        void PursueFire(float dt)
        {
            _fireCooldown -= dt;
            _burstGap -= dt;
            _onCamera = IsOnCamera(transform.position);
            _appeared |= _onCamera;
            _seenFor = _onCamera ? _seenFor + dt : 0f;

            if (!_onCamera || HoldFire || _seenFor < FirstBurstSec || _dodge.Active
                || _target == null)
            {
                _burstLeft = 0;
                return;
            }

            if (_burstLeft <= 0)
            {
                if (_burstGap > 0f || !BurstReady()) return;

                _burstLeft = UnityEngine.Random.Range(BurstMin, BurstMax + 1);
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                _burstError = AimedFire || UnityEngine.Random.value < BurstTrueChance
                    ? UnityEngine.Random.Range(-BurstTrueDeg, BurstTrueDeg)
                    : side * UnityEngine.Random.Range(BurstMissMinDeg, BurstMissMaxDeg);
            }
            else if (AimError(_target.position) > _config.fireAngleThreshold * BurstBreakFactor)
            {
                EndBurst();
                return;
            }

            if (_fireCooldown > 0f) return;
            _fireCooldown = Mathf.Max(0.01f, _config.fireRate);

            Shoot(_config.bulletSpeed, _config.damage,
                _burstError + UnityEngine.Random.Range(-BurstSpreadDeg, BurstSpreadDeg));
            if (--_burstLeft <= 0) EndBurst();
        }

        bool BurstReady() =>
            IsOnCamera(_target.position) && TrueDistance() <= _config.maxFireRange
            && (HasFiringSolution(TrueIntercept()) || HasFiringSolution(_target.position));

        void EndBurst()
        {
            _burstLeft = 0;
            _burstGap = UnityEngine.Random.Range(BurstGapMin, BurstGapMax);
        }

        float AimError(Vector2 point) =>
            Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, HeadingTo(point) * Mathf.Rad2Deg));

        public bool SteerFall(Vector3 target, Action landed)
        {
            if (!_falling || _dead || _fall == null) return false;

            _fallTarget = target;
            _landed = landed;
            _fallTimer = 0f;
            _rb.constraints &= ~RigidbodyConstraints.FreezePositionZ;
            return true;
        }

        void TickFall(float dt)
        {
            _barrel.Step(dt);
            _lean -= _lean * (1f - Mathf.Exp(-FlightSmoothing.LeanResponse * dt));
            if (_fallTarget.HasValue)
            {
                Vector3 target = _fallTarget.Value;
                _fall.Home(_rb, target, dt);
                _heading = _fall.Heading;
                ApplyRotation();

                _fallTimer += dt;
                Vector2 gap = (Vector2)_rb.position - (Vector2)target;
                if (gap.sqrMagnitude <= LandReach * LandReach || _fallTimer >= HomeTimeout) Explode();
                return;
            }

            _fall.Step(_rb, dt);
            _heading = _fall.Heading;
            ApplyRotation();

            _fallTimer += dt;
            if (_fallTimer >= PlaneFall.Timeout) RemoveWreck();
        }

        void Land()
        {
            Action landed = _landed;
            _landed = null;
            _fallTarget = null;
            landed?.Invoke();
        }

        void TickDeck()
        {
            if (!Contouring)
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
                || SeenPos.y > BandCeiling() + PressReach;

            _pressTimer = outOfReach ? _pressTimer + dt : 0f;
            if (_pressTimer >= _config.pressDelay) _pressHold = _config.pressDuration;
        }

        bool CheckGroundAvoidance()
        {
            bool trapped = _pullOut < HardClearance;
            if (_state == AiState.Recover) return trapped || GroundThreat();

            if (trapped)
            {
                EnterRecover();
                return true;
            }

            if (RunningDown) return false;
            if (!GroundThreat()) return false;
            if (_urgency < 1f) return false;

            EnterRecover();
            return true;
        }

        float HardClearance => _bodyRadius + RunDownClearance;

        float UrgencyMargin =>
            RunningDown ? HardClearance + PullOutSoftGap
            : Striking || VeteranRunning ? _config.minAltitudeMargin
            : _config.safeAltitudeMargin;

        float PullOutUrgency()
        {
            float hard = HardClearance;
            float soft = Mathf.Max(hard + 1f, UrgencyMargin);
            return Mathf.Clamp01((soft - _pullOut) / (soft - hard));
        }

        float PullOutClearance()
        {
            Vector2 p = _rb.position;
            float h = _heading;
            float w = _angularVelocity;
            float cruise = _config.flySpeed;
            PlaneEnergy energy = _energy;
            float scale = FlightSpeed() / Mathf.Max(1f, energy.Speed);
            float recover = cruise * Mathf.Max(1f, _config.recoverBoost);
            float climb = RecoverClimbAngleDeg * Mathf.Deg2Rad;
            float target = _avoidSide >= 0f ? climb : Mathf.PI - climb;
            float response = _config.turnResponsiveness / Mathf.Max(0.0001f, _rb.mass);
            float done = Mathf.Sin(PullOutDoneDeg * Mathf.Deg2Rad);
            float lowest = p.y - GroundAt(p.x);
            int steps = Mathf.CeilToInt(PullOutHorizon / PullOutStep);

            for (int i = 1; i <= steps; i++)
            {
                bool pulling = i * PullOutStep > PullOutCommitSec;
                float speed = energy.Speed * scale;
                if (pulling && !Ace) speed = Mathf.Max(speed, recover);
                if (Scouting) speed = Mathf.Min(speed, TopSpeed);

                if (pulling)
                {
                    float maxRate = _config.rotationSpeed * Mathf.Deg2Rad
                                    * Mathf.Max(TurnBoostAt(speed), _boost);
                    float raw = Mathf.DeltaAngle(h * Mathf.Rad2Deg, target * Mathf.Rad2Deg)
                                * Mathf.Deg2Rad / PullOutStep;
                    float limit = TurnLimitAt(h, Mathf.Sign(raw), maxRate);
                    w = FlightSmoothing.StepTurnRate(w, Mathf.Clamp(raw, -limit, limit), response,
                        maxRate, PullOutStep);
                }

                h += w * PullOutStep;
                p += new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * (speed * PullOutStep);

                float gap = p.y - GroundAt(p.x);
                if (gap < lowest) lowest = gap;
                if (pulling && Mathf.Sin(h) >= done) break;

                energy.Step(h, cruise, _config.diveAcceleration, _config.speedDrag,
                    cruise * _config.energyMinFraction, TopSpeed, PullOutStep);
            }
            return lowest;
        }

        float GroundUrgency()
        {
            float margin = UrgencyMargin;

            Vector2 start = _rb.position;
            if (start.y - GroundRef < margin) return 1f;

            float maxRateDeg = Mathf.Max(1f, _config.rotationSpeed * Mathf.Max(TurnBoost(), _boost));
            float gap = Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, _avoidHeading * Mathf.Rad2Deg));
            float lookahead = Mathf.Clamp(gap / maxRateDeg + UrgencyLeadSec,
                UrgencyMinLookSec, UrgencyMaxLookSec);

            float speed = FlightSpeed();
            float step = lookahead / UrgencyProbeSteps;
            var line = new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            Vector2 arc = start;
            float h = _heading;

            for (int i = 1; i <= UrgencyProbeSteps; i++)
            {
                float t = i * step;
                h += _angularVelocity * step;
                arc += new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * (speed * step);
                Vector2 straight = start + line * (speed * t);

                if (arc.y - GroundAt(arc.x) < margin || straight.y - GroundAt(straight.x) < margin)
                    return 1f - t / lookahead;
            }
            return 0f;
        }

        float AvoidHeading()
        {
            float climb = AvoidClimbDeg * Mathf.Deg2Rad;
            return _avoidSide >= 0f ? climb : Mathf.PI - climb;
        }

        bool GroundThreat()
        {
            float margin = RunningDown ? _bodyRadius + RunDownClearance : _config.minAltitudeMargin;
            float lookahead = RunningDown ? RunDownLookaheadSec : RecoverLookaheadSec;

            Vector2 p = _rb.position;
            if (p.y - GroundRef < margin) return true;

            float travel = FlightSpeed() * (lookahead / RecoverProbeSteps);
            var step = new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading)) * travel;

            for (int i = 0; i < RecoverProbeSteps; i++)
            {
                p += step;
                if (p.y - GroundAt(p.x) < margin) return true;
            }
            return false;
        }

        void EnterRecover()
        {
            EndStrike();
            CancelReversal();
            _evade.Cancel();
            _dodge.Release();

            _state = AiState.Recover;
            _stateTimer = 0f;
            _recoverSide = ClearerSide();

            _tailLocked = false;
            _tailLost = 0f;
            EnemyWing.Release(this);
            CancelTactics();
            _diveOutRest = DiveOutRestSec;

            _pressTimer = 0f;
            _pressHold = 0f;
            _circleTimer = 0f;
            _turnDir = 0f;
            _turnClimb = false;

            _evadeCooldown = _config.evadeCooldown;
        }

        float ClearerSide()
        {
            float facing = Mathf.Cos(_heading) >= 0f ? 1f : -1f;

            float reach = Mathf.Max(1f, FlightSpeed() * RecoverLookaheadSec);
            float x = _rb.position.x;
            float right = GroundAt(x + reach);
            float left = GroundAt(x - reach);

            if (Mathf.Abs(right - left) < RecoverSideBias) return facing;
            return right <= left ? 1f : -1f;
        }

        void TickState(float dt)
        {
            if (_state == AiState.Recover)
            {
                if (transform.position.y - GroundRef >= _config.safeAltitudeMargin) EnterAttack();
                return;
            }

            if (Striking)
            {
                if (_stateTimer <= 0f || _target == null)
                {
                    EndStrike();
                    EnterAttack();
                }
                else if (_state == AiState.StrikeAim && StrikeLined())
                {
                    EnterStrikeRun();
                }
                else if (_state == AiState.StrikeRun && Deciding && Passed())
                {
                    EndStrike();
                    AfterPass();
                }
                return;
            }

            if (_state == AiState.Evade)
            {
                if (_stateTimer <= 0f)
                {
                    if (!_tacticalEvade) _evadeCooldown = _config.evadeCooldown;
                    _tacticalEvade = false;
                    _rookie.EndPanic();
                    EnterAttack();
                    return;
                }

                _evade.Step(dt, transform.position,
                    _target != null ? SeenPos : (Vector2)transform.position);
                return;
            }

            if (_state == AiState.Tactic)
            {
                TickTactic(dt);
                return;
            }

            if (Pilot == PilotLevel.Veteran)
            {
                TickVeteran(dt);
                return;
            }

            if (Deciding && WantsCutOff())
            {
                EnterCutOff();
                return;
            }

            if (TickBait(dt)) return;

            TickSector(dt);

            if (Deciding && WantsStrike())
            {
                EnterStrike();
                return;
            }

            if (_state == AiState.Tail)
            {
                TickTail(dt);
                return;
            }

            if (Deciding && _evadeCooldown <= 0f && WantsTail())
            {
                EnterTail();
                return;
            }

            if (Deciding && WantsPerch())
            {
                EnterPerch();
                return;
            }

            if (Deciding && _state == AiState.Attack && _stateTimer <= 0f)
            {
                if (TargetDistance() <= _config.maxFireRange)
                    EnterFly(_target != null ? SeenPos.x : transform.position.x,
                        track: _target != null);
                else
                    _stateTimer = AttackSeconds;
                return;
            }

            if (Deciding && _state == AiState.Fly && (_stateTimer <= 0f || CutOffDone()))
            {
                EnterAttack();
                return;
            }

            if (Deciding && _evadeCooldown <= 0f && UnderThreat())
            {
                EnterEvade(circling: false);
                return;
            }

            if (Deciding && _appeared && _circleTimer >= CircleLimit)
            {
                BreakCircle();
                return;
            }

            if (_state == AiState.Fly) _flyWeaveT += dt;
        }

        float CircleLimit => Pilot == PilotLevel.Regular ? RegularTactics.CircleSec : CircleSeconds;

        void BreakCircle()
        {
            if (Pilot == PilotLevel.Rookie) BeginZoom();
            else if (Pilot == PilotLevel.Regular) BeginExtend();
            else if (_evadeCooldown <= 0f) EnterEvade(circling: true);
        }

        void AfterPass()
        {
            if (Pilot == PilotLevel.Rookie)
            {
                BeginZoom();
                return;
            }

            if (Pilot == PilotLevel.Regular)
            {
                EnemyController bait = EnemyWing.Bait;
                if (bait != null && bait != this && BaitAllowed())
                {
                    EnemyWing.HandBait(this);
                    EnterBait();
                    return;
                }

                BeginExtend();
                return;
            }

            EnterAttack();
        }

        void EnterTactic()
        {
            _state = AiState.Tactic;
            _stateTimer = 0f;
            _tacticHeading = _heading;
            _tailLocked = false;
            _tailLost = 0f;
            _circleTimer = 0f;
            _circleDir = 0f;
            EnemyWing.ReleaseTail(this);
        }

        void BeginZoom()
        {
            EndStrike();
            EnterTactic();
            _rookie.BeginZoom(_heading);
        }

        void BeginExtend()
        {
            EndStrike();
            EnterTactic();
            _regular.BeginExtend(_heading);
        }

        void TickTactic(float dt)
        {
            if (_rookie.Zooming)
            {
                float zoom = _rookie.Step(dt, EnergySpeed, _config.flySpeed);
                if (!float.IsNaN(zoom))
                {
                    _tacticHeading = zoom;
                    return;
                }
            }

            if (_rookie.Looping)
            {
                if (!_rookie.LoopRunning)
                {
                    float sweep = RookieTactics.LoopSweep(_heading);
                    _rookie.StartLoop();
                    _reversalCooldown = ReversalCooldown;
                    _loop.Begin(_heading, _config.loopSeconds * sweep / Mathf.PI, sweep);
                    return;
                }

                _rookie.EndLoop();
                EnterPerch();
                return;
            }

            if (_regular.Extending)
            {
                float extend = _regular.Step(dt);
                if (!float.IsNaN(extend))
                {
                    _tacticHeading = extend;
                    return;
                }

                BeginChandelle();
                return;
            }

            EnterAttack();
        }

        void BeginChandelle()
        {
            var plan = new EvadePlan
            {
                move = EvadeMove.Chandelle,
                side = 1f,
                breakHeading = _heading,
                breakSeconds = _config.evadeDuration,
                jitterAmplitude = _config.jitterAmplitude,
                jitterHz = _config.jitterHz,
            };

            _evade.Begin(plan, _heading, transform.position,
                _target != null ? SeenPos : (Vector2)transform.position);

            _tacticalEvade = true;
            _circleTimer = 0f;
            _circleDir = 0f;
            _stateTimer = _evade.Seconds;
            _state = AiState.Evade;
        }

        void BeginPanic()
        {
            EndStrike();
            CancelReversal();
            _rookie.BeginPanic();
            _tailLocked = false;
            _tailLost = 0f;
            EnemyWing.ReleaseTail(this);

            var plan = new EvadePlan
            {
                move = EvadeMove.Extend,
                side = 1f,
                breakHeading = _heading,
                breakSeconds = _config.evadeDuration,
                jitterAmplitude = _config.jitterAmplitude,
                jitterHz = _config.jitterHz,
                extendSeconds = RookieTactics.PanicSeconds,
            };

            _evade.Begin(plan, _heading, transform.position,
                _target != null ? SeenPos : (Vector2)transform.position);

            _tacticalEvade = false;
            _circleTimer = 0f;
            _circleDir = 0f;
            _stateTimer = _evade.Seconds;
            _state = AiState.Evade;
        }

        bool WantsPerch()
        {
            if (Pilot != PilotLevel.Rookie || _state != AiState.Attack || _target == null) return false;
            if (!_appeared || _sector == ApproachSector.HeadOn || !_sectorReached) return false;
            if (_rb.position.y >= SeenPos.y + RookieTactics.StrikeAbove) return false;
            if (TargetDistance() <= FireRange && AimError(PredictIntercept()) <= RookieEngagedDeg)
                return false;
            return SeenPos.y + RookieTactics.StrikeAbove < BandCeiling();
        }

        void EnterPerch()
        {
            EnterFly(_target != null ? SeenPos.x : transform.position.x, track: _target != null);
            _stateTimer = Mathf.Max(_stateTimer, RookieTactics.PerchSeconds);
        }

        bool BaitAllowed() =>
            Pilot == PilotLevel.Regular && _appeared && _target != null && !_standDown
            && EnemyWing.Count(PilotLevel.Regular) >= 2;

        bool TickBait(float dt)
        {
            if (_baiting && (EnemyWing.Bait != this || !BaitAllowed()))
            {
                EnemyWing.ReleaseBait(this);
                _baiting = false;
                _baitRest = BaitRestSec;
                EnterAttack();
                return true;
            }

            if (!_baiting)
            {
                if (!Deciding || _baitRest > 0f || !BaitAllowed()) return false;
                if (_state != AiState.Attack && _state != AiState.Fly) return false;
                if (EnemyWing.HasTail(this) || !EnemyWing.ClaimBait(this)) return false;

                EnterBait();
                return true;
            }

            if (_state != AiState.Fly) EnterBait();

            if (Deciding && _evadeCooldown <= 0f && UnderThreat())
            {
                EnterEvade(circling: false);
                return true;
            }

            _flyWeaveT += dt;
            return true;
        }

        void EnterBait()
        {
            EnterFly(_target != null ? SeenPos.x : transform.position.x, track: _target != null);
            _baiting = true;
            _flyLeadFraction = UnityEngine.Random.Range(RegularTactics.BaitLeadMin,
                RegularTactics.BaitLeadMax);
            EnemyWing.ReleaseTail(this);
            EnemyWing.ReleaseSector(this);
        }

        void LoseBait()
        {
            if (!_baiting && EnemyWing.Bait != this) return;

            EnemyWing.ReleaseBait(this);
            _baiting = false;
            _baitRest = BaitRestSec;
        }

        float PlayerFacing()
        {
            if (Mathf.Abs(SeenVel.x) > 1f) return Mathf.Sign(SeenVel.x);
            return SeenPos.x >= _rb.position.x ? -1f : 1f;
        }

        bool Outrun()
        {
            if (_target == null) return false;

            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return false;

            Vector2 off = (Vector2)_rb.position - SeenPos;
            float range = off.magnitude;
            if (range <= _config.maxFireRange) return false;
            if (Vector2.Dot(run.normalized, off / range) >= CutOffBehindDot) return false;

            return RangeOpening();
        }

        bool WantsCutOff()
        {
            if (!_appeared || _standDown || _cutOff || _baiting) return false;
            if (_state != AiState.Attack && _state != AiState.Fly) return false;
            return Outrun();
        }

        void EnterCutOff()
        {
            EnemyWing.ReleaseTail(this);
            EnterFly(_target != null ? SeenPos.x : transform.position.x, track: _target != null);
            _cutOff = true;
            _flyLeadFraction = CutOffLead;
            _stateTimer = CutOffMaxSec;
        }

        bool CutOffDone()
        {
            if (!_cutOff || _target == null) return false;
            if (TargetDistance() <= _config.maxFireRange) return true;
            return Vector2.Dot(SeenVel, (Vector2)_rb.position - SeenPos) > 0f;
        }

        void TickSector(float dt)
        {
            if (_state != AiState.Attack || _sectorReached) return;

            _sectorTimer += dt;
            float limit = Pilot == PilotLevel.Rookie ? RookieSectorMaxSec : SectorMaxSec;
            if (_sector == ApproachSector.None || _target == null || _sectorTimer >= limit
                || Vector2.Distance(_rb.position, ClampToBand(SectorPoint())) <= SectorReach
                || TargetDistance() <= _config.maxFireRange * SectorEngageFraction)
                _sectorReached = true;
        }

        Vector2 SectorPoint()
        {
            float facing = PlayerFacing();
            switch (_sector)
            {
                case ApproachSector.AboveFront:
                    return SeenPos + new Vector2(facing * _halfView * SectorFront, SectorLift);
                case ApproachSector.AboveBehind:
                    return SeenPos + new Vector2(-facing * _halfView * SectorBehind, SectorLift);
                case ApproachSector.HeadOn:
                    return SeenPos + new Vector2(facing * _halfView * SectorHeadOn, 0f);
                default:
                    return SeenPos;
            }
        }

        void TickVeteran(float dt)
        {
            if (_state != AiState.Attack)
            {
                EnterAttack();
                return;
            }

            _vet.Tick(dt);
            _onSix = PlayerOnSix() ? _onSix + dt : 0f;

            switch (_vet.Phase)
            {
                case VeteranPhase.Wingover:
                    _vet.BeginSetup();
                    return;

                case VeteranPhase.Reverse:
                    _vet.BeginRun();
                    return;

                case VeteranPhase.Break:
                    if (_vet.BreakDone(OutOfPlayerLine())) BeginSetup();
                    return;

                case VeteranPhase.DiveOut:
                    if (_vet.DiveOutDone(EnergySpeed, _config.flySpeed))
                    {
                        _diveOutRest = DiveOutRestSec;
                        BeginSetup();
                    }
                    return;

                case VeteranPhase.Feint:
                    if (_vet.FeintDone) BeginSetup();
                    return;

                case VeteranPhase.Zoom:
                    if (_vet.ZoomDone(EnergySpeed, _config.flySpeed))
                    {
                        EndZoom();
                        return;
                    }
                    break;

                case VeteranPhase.Run:
                    if (RunOver())
                    {
                        _vet.BeginZoom(_heading);
                        return;
                    }
                    break;
            }

            if (!Deciding || _target == null) return;

            if (HeadOnThreat())
            {
                _vet.BeginBreak(_heading, BreakSide());
                return;
            }

            if (_onSix >= VeteranTactics.OnSixSec)
            {
                BeginReverse();
                return;
            }

            if (_diveOutRest <= 0f && Outrun())
            {
                _vet.BeginDiveOut(_heading);
                return;
            }

            if (_vet.Phase != VeteranPhase.Setup) return;

            if (_feintRest <= 0f && PlayerNoseOnMe(VeteranTactics.NoseOnMeDeg)
                && TargetDistance() <= VeteranTactics.FeintRange)
            {
                BeginFeint();
                return;
            }

            if (SetupReached() || RunOpening()) _vet.BeginRun();
        }

        void BeginSetup()
        {
            _vet.PickEntry();
            _vet.BeginSetup();
        }

        bool VeteranRunning =>
            Pilot == PilotLevel.Veteran && _state == AiState.Attack && _vet.Phase == VeteranPhase.Run;

        Vector2 SetupPoint()
        {
            float facing = PlayerFacing();
            switch (_vet.Entry)
            {
                case VeteranEntry.AboveFront:
                    return SeenPos + new Vector2(facing * _halfView * VeteranTactics.SetupFront,
                        VeteranTactics.SetupLift);
                case VeteranEntry.Overhead:
                    return SeenPos + new Vector2(0f, VeteranTactics.OverheadLift);
                default:
                    return SeenPos + new Vector2(-facing * _halfView * VeteranTactics.SetupBehind,
                        VeteranTactics.SetupLift);
            }
        }

        bool SetupReached() =>
            _vet.Elapsed >= VeteranTactics.SetupMaxSec
            || Vector2.Distance(_rb.position, ClampToBand(SetupPoint())) <= VeteranTactics.SetupReach;

        bool RunOpening() =>
            TargetDistance() <= FireRange
            && AimError(PredictIntercept()) <= VeteranTactics.RunOpenDeg
            && !PlayerNoseOnMe(VeteranTactics.NoseOnMeDeg);

        bool RunOver()
        {
            if (_target == null || _vet.Elapsed >= VeteranTactics.RunMaxSec) return true;
            if (TargetDistance() < VeteranTactics.RunCloseRange) return true;
            if (_vet.Elapsed < VeteranTactics.RunGraceSec) return false;
            return Passed() || AimError(PredictIntercept()) > VeteranTactics.RunAbortDeg;
        }

        void EndZoom()
        {
            _vet.PickEntry();
            Vector2 travel = new Vector2(Mathf.Cos(_heading), 0f);
            Vector2 toSetup = ClampToBand(SetupPoint()) - (Vector2)_rb.position;

            if (Vector2.Dot(travel, toSetup) >= 0f || _urgency > 0f)
            {
                _vet.BeginSetup();
                return;
            }

            float sweep = RookieTactics.LoopSweep(_heading);
            _vet.BeginWingover();
            _reversalCooldown = ReversalCooldown;
            _loop.Begin(_heading, _config.loopSeconds * sweep / Mathf.PI, sweep);
        }

        void BeginReverse()
        {
            _onSix = 0f;

            float y = transform.position.y;
            bool up = EnergySpeed >= _config.flySpeed * VeteranTactics.ReverseEnergy
                      && EvadeRoof() - y >= VeteranTactics.ReverseClimbRoom;
            bool down = _urgency <= 0f
                        && y - (GroundRef + _config.minAltitudeMargin) >= VeteranTactics.ReverseDiveRoom;

            if (!up && !down)
            {
                BeginFeint();
                return;
            }

            bool climb = up && (!down || UnityEngine.Random.value < 0.6f);
            float pitch = Mathf.Asin(Mathf.Clamp(Mathf.Sin(_heading), -1f, 1f));
            float sweep = Mathf.Clamp(climb ? Mathf.PI - pitch : Mathf.PI + pitch,
                Mathf.PI * 0.5f, Mathf.PI);

            _vet.BeginReverse();
            _reversalCooldown = ReversalCooldown;
            _loop.Begin(_heading, _config.loopSeconds * sweep / Mathf.PI, sweep, under: !climb);
        }

        void BeginFeint()
        {
            float y = transform.position.y;
            float roomUp = BandCeiling() - y;
            float roomDown = y - BandFloor();
            bool breakUp = Mathf.Abs(roomUp - roomDown) < BreakRoomTie
                ? UnityEngine.Random.value < 0.5f
                : roomUp > roomDown;
            float upSign = Mathf.Cos(_heading) >= 0f ? 1f : -1f;

            _feintRest = FeintRestSec;
            _vet.BeginFeint(_heading, breakUp ? -upSign : upSign);
        }

        float BreakSide()
        {
            bool roomBelow = transform.position.y - BandFloor() >= EvadeDiveRoom;
            if (!roomBelow) return 1f;

            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return 1f;

            Vector2 fwd = run.normalized;
            Vector2 up = fwd.x >= 0f ? new Vector2(-fwd.y, fwd.x) : new Vector2(fwd.y, -fwd.x);
            return Vector2.Dot((Vector2)_rb.position - SeenPos, up) >= 0f ? 1f : -1f;
        }

        bool PlayerNoseOnMe(float coneDeg)
        {
            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return false;

            Vector2 off = (Vector2)_rb.position - SeenPos;
            return off.sqrMagnitude > 1f && Vector2.Angle(run, off) <= coneDeg;
        }

        bool PlayerOnSix()
        {
            Vector2 to = SeenPos - (Vector2)_rb.position;
            float range = to.magnitude;
            if (range < 1f || range > _config.threatRange * VeteranTactics.HeadOnRangeFactor)
                return false;

            var nose = new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            if (Vector2.Angle(nose, to) < 180f - VeteranTactics.TailConeDeg) return false;

            return PlayerNoseOnMe(VeteranTactics.NoseOnMeDeg);
        }

        bool HeadOnThreat()
        {
            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return false;

            Vector2 fwd = run.normalized;
            var nose = new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            if (Vector2.Dot(nose, fwd) >= VeteranTactics.HeadOnDot) return false;

            Vector2 off = (Vector2)_rb.position - SeenPos;
            float range = off.magnitude;
            if (range < 1f || range > PlayerGunRange * VeteranTactics.HeadOnRangeFactor) return false;

            return Vector2.Angle(fwd, off) <= VeteranTactics.HeadOnConeDeg;
        }

        bool OutOfPlayerLine()
        {
            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return true;

            Vector2 off = (Vector2)_rb.position - SeenPos;
            return off.sqrMagnitude < 1f || Vector2.Angle(run, off) > PlayerLineClearDeg;
        }

        float VeteranHeading()
        {
            if (_target == null) return _heading;

            float setup = HeadingTo(ClampToBand(SetupPoint()));
            float run = HeadingTo(ClampToBand(PredictIntercept()));
            return _vet.Heading(_heading, setup, run);
        }

        void TickAbility(float dt)
        {
            _barrelHold = Mathf.Max(0f, _barrelHold - dt);

            bool rolling = _barrel.Active;
            _barrel.Step(dt);
            if (rolling && !_barrel.Active) EndBarrel();
            else if (WantsBarrel()) BeginBarrel();
        }

        void CancelAbility()
        {
            if (!_barrel.Active) return;

            _barrel.Cancel();
            _barrelHold = _config.barrelCooldown;
            SetStreaks(false);
        }

        void StopAbility()
        {
            CancelAbility();
            _barrel.Stop();
        }

        bool Passed()
        {
            if (_target == null) return true;

            var nose = new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            return Vector2.Dot(SeenPos - (Vector2)_rb.position, nose) < 0f;
        }

        bool WantsBarrel()
        {
            if (!Ace || _barrel.Spinning || _barrelHold > 0f || _standDown) return false;
            if (!_appeared || _target == null || !InFullView()) return false;

            return UnderAim(_config.barrelAimRange, _config.barrelAimCone);
        }

        bool InFullView()
        {
            if (_cam == null) return true;

            Vector3 p = transform.position;
            var corner = new Vector3(_bodyRadius, _bodyRadius, 0f);
            return InView(p - corner) && InView(p + corner);
        }

        bool InView(Vector3 worldPos)
        {
            Vector3 vp = _cam.WorldToViewportPoint(worldPos);
            return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        }

        void BeginBarrel()
        {
            _barrel.Begin(_config.barrelSeconds, _config.barrelTurns, Mathf.Cos(_heading));
            SetStreaks(true);
        }

        void EndBarrel()
        {
            SetStreaks(false);
            _barrelHold = _config.barrelCooldown;
        }

        void EnterAttack()
        {
            _state = AiState.Attack;
            _tailLocked = false;
            _stateTimer = AttackSeconds;
            _sectorTimer = 0f;
            _cutOff = false;
            _tacticalEvade = false;
            _rookie.EndPanic();

            if (Pilot == PilotLevel.Veteran)
            {
                BeginSetup();
                ClearSector();
                return;
            }

            if (_target != null && !_standDown && !_baiting && !EnemyWing.HasTail(this))
            {
                _sector = EnemyWing.ClaimSector(this, headOn: true);
                _sectorReached = false;
                return;
            }

            ClearSector();
        }

        float AttackSeconds =>
            _config.attackDuration * (Pilot == PilotLevel.Rookie ? RookieAttackFactor : 1f);

        void ClearSector()
        {
            EnemyWing.ReleaseSector(this);
            _sector = ApproachSector.None;
            _sectorReached = true;
        }

        void EnterTail()
        {
            _state = AiState.Tail;
            _stateTimer = 0f;
            _tailLost = 0f;
            _tailHeld = 0f;
            _tailLocked = false;
            ClearSector();
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
            if (Striking || UnderThreat()) return false;
            if (TargetDistance() > _config.maxFireRange * TailRangeFactor) return false;
            if (TailOffAngle() > TailEnterConeDeg) return false;

            return EnemyWing.ClaimTail(this);
        }

        void TickTail(float dt)
        {
            if (_target == null || !EnemyWing.HasTail(this) || UnderThreat()
                || TargetDistance() > _config.maxFireRange * TailRangeFactor)
            {
                EnemyWing.ReleaseTail(this);
                EnterAttack();
                return;
            }

            _tailHeld += dt;
            if (Pilot == PilotLevel.Regular && _tailHeld >= RegularTactics.TailHoldSec)
            {
                BeginExtend();
                return;
            }

            bool lost = TailOffAngle() > TailBreakConeDeg || (RunningDown && OffGunLine());
            _tailLost = lost ? _tailLost + dt : 0f;
            if (_tailLost >= TailBreakSeconds)
            {
                EnemyWing.ReleaseTail(this);
                EnterAttack();
                return;
            }

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
                HeadingTo(SeenPos) * Mathf.Rad2Deg));

            return range * Mathf.Sin(errorDeg * Mathf.Deg2Rad)
                > _targetRadius * TailGunLineFactor;
        }

        float TailBehind()
        {
            if (_target == null) return 0f;

            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return 0f;

            return Vector2.Dot((Vector2)transform.position - SeenPos, -run.normalized);
        }

        bool NoseOnTrack()
        {
            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return false;

            float track = Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, track))
                <= TailLockHeadingDeg;
        }

        float TailOffAngle()
        {
            if (_target == null) return 180f;

            Vector2 run = SeenVel;
            if (run.sqrMagnitude < 1f) return 0f;

            return Vector2.Angle(-run, (Vector2)transform.position - SeenPos);
        }

        Vector2 TailSlot()
        {
            if (_target == null) return transform.position;

            Vector2 run = SeenVel;
            Vector2 back = run.sqrMagnitude > 1f
                ? run.normalized
                : new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            return SeenPos - back * TailStandoff;
        }

        float TailHeading()
        {
            if (RunningDown && _target != null)
                return EaseDescent(HeadingTo(SeenPos));

            Vector2 slot = ClampToBand(TailSlot());
            float toSlot = HeadingTo(slot);

            float gap = Vector2.Distance(transform.position, slot);
            if (gap >= TailSlotTolerance) return toSlot;

            Vector2 run = _target != null ? SeenVel : Vector2.zero;
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

            float run = SeenVel.magnitude;
            if (run < 1f) return speed;

            float gap = RunningDown
                ? Mathf.Max(0f, TargetDistance() - TailStandoff)
                : Vector2.Distance(transform.position, ClampToBand(TailSlot()));
            float closing = Mathf.Clamp01(gap / TailCloseRange);
            return Mathf.Lerp(run, Mathf.Max(speed, run * TailChaseFactor), closing);
        }

        void EnterFly(float anchorX, bool track)
        {
            float behind = track ? Behind() : 0f;

            _state = AiState.Fly;
            _stateTimer = _config.flyDuration * Mathf.Lerp(1f, FlyBehindFraction, behind);
            _flyWeaveT = 0f;
            _flyTrack = track;
            _flyAnchorX = anchorX;
            _flyLeadX = behind * (_maxX - _minX) * 0.5f * FlyLeadFraction;
            _baiting = false;
            _cutOff = false;
            _flyLeadFraction = 0f;
        }

        float FlyBaseX()
        {
            if (!_flyTrack || _target == null) return _flyAnchorX;
            if (_flyLeadFraction > 0f) return SeenPos.x + PlayerFacing() * _halfView * _flyLeadFraction;
            return SeenPos.x + _flyLeadX;
        }

        float PerchHeight() =>
            Pilot == PilotLevel.Rookie ? RookieTactics.Perch : _config.flyPerchHeight;

        bool WantsStrike()
        {
            if (!_appeared || !_onCamera || _target == null) return false;
            if (Pilot == PilotLevel.Veteran) return false;
            if (Scouting && Pilot != PilotLevel.Rookie) return false;
            if (_strikeCooldown > 0f) return false;
            if (_state != AiState.Attack && _state != AiState.Fly && _state != AiState.Tail)
                return false;
            if (_state == AiState.Attack && !_sectorReached) return false;

            if (Pilot == PilotLevel.Rookie)
                return _rb.position.y >= SeenPos.y + RookieTactics.StrikeAbove
                       && TargetDistance() <= _config.maxFireRange * RookieTactics.StrikeReach;

            return TargetDistance() <= _config.maxFireRange * StrikeRangeFactor;
        }

        void EnterStrike()
        {
            _state = AiState.StrikeAim;
            _stateTimer = Mathf.Max(0.01f, RBoost.boostDuration);
            _tailLocked = false;
            _tailLost = 0f;
            _cutOff = false;
            EnemyWing.ReleaseTail(this);
            SetStreaks(true);
        }

        void EnterStrikeRun()
        {
            _state = AiState.StrikeRun;
            _strikeHeading = HeadingTo(StrikePoint());
        }

        void EndStrike()
        {
            if (!Striking) return;
            _strikeCooldown = Mathf.Max(0.01f, RBoost.boostCooldown);
            SetStreaks(false);
        }

        bool StrikeLined()
        {
            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg,
                HeadingTo(StrikePoint()) * Mathf.Rad2Deg);
            return Mathf.Abs(error) <= StrikeLockDeg;
        }

        Vector2 StrikePoint()
        {
            Vector2 at = SeenPos;
            Vector2 run = SeenVel;
            float speed = Mathf.Max(1f, FlightSpeed());

            Vector2 aim = at;
            for (int i = 0; i < StrikeLeadPasses; i++)
                aim = at + run * (Vector2.Distance(_rb.position, aim) / speed);

            return run.sqrMagnitude > 1f ? aim + run.normalized * StrikeNoseLead : aim;
        }

        void SetStreaks(bool on)
        {
            if (_streaks != null) _streaks.SetEmitting(on);
        }

        void EnterEvade(bool circling)
        {
            Vector2 away = (Vector2)transform.position
                         - (_target != null ? SeenPos : (Vector2)transform.position - Vector2.right);
            float awayHeading = Mathf.Atan2(away.y, away.x);
            float towardHeading = awayHeading + Mathf.PI;
            float breakAngle = _config.evadeBreakAngle * Mathf.Deg2Rad;
            float scissorAngle = EnemyEvade.ScissorsAngle * Mathf.Deg2Rad;

            float high = awayHeading + breakAngle;
            float low = awayHeading - breakAngle;
            float roomUp = Mathf.Max(0f, BandCeiling() - transform.position.y);
            float roomDown = Mathf.Max(0f, transform.position.y - BandFloor());

            EvadeMove move = Pilot == PilotLevel.Regular && !circling && PlayerOnTail()
                ? EvadeMove.Scissors
                : PickEvade(circling);
            bool breakHigh = PickSide(high, low, roomUp, roomDown);
            bool sideHigh = move == EvadeMove.Scissors
                ? PickSide(towardHeading + scissorAngle, towardHeading - scissorAngle,
                    roomUp, roomDown)
                : breakHigh;

            var plan = new EvadePlan
            {
                move = move,
                side = sideHigh ? 1f : -1f,
                breakHeading = breakHigh ? high : low,
                breakSeconds = _config.evadeDuration,
                jitterAmplitude = _config.jitterAmplitude,
                jitterHz = _config.jitterHz,
            };

            _evade.Begin(plan, _heading, transform.position,
                _target != null ? SeenPos : (Vector2)transform.position);

            _circleTimer = 0f;
            _circleDir = 0f;
            _stateTimer = _evade.Seconds;
            _state = AiState.Evade;
        }

        bool PlayerOnTail()
        {
            Vector2 to = SeenPos - (Vector2)_rb.position;
            float range = to.magnitude;
            if (range < 1f || range > _config.threatRange) return false;

            var tail = new Vector2(-Mathf.Cos(_heading), -Mathf.Sin(_heading));
            return Vector2.Angle(tail, to) <= RegularTactics.ScissorsConeDeg;
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

        static bool PickSide(float high, float low, float roomUp, float roomDown)
        {
            float highRoom = BreakRoom(high, roomUp, roomDown);
            float lowRoom = BreakRoom(low, roomUp, roomDown);

            return Mathf.Abs(highRoom - lowRoom) < BreakRoomTie
                ? UnityEngine.Random.value < 0.5f
                : highRoom > lowRoom;
        }

        static float BreakRoom(float heading, float roomUp, float roomDown)
        {
            return Mathf.Min(Mathf.Sin(heading) > 0f ? roomUp : roomDown, BreakRoomCap);
        }

        bool UnderThreat()
        {
            if (_target == null) return false;

            Vector2 toTarget = SeenPos - (Vector2)transform.position;
            float distance = toTarget.magnitude;
            if (distance < 1f || distance > _config.threatRange) return false;

            Vector2 aim = SeenVel;
            if (aim.sqrMagnitude < 1f) return false;
            if (Vector2.Angle(aim, -toTarget) > _config.threatCone) return false;

            var nose = new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading));
            return Vector2.Angle(nose, toTarget) > _config.threatTailAngle;
        }

        float TargetDistance()
        {
            return _target != null
                ? Vector2.Distance(transform.position, SeenPos)
                : float.MaxValue;
        }

        float TrueDistance()
        {
            return _target != null
                ? Vector2.Distance(transform.position, _target.position)
                : float.MaxValue;
        }

        float Behind()
        {
            if (_scroll <= ScrollFloor) return 0f;

            float half = (_maxX - _minX) * 0.5f;
            if (half <= 1f) return 0f;

            return Mathf.Clamp01((_minX + half - transform.position.x) / half);
        }

        float BandFloor()
        {
            if (Striking || VeteranRunning) return GroundRef + _config.minAltitudeMargin;

            if (Contouring || _baiting) return GroundRef + _config.safeAltitudeMargin;

            return Mathf.Max(AltitudeBands.Floor(AltitudeBand.High, _groundY, _ceilingY),
                _groundY + _config.safeAltitudeMargin);
        }

        float BandCeiling()
        {
            float roof;
            if (Scouting && Pilot == PilotLevel.Veteran)
            {
                roof = AltitudeBands.Ceiling(AltitudeBand.High, _groundY, _ceilingY);
            }
            else if (Scouting && Pilot == PilotLevel.Rookie)
            {
                roof = AltitudeBands.Ceiling(AltitudeBand.Mid, _groundY, _ceilingY);
            }
            else if (Scouting)
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
            if (_state == AiState.StrikeRun) return heading;

            float floor = BandFloor();
            float roof = Mathf.Max(floor + 1f, BandCeiling());

            if (_state == AiState.Evade)
            {
                if (_evade.Move == EvadeMove.Chandelle) roof = EvadeRoof();
                else if (_evade.Move == EvadeMove.SplitDive) floor = EvadeFloor();
                roof = Mathf.Max(floor + 1f, roof);
            }
            else if ((_state == AiState.Tactic && _rookie.Zooming)
                     || (Pilot == PilotLevel.Veteran && _state == AiState.Attack
                         && _vet.Phase == VeteranPhase.Zoom))
            {
                roof = Mathf.Max(floor + 1f, EvadeRoof());
            }

            float margin = Mathf.Min(BandPushMargin, (roof - floor) * BandPushFraction);
            float floorMargin = Contouring
                ? Mathf.Max(margin, _config.safeAltitudeMargin - _config.minAltitudeMargin)
                : margin;

            if (RunningDown) { floorMargin = 0f; margin = 0f; }

            return FlightSteering.Contain(heading, _rb.position,
                _minX, _maxX, _edgeMargin,
                floor, floorMargin, roof, margin);
        }

        float ComputeHeading()
        {
            if (Pilot == PilotLevel.Veteran && _state == AiState.Attack) return VeteranHeading();

            switch (_state)
            {
                case AiState.Recover:
                {
                    float climb = RecoverClimbAngleDeg * Mathf.Deg2Rad;
                    return _recoverSide >= 0f ? climb : Mathf.PI - climb;
                }

                case AiState.StrikeAim:
                    return _target != null ? HeadingTo(StrikePoint()) : _heading;

                case AiState.StrikeRun:
                    return _strikeHeading;

                case AiState.Evade:
                    return _evade.Heading;

                case AiState.Tactic:
                    return _tacticHeading;

                case AiState.Fly:
                {
                    float floor = BandFloor();
                    float roof = Mathf.Max(floor, BandCeiling());
                    float baseY = _target != null ? SeenPos.y : transform.position.y;
                    float perch = _baiting ? baseY : baseY + PerchHeight();
                    if (_cutOff)
                        perch = Mathf.Max(perch,
                            AltitudeBands.Floor(AltitudeBand.High, _groundY, _ceilingY));
                    float targetY = Mathf.Clamp(perch, floor, roof);
                    float weaveX = FlyBaseX()
                                 + Mathf.Sin(_flyWeaveT * Mathf.PI * 2f * _config.weaveHz)
                                   * _config.weaveAmplitude;
                    return HeadingTo(new Vector2(weaveX, targetY));
                }

                case AiState.Return:
                    return _target != null
                        ? HeadingTo(ClampToBand(SeenPos))
                        : _heading;

                case AiState.Tail:
                    return TailHeading();

                case AiState.Attack:
                default:
                    if (!_sectorReached && _target != null)
                        return HeadingTo(ClampToBand(SectorPoint()));
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
            if (!_appeared || !Deciding) return false;
            if (Scouting || Ace || _reversalCooldown > 0f || _standDown) return false;
            if (Pilot == PilotLevel.Veteran || _state == AiState.Tactic) return false;
            if (_urgency > UrgencyReversalCancel) return false;
            if (_state == AiState.Recover || _state == AiState.Return) return false;
            if (_state == AiState.Tail) return false;
            if (Striking) return false;

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
            float before = _heading;
            _loop.Step(dt);
            _heading = _loop.Heading;

            _angularVelocity = dt > 0f ? (_heading - before) / dt : 0f;
            _roll.Tick(dt, _heading, false, _config.rotationSpeed);
            StepLean(_config.rotationSpeed * Mathf.Deg2Rad, dt);
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
            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad * TurnBoost();
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
            float boost = Mathf.Max(TurnBoost(), _boost);
            float maxRate = _config.rotationSpeed * Mathf.Deg2Rad * boost;

            targetHeading = CapSlowClimb(targetHeading);
            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, targetHeading * Mathf.Rad2Deg)
                        * Mathf.Deg2Rad;
            if (_turnDir != 0f && Mathf.Sign(error) != _turnDir)
                error += _turnDir * Mathf.PI * 2f;

            float rawRate = dt > 0f ? error / dt : 0f;
            rawRate += PlaneEnergy.NoseDownRate(_heading, _energy.Speed, _config.flySpeed,
                _config.energyMinFraction);
            float limit = TurnLimitAt(_heading, Mathf.Sign(rawRate), maxRate);
            float desiredRate = Mathf.Clamp(rawRate, -limit, limit);

            _angularVelocity = FlightSmoothing.StepTurnRate(_angularVelocity, desiredRate,
                _config.turnResponsiveness / _rb.mass, maxRate, dt);
            _heading += _angularVelocity * dt;
            _roll.Tick(dt, _heading,
                PlaneRoll.Steady(_angularVelocity, maxRate) && !_barrel.Spinning,
                _config.rotationSpeed);
            StepLean(maxRate, dt);
            ApplyRotation();
        }

        float CapSlowClimb(float target)
        {
            if (_state == AiState.Recover) return target;

            float strength = PlaneEnergy.Strength(_heading, _energy.Speed, _config.flySpeed,
                _config.energyMinFraction);
            if (strength <= 0f) return target;

            bool right = Mathf.Cos(_heading) >= 0f;
            if ((Mathf.Cos(target) >= 0f) != right) return target;

            float cap = Mathf.Lerp(90f, PlaneEnergy.StallPitchDeg, strength) * Mathf.Deg2Rad;
            if (Mathf.Sin(target) <= Mathf.Sin(cap)) return target;
            return right ? cap : Mathf.PI - cap;
        }

        void StepLean(float maxRate, float dt)
        {
            bool still = _barrel.Spinning || _dodge.Active;
            _lean = FlightSmoothing.StepLean(_lean, still ? 0f : _angularVelocity,
                Mathf.Max(0.0001f, maxRate), _roll.Angle, dt);
        }

        float TurnBoost() => TurnBoostAt(FlightSpeed());

        float TurnBoostAt(float speed)
        {
            float cap = Mathf.Max(1f, _config.catchUpTurnMultiplier);
            float span = TopSpeed - _config.flySpeed;
            if (cap <= 1f || span <= 0f) return 1f;

            float over = (speed - _config.flySpeed) / span;
            return Mathf.Lerp(1f, cap, Mathf.Clamp01(over));
        }

        float TurnLimitAt(float heading, float dir, float maxRate)
        {
            if (!Scouting || Pilot == PilotLevel.Veteran) return maxRate;
            if (Mathf.Sin(heading) < 0f && Mathf.Cos(heading) * dir >= 0f) return maxRate;

            float rightward = Mathf.Clamp01(-Mathf.Sin(heading) * dir);
            return maxRate * Mathf.Lerp(1f, Mathf.Clamp01(_config.turnBias), rightward);
        }

        void ApplyVelocity(float dt)
        {
            float speed = UpdateSpeed(dt);

            Vector3 vel = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * speed;
            Vector3 pos = _rb.position;

            if (pos.y >= _ceilingY && vel.y > 0f) vel.y = 0f;
            if (_dodge.Active && dt > 0f) vel.z = (_dodge.Z - pos.z) / dt;

            _rb.linearVelocity = vel;

            if (pos.y > _ceilingY)
            {
                pos.y = _ceilingY;
                _rb.position = pos;
            }
        }

        float UpdateSpeed(float dt)
        {
            float cruise = _config.flySpeed;
            bool pinned = _rb.position.y >= _ceilingY && Mathf.Sin(_heading) > 0f;
            _energy.Step(_heading, cruise, pinned ? 0f : _config.diveAcceleration, _config.speedDrag,
                cruise * _config.energyMinFraction, TopSpeed, dt);

            if (Ace)
            {
                float goal = cruise * (_onCamera ? 1f : Mathf.Max(1f, _config.aceCatchUp))
                             * (_energy.Speed / Mathf.Max(0.0001f, cruise));
                _aceSpeed = Mathf.Lerp(_aceSpeed, goal, 1f - Mathf.Exp(-AceSpeedResponse * dt));
                return FlightSpeed();
            }

            _speed = _energy.Speed;

            _engageSpeed = Mathf.Lerp(_engageSpeed, StationTarget(),
                1f - Mathf.Exp(-_config.engageResponse * dt));

            _boost = Mathf.Lerp(_boost, Striking ? Mathf.Max(1f, RBoost.boostMultiplier) : 1f,
                1f - Mathf.Exp(-StrikeBoostResponse * dt));

            return FlightSpeed();
        }

        float FlightSpeed()
        {
            return Mathf.Max(1f, Ace ? _aceSpeed : CruiseSpeed());
        }

        float CruiseSpeed()
        {
            float speed = Mathf.Max(_speed, _engageSpeed) * _boost;
            if (_state == AiState.Return)
                speed = Mathf.Max(speed, _config.flySpeed * ReturnSpeedFactor);
            if (_state == AiState.Tail) speed = TailSpeed(speed);
            if (_state == AiState.Recover)
                speed = Mathf.Max(speed,
                    _config.flySpeed * Mathf.Max(1f, _config.recoverBoost));
            if (Scouting) speed = Mathf.Min(speed, TopSpeed);
            if (_pace != null)
                speed = Mathf.Min(speed,
                    Mathf.Max(_config.flySpeed, ((Vector2)_pace.linearVelocity).magnitude));
            return speed;
        }

        float TopSpeed => _config.flySpeed * Mathf.Max(1f, _config.maxSpeedMultiplier);

        float StationTarget()
        {
            if (_standDown) return 0f;

            float behind = Behind();
            return behind > 0f ? Mathf.Lerp(_config.flySpeed, TopSpeed, behind) : 0f;
        }

        void ApplyRotation()
        {
            float roll = _roll.Angle + _lean + _dodge.Bank + (_fall != null ? _fall.Roll : 0f) + Spin
                       + _barrel.Spin;
            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg)
                               * Quaternion.Euler(roll, 0f, 0f);
        }

        float HeadingTo(Vector2 point)
        {
            return Mathf.Atan2(point.y - transform.position.y, point.x - transform.position.x);
        }

        void MeasureBand()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
            {
                _bodyLow = -_bodyRadius;
                _bodyHigh = _bodyRadius;
                _bodyBack = _bodyRadius;
                _bodyFront = _bodyRadius;
                return;
            }

            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 p = transform.position;
            _bodyLow = p.y - b.max.y;
            _bodyHigh = p.y - b.min.y;
            _bodyBack = p.x - b.min.x;
            _bodyFront = b.max.x - p.x;
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
            if (_dodge.Active
                || (Pilot == PilotLevel.Veteran && _state == AiState.Attack && !_vet.Armed))
            {
                _burstLeft = 0;
                return;
            }

            bool ready = FireReady();
            if (Skill.bursts)
            {
                FireBursts(ready);
                return;
            }

            if (!ready || _fireCooldown > 0f) return;
            _fireCooldown = Mathf.Max(0.01f, _config.fireRate);

            Shoot(_config.bulletSpeed, _config.damage, GunLay());
        }

        float GunLay()
        {
            if (Skill.layDeg <= 0f || _target == null) return 0f;

            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg,
                HeadingTo(PredictIntercept()) * Mathf.Rad2Deg);
            return Mathf.Clamp(error, -Skill.layDeg, Skill.layDeg);
        }

        bool FireReady()
        {
            if (_state == AiState.StrikeRun) return _onCamera && !Reversing;

            if (_target == null || !IsOnCamera(_target.position)) return false;
            if (TargetDistance() > FireRange) return false;

            return HasFiringSolution(PredictIntercept()) || HasFiringSolution(AimPos);
        }

        float FireRange => _config.maxFireRange * Skill.fireRangeScale;

        void FireBursts(bool ready)
        {
            if (_burstLeft <= 0)
            {
                if (_burstGap > 0f || !ready) return;

                _burstLeft = UnityEngine.Random.Range(Skill.burstMin, Skill.burstMax + 1);
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                _burstError = UnityEngine.Random.value < Skill.trueChance
                    ? UnityEngine.Random.Range(-BurstTrueDeg, BurstTrueDeg)
                    : side * UnityEngine.Random.Range(Skill.missMinDeg, Skill.missMaxDeg);
            }
            else if (!BurstHolds())
            {
                EndFreeBurst();
                return;
            }

            if (_fireCooldown > 0f) return;
            _fireCooldown = Mathf.Max(0.01f, _config.fireRate);

            Shoot(_config.bulletSpeed, _config.damage,
                _burstError + UnityEngine.Random.Range(-BurstSpreadDeg, BurstSpreadDeg));
            if (--_burstLeft <= 0) EndFreeBurst();
        }

        bool BurstHolds()
        {
            if (_state == AiState.StrikeRun) return _onCamera && !Reversing;
            if (_target == null || !IsOnCamera(_target.position)) return false;
            return AimError(AimPos) <= _config.fireAngleThreshold * BurstBreakFactor;
        }

        void EndFreeBurst()
        {
            _burstLeft = 0;
            _burstGap = UnityEngine.Random.Range(Skill.burstGapMin, Skill.burstGapMax);
        }

        void Shoot(float bulletSpeed, float damage, float offsetDeg = 0f)
        {
            float aim = _heading + offsetDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(aim), Mathf.Sin(aim), 0f);
            Vector3 muzzle = _gunMounted
                ? transform.TransformPoint(_gunLocal)
                : transform.position + dir * (_bodyRadius + 6f);
            muzzle.z = transform.position.z;
            var go = Instantiate(_bulletTemplate, muzzle,
                Quaternion.AngleAxis(offsetDeg, Vector3.forward) * transform.rotation
                * Quaternion.Euler(0f, 0f, -90f));
            go.name = "EnemyBullet";
            go.SetActive(true);
            var round = go.GetComponent<Bullet>();
            round.Launch(dir, bulletSpeed, damage, _collider, fromEnemy: true);
            if (!BossFight) round.Tail();

            MuzzleFlash.Spawn(muzzle, dir, _bodyRadius);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Scene);
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
            return Intercept(AimPos, AimVel, _config.leadFactor * Skill.leadScale);
        }

        Vector2 TrueIntercept()
        {
            if (_target == null) return transform.position;
            return Intercept(_target.position, _target.linearVelocity, _config.leadFactor);
        }

        Vector2 Intercept(Vector2 tp, Vector2 tv, float lead)
        {
            float t = 0f;
            for (int i = 0; i < 2; i++)
            {
                float d = Vector2.Distance(transform.position, tp + tv * (t * lead));
                t = _config.bulletSpeed > 0f ? d / _config.bulletSpeed : 0f;
            }
            return tp + tv * (t * lead);
        }

        bool IsOnCamera(Vector3 worldPos)
        {
            if (_cam == null) return true;
            Vector3 vp = _cam.WorldToViewportPoint(worldPos);
            return vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        public void MarkStrike(bool byPlayer)
        {
            if (_dead || _falling || OffPlane || Evading || (_scripted && !Exposed)) return;
            _byPlayer = byPlayer;
        }

        public void TakeDamage(float amount)
        {
            if (_dead || _falling || OffPlane || Evading || (_scripted && !Exposed)) return;
            ApplyDamage(amount);
            if (_falling || _dodge.Active || _scripted || Ace) return;
            if (Pilot == PilotLevel.Veteran) return;

            if (_hitPending <= 0f) _hitPending = Mathf.Max(0.01f, Skill.reactionSec);
        }

        void TickHit(float dt)
        {
            if (_hitPending <= 0f) return;

            _hitPending -= dt;
            if (_hitPending > 0f) return;

            _hitPending = 0f;
            if (_standDown || _dodge.Active || _state == AiState.Recover) return;

            LoseBait();

            if (Pilot == PilotLevel.Rookie)
            {
                if (_rookie.CanPanic && _state != AiState.Return) BeginPanic();
                return;
            }

            if (_evadeCooldown <= 0f
                && (_state == AiState.Attack || _state == AiState.Fly)) EnterEvade(circling: false);
        }

        bool CanDodge()
        {
            if (!_appeared) return false;
            if (!Scouting || RunningDown || _standDown || _dodge.Active
                || _dodgeCooldown > 0f) return false;
            if (CurrentHealth > _config.health * _config.dodgeHealthFraction) return false;

            if (!UnderAim())
            {
                _dodgeRoll = 0;
                return false;
            }

            if (_dodgeRoll == 0)
                _dodgeRoll = Skill.dodgeScale >= 1f || UnityEngine.Random.value < Skill.dodgeScale
                    ? 1
                    : -1;
            return _dodgeRoll > 0;
        }

        bool UnderAim() => UnderAim(_config.dodgeAimRange, _config.dodgeAimCone);

        bool UnderAim(float reach, float cone)
        {
            if (_shooter == null || _target == null || !_shooter.Firing) return false;

            Vector2 to = (Vector2)_rb.position - AimPos;
            float range = to.magnitude;
            if (range < 1f || range > reach) return false;

            Vector2 nose = AimVel.sqrMagnitude > 1f ? AimVel : (Vector2)_target.transform.right;
            return Vector2.Angle(nose, to) <= cone;
        }

        void BeginDodge()
        {
            _dodgeRoll = 0;
            _rb.constraints &= ~RigidbodyConstraints.FreezePositionZ;
            _dodge.Begin(_baseZ, _config.dodgeDepth, _config.dodgeBank, _config.dodgeRoll,
                _config.dodgeOut, _config.dodgeHold, _config.dodgeBack);
        }

        void ApplyDamage(float amount)
        {
            float floor = Mathf.Clamp(HealthFloor, 0f, CurrentHealth);
            CurrentHealth = Mathf.Max(floor, CurrentHealth - amount);
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
            CancelAbility();
            EnemyWing.Release(this);

            if (_scripted)
            {
                _scripted = false;
                _rb.isKinematic = false;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (_sputter != null) _sputter.Settle();
            if (_smoke != null) _smoke.Ignite(ModelSize);
            if (_fire == null) _fire = PlaneFire.Ignite(gameObject, ModelSize, _engineLocal);

            _fall = PlaneFall.Begin(_rb, _heading, _config.flySpeed);
            if (_bar != null) Destroy(_bar.gameObject);

            ReportDestroyed();
        }

        void RemoveWreck()
        {
            Land();
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

        public void Explode() => Explode(false);

        void Explode(bool grounded)
        {
            if (_dead) return;
            Land();
            _dead = true;

            Explosion.Spawn(transform.position, ModelSize);
            PlaneWreck.Spawn(gameObject, transform.position, _rb != null ? _rb.linearVelocity : Vector3.zero,
                grounded);
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

            Explode(collision.gameObject.layer == ProceduralTerrain.GroundLayer);
        }

        public bool Scrape()
        {
            if (_dead || _falling || OffPlane || _scripted) return false;
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
            EnemyWing.Leave(this);
            if (_bar != null) Destroy(_bar.gameObject);
        }
    }
}
