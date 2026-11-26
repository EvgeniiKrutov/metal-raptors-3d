using UnityEngine;

namespace MetalRaptors
{
    public enum DuelRole { Escort, Peel, Level, Form, Support, Idle }

    public class DuelPlane : MonoBehaviour
    {
        const string ShotClip = "Sounds/bullet_shot_1";

        public const float BaseCruiseSpeed = 175f;
        public const float CruiseSpeed = BaseCruiseSpeed * FlightPace.Fast;

        const float PaceRate = 60f * FlightPace.Fast;
        const float PilotGain = 5f;

        const float BankPerDepthSpeed = 0.40f;
        const float BankPerTurnRate = 0.22f;
        const float MaxBank = 80f;
        const float BankResponse = 4.5f;

        const float StationResponse = 1.8f;
        const float StationCloseSpeed = 220f * FlightPace.Fast;
        const float TrackFilter = 8f;
        const float WobbleHz = 0.19f;
        const float WobbleRise = 4f;
        const float WobbleDrift = 5f;

        const float FreeGain = 0.6f;
        const float FreeLag = 0.4f;
        const float FreeTrackLag = 0.08f;
        const float FreeSeparation = 85f;
        const float FreeRepel = 3f;
        const float LiftShare = 0.5f;
        const float FreeTurnMax = 1.4f;
        const float FreeMinPace = 0.6f;
        const float FreeMaxPace = 1.25f;
        const float FreeAccel = 45f * FlightPace.Fast;
        const float FreeCrossMax = 0.5f;
        const float MateSeparation = 95f;
        const float SeparationLift = 1f;
        const float JoinWobbleSec = 1.5f;
        const float FreeClimbMax = 0.45f;
        const float FreeBankDeg = 40f;
        const float FreeBankRadius = 380f;
        const float FreeBankResponse = 2.5f;
        const float FreeBankRate = 45f;
        const float FreeRateLag = 0.25f;
        const float BankKnee = 0.15f;
        const float FreeBlendSec = 0.5f;
        const float FreeJump = 1500f;
        const float FollowVelLag = 0.1f;

        const float PeelClimbDeg = 24f;

        const float LevelToleranceDeg = 10f;
        const float FormLookahead = 130f;
        const float FormGain = 0.55f;
        const float FormPush = 95f;
        const float FormTrim = 70f;
        const float StallFraction = 0.7f;
        const float FormReach = 70f;

        const float RestMin = 0.9f;
        const float RestMax = 1.9f;
        const float MuzzleClearance = 6f;
        const float TracerSpeed = 430f * FlightPace.Fast;
        const float ShotVolume = 0.045f;
        const float LoudShotVolume = 0.36f;

        const float SupportLeash = 900f;
        const float SupportFireRange = 420f;
        const float SupportFireAngleDeg = 8f;
        const float SupportDamageShare = 0.5f;
        const int SupportBurstMin = 3;
        const int SupportBurstMax = 5;
        const float SupportRestMin = 1.6f;
        const float SupportRestMax = 2.8f;
        const float SupportShotVolume = 0.18f;

        const float SideMargin = 200f;
        const float FloorMargin = 100f;
        const float CeilingMargin = 100f;
        const float ClampSpeed = 120f * FlightPace.Fast;

        const float FallDiveDeg = PlaneFall.DiveDeg;
        const float FallSpinDeg = PlaneFall.RollRateDeg;
        const float FallDiveResponse = PlaneFall.DiveResponse;
        const float FallSpeedGain = PlaneFall.SpeedGain;
        const float FallExitMargin = 60f;
        const float FallDepthDamp = 2f;
        const float UnscriptBlendSec = 0.6f;
        const float UnscriptMinShare = 0.3f;

        static readonly Quaternion Flip = Quaternion.Euler(180f, 0f, 0f);

        DuelRole _role = DuelRole.Idle;
        Transform _escort;
        Vector3 _stationOffset;
        Vector3 _station;
        bool _hasStation;
        float _facing = 1f;

        Transform _leader;
        Vector3 _slot;
        Vector3 _slotFrom;
        Vector3 _slotTo;
        float _slotAge;
        float _slotSec = 0.01f;
        bool _slotLift;
        bool _manualFollow;
        Quaternion _slaveFromRot;
        Vector3 _slaveFlat = Vector3.right;
        float _slaveAge;
        float _slaveBlendSec;
        Vector3 _followVel;
        Vector3 _lastPos;
        bool _tracked;
        System.Collections.Generic.List<DuelPlane> _mates;
        int _freeFrames;
        bool _free;
        bool _freeLive;
        float _freeAge;
        float _freeHeading;
        float _freeSpeed;
        float _freePace;
        float _freeClimb;
        float _freeBank;
        Vector3 _freeTargetPrev;
        Vector3 _freeTargetVel;
        float _freeTrackHeading;
        float _freeTrackRate;
        float _freeYawRate;
        Vector3 _freeLeaderPrev;
        Quaternion _freeFromRot;

        bool _mirrored;
        bool _scripted;
        bool _scriptFall;
        Vector3 _fallVel;
        float _fallSpin;
        Quaternion _blendFrom = Quaternion.identity;
        float _blendAge = UnscriptBlendSec;

        float _leadSpeed = 120f;
        float _turnRate = Mathf.PI;
        float _turnResponse = 2f;
        float _diveAcceleration = 90f;
        float _speedDrag = 0.9f;
        float _maxSpeedFactor = 1.6f;

        float _size = 60f;
        float _heading;
        float _speed = CruiseSpeed;
        float _pace = CruiseSpeed;
        float _paceTarget = CruiseSpeed;
        float _angularVelocity;
        float _bank;
        PlaneRoll _roll;
        float _wobbleTime;
        float _wobblePhase;

        float _depthFrom, _depthTo, _depthTime, _depthDuration, _z;

        float _minX, _maxX, _floorY, _ceilingY;
        float _escortFloorY, _escortCeilingY;
        bool _bounded;

        float _viewMinX, _viewMaxX, _viewMinY, _viewMaxY;
        bool _hasView;
        float _groundY;
        bool _hasGround;

        float _shotTimer, _restTimer, _shotSpacing;
        int _burstLeft;

        Rigidbody _mark;
        GameObject _round;
        float _gunDamage;
        float _gunSpeed;

        bool _falling;
        bool _vanishing;
        float _fallSpeed;
        float _fallHeadingDeg;
        bool _posed;

        SmokeTrail _smoke;
        PlaneFire _fire;
        Vector3? _gun;
        Vector3? _engine;
        AudioSource _audio;
        AudioClip _shotClip;

        public bool IsFalling => _falling;

        public bool Slaved => _leader != null;

        Vector3 StationOffset => new Vector3(_stationOffset.x * _facing, _stationOffset.y, _stationOffset.z);

        public bool Levelled =>
            Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, 0f)) <= LevelToleranceDeg;

        public bool Rolling => _roll != null && _roll.Rolling;

        public bool AtStation
        {
            get
            {
                if (_escort == null) return true;

                Vector2 gap = (Vector2)(_escort.position + StationOffset)
                            - (Vector2)transform.position;
                return gap.sqrMagnitude <= FormReach * FormReach;
            }
        }

        public bool Scripted => _scripted;

        public Vector3 Motion => _followVel;

        public Quaternion Visual => _mirrored ? transform.rotation * Flip : transform.rotation;

        public Vector3 VisualUp => Visual * Vector3.up;

        public static DuelPlane Spawn(string name, PlaneModelConfig plane, PlayerConfig flight,
            Vector3 position, float heading, bool mirrored, PlaneSkin skin)
        {
            if (plane == null)
            {
                Debug.LogError("DuelPlane: no plane model configured.");
                return null;
            }

            if (flight == null) flight = ScriptableObject.CreateInstance<PlayerConfig>();

            var go = new GameObject(name);
            go.transform.position = position;
            Transform model = PlaneFactory.BuildPlaneModel(go.transform, plane, mirrored, skin);
            model.localScale *= PlaneModelConfig.LevelScale;
            WreckSource.Attach(go, model, plane, skin);

            foreach (Collider col in go.GetComponentsInChildren<Collider>()) Destroy(col);

            var duel = go.AddComponent<DuelPlane>();
            duel._size = plane.OnScreenSize * PlaneModelConfig.LevelScale;
            duel._leadSpeed = Mathf.Max(1f, flight.flySpeed);
            duel._turnRate = flight.rotationSpeed * Mathf.Deg2Rad;
            duel._turnResponse = flight.turnResponsiveness / Mathf.Max(0.0001f, flight.mass);
            duel._diveAcceleration = flight.diveAcceleration;
            duel._speedDrag = flight.speedDrag;
            duel._maxSpeedFactor = Mathf.Max(1f, flight.maxSpeedMultiplier);
            duel._heading = heading;
            duel._mirrored = mirrored;
            duel._roll = new PlaneRoll(mirrored);
            duel._z = position.z;
            duel._depthTo = position.z;
            duel._wobblePhase = Random.Range(0f, 10f);
            duel._shotSpacing = Mathf.Max(0.01f, flight.fireRate);
            duel._restTimer = Random.Range(RestMin, RestMax);
            duel._smoke = go.AddComponent<SmokeTrail>();
            if (PlaneFactory.GunMuzzleLocal(go, model, plane, out Vector3 gun)) duel._gun = gun;
            else if (plane.gunLift > 0f)
                duel._gun = PlaneFactory.GunLocal(go, model, plane, PlaneModelConfig.LevelScale, mirrored);
            duel._engine = PlaneFactory.EngineLocal(go, model, plane);
            if (duel._engine.HasValue) duel._smoke.Mount = duel._engine.Value;

            duel._shotClip = Resources.Load<AudioClip>(ShotClip);
            duel._audio = go.AddComponent<AudioSource>();
            duel._audio.playOnAwake = false;
            duel._audio.spatialBlend = 0f;

            duel.ApplyRotation();
            return duel;
        }

        public void ArmGuns(PlayerConfig flight)
        {
            if (_round != null || flight == null) return;

            _gunDamage = Mathf.Max(0f, flight.damage) * SupportDamageShare;
            _gunSpeed = Mathf.Max(1f, flight.bulletSpeed);
            _round = Bullet.BuildTemplate(Bullet.RoundColor);
        }

        public void SetMark(Rigidbody mark)
        {
            _mark = mark;
        }

        public void SetFacing(float facing) => _facing = facing < 0f ? -1f : 1f;

        public void Slave(Transform leader, Vector3 slot, float blendSec)
        {
            if (_falling || leader == null) return;

            EndScript();
            _leader = leader;
            _slaveAge = 0f;
            _slaveBlendSec = Mathf.Max(0.01f, blendSec);
            _slaveFromRot = transform.rotation;
            _slot = OffsetFrom(leader);
            Reslot(slot, blendSec);
            _role = DuelRole.Idle;
            _escort = null;
            _mark = null;
            _burstLeft = 0;
            _depthDuration = 0f;
        }

        public void Reslot(Vector3 slot, float seconds, bool lift = false)
        {
            _slotFrom = _slot;
            _slotTo = slot;
            _slotAge = 0f;
            _slotSec = Mathf.Max(0.01f, seconds);
            _slotLift = lift;
        }

        public void SetManualFollow(bool on) => _manualFollow = on;

        public float SlotGap { get; private set; }

        public void Join(Transform leader, Vector3 slot, System.Collections.Generic.List<DuelPlane> mates)
        {
            if (_falling || leader == null) return;

            EndScript();
            _leader = leader;
            _slot = slot;
            _slotFrom = slot;
            _slotTo = slot;
            _slotAge = 0f;
            _slotSec = 0.01f;
            _slotLift = false;
            _slaveAge = 0f;
            _slaveBlendSec = JoinWobbleSec;
            _slaveFromRot = transform.rotation;
            _mates = mates;
            _free = true;
            _freeLive = false;
            _role = DuelRole.Idle;
            _escort = null;
            _mark = null;
            _burstLeft = 0;
            _depthDuration = 0f;
            SlotGap = float.MaxValue;
        }

        public void SetFreeFollow(bool on, float settleSec)
        {
            if (on == _free) return;

            _free = on;
            _freeLive = false;
            if (!on && _leader != null && !_falling) Reslave(settleSec);
        }

        void Reslave(float seconds)
        {
            Vector3 to = _slotTo;
            _slaveAge = 0f;
            _slaveBlendSec = Mathf.Max(0.01f, seconds);
            _slaveFromRot = transform.rotation;
            _slot = OffsetFrom(_leader);
            Reslot(to, seconds);
        }

        public void FollowNow(float dt)
        {
            if (_leader == null || _falling || dt <= 0f) return;
            FollowLeader(dt);
        }

        Vector3 OffsetFrom(Transform leader)
        {
            Vector3 forward = leader.rotation * Vector3.right;
            var flat = new Vector3(forward.x, 0f, forward.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.right;
            var side = new Vector3(-flat.z, 0f, flat.x);
            Vector3 gap = transform.position - leader.position;
            return new Vector3(Vector3.Dot(gap, flat), gap.y, Vector3.Dot(gap, side));
        }

        public void SetEscort(Transform target, Vector3 offset)
        {
            if (_leader != null) return;
            EndScript();
            _escort = target;
            _stationOffset = offset;
            _hasStation = false;
        }

        public void SetRole(DuelRole role)
        {
            if (_falling || _leader != null) return;

            EndScript();
            _role = role;
            _burstLeft = 0;
            _restTimer = Mathf.Max(_restTimer, Random.Range(RestMin, RestMax) * 0.5f);
            if (role == DuelRole.Escort) _hasStation = false;
        }

        public void BeginScript()
        {
            if (_falling) return;

            _scripted = true;
            _posed = false;
            _leader = null;
            _free = false;
            _freeLive = false;
            _manualFollow = false;
            _role = DuelRole.Idle;
            _escort = null;
            _mark = null;
            _burstLeft = 0;
            _bounded = false;
            _depthDuration = 0f;
        }

        public void ScriptPose(Vector3 position, Quaternion visual)
        {
            if (!_scripted) return;

            transform.SetPositionAndRotation(position, _mirrored ? visual * Flip : visual);
            _z = position.z;
            _depthTo = position.z;
        }

        public void EndScript()
        {
            if (!_scripted) return;

            _scripted = false;
            Vector3 nose = transform.right;
            var flat = new Vector2(_followVel.x, _followVel.y);
            if (Mathf.Abs(flat.x) < CruiseSpeed * UnscriptMinShare)
                flat.x = (nose.x >= 0f ? 1f : -1f) * CruiseSpeed;
            _heading = Mathf.Atan2(flat.y, flat.x);
            _speed = Mathf.Clamp(flat.magnitude, CruiseSpeed * FreeMinPace, CruiseSpeed * FreeMaxPace);
            _angularVelocity = 0f;
            _bank = 0f;
            _z = transform.position.z;
            _depthTo = _z;
            _depthDuration = 0f;
            if (_roll != null) _roll.Settle(_heading);
            _blendFrom = transform.rotation;
            _blendAge = 0f;
        }

        public void FlipUpright()
        {
            if (_falling || _roll == null || _scripted) return;
            _roll.Flip(_heading, _turnRate * Mathf.Rad2Deg);
        }

        public void SetDepth(float z, float seconds)
        {
            if (_leader != null || _scripted) return;
            _depthFrom = _z;
            _depthTo = z;
            _depthTime = 0f;
            _depthDuration = Mathf.Max(0f, seconds);
            if (_depthDuration <= 0f) _z = z;
        }

        public void HoldDepth()
        {
            SetDepth(_z, 0f);
        }

        public void SetBounds(float minX, float maxX, float floorY, float ceilingY)
        {
            _minX = minX;
            _maxX = maxX;
            _floorY = floorY;
            _ceilingY = ceilingY;
            _bounded = true;
        }

        public void SetEscortBounds(float floorY, float ceilingY)
        {
            _escortFloorY = floorY;
            _escortCeilingY = ceilingY;
        }

        public void SetView(float minX, float maxX, float minY, float maxY)
        {
            _viewMinX = minX;
            _viewMaxX = maxX;
            _viewMinY = minY;
            _viewMaxY = maxY;
            _hasView = true;
        }

        public void SetGround(float y)
        {
            _groundY = y;
            _hasGround = true;
        }

        public void SetPace(float cruise)
        {
            _paceTarget = Mathf.Max(1f, cruise);
        }

        public void StandDown()
        {
            if (_falling) return;

            EndScript();
            _role = DuelRole.Idle;
            _escort = null;
            _burstLeft = 0;
            _bounded = false;
        }

        public void BeginPosed()
        {
            if (_falling) return;

            _posed = true;
            _scripted = false;
            _leader = null;
            _free = false;
            _freeLive = false;
            _manualFollow = false;
            _role = DuelRole.Idle;
            _escort = null;
            _mark = null;
            _burstLeft = 0;
            _bounded = false;
            _depthDuration = 0f;
        }

        public void Pose(Vector3 position, Quaternion rotation)
        {
            if (!_posed) return;

            transform.SetPositionAndRotation(position, rotation);
            _z = position.z;
            _depthTo = position.z;
        }

        public void ShootAlong(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-6f) return;

            dir.Normalize();
            Vector3 muzzle = Muzzle(dir);
            Tracer.Spawn(muzzle, dir, TracerSpeed);
            MuzzleFlash.Spawn(muzzle, dir, _size);
            if (_shotClip != null && _audio != null) _audio.PlayOneShot(_shotClip, LoudShotVolume * AudioOptions.Scene);
        }

        public void Fire(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-6f) return;

            dir.Normalize();
            Vector3 muzzle = Muzzle(dir);
            Vector3 flight = _followVel + dir * TracerSpeed;
            Tracer.Spawn(muzzle, flight.normalized, flight.magnitude);
            MuzzleFlash.Spawn(muzzle, dir, _size);
            if (_shotClip != null && _audio != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Scene);
        }

        public static float RoundSpeed => TracerSpeed;

        public float ShotSpacing => _shotSpacing;

        public void Burn()
        {
            Explosion.Spawn(transform.position, _size);
            if (_smoke != null) _smoke.Ignite(_size);
            if (_fire == null) _fire = PlaneFire.Ignite(gameObject, _size, _engine);
        }

        public bool CrashAt(Vector3 point) => Crash(point, false);

        static readonly System.Collections.Generic.List<DuelPlane> Fallen =
            new System.Collections.Generic.List<DuelPlane>();

        static readonly System.Collections.Generic.List<DuelPlane> Present =
            new System.Collections.Generic.List<DuelPlane>();

        public static System.Collections.Generic.IReadOnlyList<DuelPlane> All => Present;

        public float Size => _size;

        void OnEnable() => Present.Add(this);

        void OnDisable() => Present.Remove(this);

        public static void VanishFalling()
        {
            foreach (DuelPlane plane in Fallen.ToArray())
                if (plane != null) plane.Vanish();
        }

        void Vanish()
        {
            if (!_falling || _vanishing) return;

            _vanishing = true;
            if (_smoke != null) _smoke.Settle();
            CutsceneVanish.Begin(gameObject, Remove);
        }

        public void Kill()
        {
            if (_falling) return;

            bool scripted = _scripted;
            _scripted = false;
            _leader = null;
            _free = false;
            _falling = true;
            Fallen.Add(this);
            _role = DuelRole.Idle;
            _escort = null;
            _burstLeft = 0;
            _fallSpeed = Mathf.Max(scripted ? _followVel.magnitude : _speed, CruiseSpeed);
            _fallHeadingDeg = Mathf.Cos(_heading) >= 0f ? FallDiveDeg : 180f - FallDiveDeg;
            if (scripted) BeginScriptFall();

            Explosion.Spawn(transform.position, _size);
            if (_smoke != null) _smoke.Ignite(_size);
            _fire = PlaneFire.Ignite(gameObject, _size, _engine);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 here = transform.position;
            if (_tracked)
                _followVel = Vector3.Lerp(_followVel, (here - _lastPos) / dt, 1f - Mathf.Exp(-dt / FollowVelLag));
            _lastPos = here;
            _tracked = true;

            if (_posed || _scripted) return;
            if (_scriptFall) { ScriptFall(dt); return; }

            _blendAge += dt;

            if (_leader != null && !_falling)
            {
                if (!_manualFollow) FollowLeader(dt);
                return;
            }

            Vector3 pos = transform.position;
            float previousZ = _z;
            AdvanceDepth(dt);

            if (_falling) pos = Fall(pos, dt);
            else if (_role == DuelRole.Escort && _escort != null) pos = HoldStation(pos, dt);
            else pos = Fly(pos, dt);

            pos.z = _z;
            if (_bounded && !_falling) pos.y = EaseIntoBand(pos.y, dt);
            transform.position = pos;

            UpdateBank(dt, (_z - previousZ) / dt);
            ApplyRotation();

            if (_falling) { TickWreck(pos); return; }
            if (_role == DuelRole.Support) UpdateSupportGun(dt);
        }

        void BeginScriptFall()
        {
            _scriptFall = true;
            _fallVel = _followVel;
            Vector3 fwd = _fallVel.sqrMagnitude > 1f ? _fallVel.normalized : transform.right;
            _fallSpin = Vector3.SignedAngle(LevelUp(fwd), Vector3.ProjectOnPlane(VisualUp, fwd), fwd);
        }

        void ScriptFall(float dt)
        {
            float side = Mathf.Abs(_fallVel.x) > 1f ? Mathf.Sign(_fallVel.x)
                : transform.right.x >= 0f ? 1f : -1f;
            _fallSpeed += FallSpeedGain * dt;
            float dive = FallDiveDeg * Mathf.Deg2Rad;
            var want = new Vector3(side * Mathf.Cos(dive), Mathf.Sin(dive), 0f) * _fallSpeed;
            _fallVel = Vector3.Lerp(_fallVel, want, 1f - Mathf.Exp(-FallDiveResponse * dt));
            _fallVel.z *= Mathf.Exp(-FallDepthDamp * dt);

            Vector3 pos = transform.position + _fallVel * dt;
            _fallSpin += FallSpinDeg * dt;
            Vector3 fwd = _fallVel.sqrMagnitude > 1f ? _fallVel.normalized : transform.right;
            Vector3 up = Quaternion.AngleAxis(_fallSpin, fwd) * LevelUp(fwd);
            Quaternion visual = Quaternion.LookRotation(Vector3.Cross(fwd, up), up);
            transform.SetPositionAndRotation(pos, _mirrored ? visual * Flip : visual);
            TickWreck(pos);
        }

        static Vector3 LevelUp(Vector3 fwd)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, fwd);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, fwd);
            return up.normalized;
        }

        void FollowLeader(float dt)
        {
            _slaveAge += dt;
            _wobbleTime += dt;

            Vector3 forward = _leader.rotation * Vector3.right;
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude > 1e-4f) _slaveFlat = flat.normalized;

            _slotAge += dt;
            float slotT = Mathf.Clamp01(_slotAge / _slotSec);
            float slotK = Mathf.SmoothStep(0f, 1f, slotT);
            float liftK = !_slotLift ? slotK
                : Mathf.Abs(_slotTo.y) >= Mathf.Abs(_slotFrom.y)
                    ? Mathf.SmoothStep(0f, 1f, slotT / LiftShare)
                    : Mathf.SmoothStep(0f, 1f, (slotT - (1f - LiftShare)) / LiftShare);
            _slot = new Vector3(Mathf.Lerp(_slotFrom.x, _slotTo.x, slotK),
                Mathf.Lerp(_slotFrom.y, _slotTo.y, liftK),
                Mathf.Lerp(_slotFrom.z, _slotTo.z, slotK));

            float k = Mathf.SmoothStep(0f, 1f, _slaveAge / _slaveBlendSec);
            float phase = (_wobbleTime + _wobblePhase) * Mathf.PI * 2f * WobbleHz;
            var side = new Vector3(-_slaveFlat.z, 0f, _slaveFlat.x);
            Vector3 pos = _leader.position + _slaveFlat * _slot.x + Vector3.up * _slot.y + side * _slot.z
                          + (new Vector3(0f, Mathf.Sin(phase) * WobbleRise, 0f)
                             + _slaveFlat * (Mathf.Sin(phase * 0.61f) * WobbleDrift)) * k;

            Vector3 before = transform.position;
            if (_free)
            {
                FlyFree(pos, dt);
                pos = transform.position;
            }
            else
            {
                transform.position = pos;
                transform.rotation = Quaternion.Slerp(_slaveFromRot, _leader.rotation, k);
            }

            Vector3 nose = _free ? transform.rotation * Vector3.right : forward;
            Vector2 move = (Vector2)(pos - before) / dt;
            if (move.sqrMagnitude > 1f) _speed = move.magnitude;
            if (Mathf.Abs(nose.x) > 0.2f)
            {
                _heading = Mathf.Atan2(nose.y, nose.x);
                _roll.Settle(_heading);
            }
            _bank = 0f;
            _angularVelocity = 0f;
            _z = pos.z;
            _depthTo = pos.z;
        }

        Vector3 Approach(Vector3 gap, float pace)
        {
            var flatVel = new Vector2(_freeTargetVel.x, _freeTargetVel.z);
            float trackSpeed = flatVel.magnitude;
            float climb = _freeTargetVel.y + gap.y * FreeGain;
            if (trackSpeed < 1f)
            {
                Vector3 plain = _freeTargetVel + gap * FreeGain;
                plain.y = climb;
                return plain;
            }

            Vector2 along = flatVel / trackSpeed;
            var across = new Vector2(-along.y, along.x);
            var flatGap = new Vector2(gap.x, gap.z);
            float forward = Mathf.Clamp(trackSpeed + Vector2.Dot(flatGap, along) * FreeGain,
                pace * FreeMinPace, pace * FreeMaxPace);
            float sideways = Mathf.Clamp(Vector2.Dot(flatGap, across) * FreeGain,
                -pace * FreeCrossMax, pace * FreeCrossMax);
            Vector2 flat = along * forward + across * sideways;
            return new Vector3(flat.x, climb, flat.y);
        }

        Vector3 Repel(Vector3 other, float reach, float side)
        {
            Vector3 away = transform.position - other;
            float apart = away.magnitude;
            if (apart >= reach) return Vector3.zero;

            Vector3 dir = apart > 0.01f ? away / apart : Vector3.zero;
            dir.y += side * SeparationLift;
            return dir.normalized * ((reach - apart) * FreeRepel);
        }

        float SideFrom(DuelPlane mate)
        {
            float dy = _slotTo.y - mate._slotTo.y;
            if (Mathf.Abs(dy) > 1f) return Mathf.Sign(dy);
            float dx = _slotTo.x - mate._slotTo.x;
            if (Mathf.Abs(dx) > 1f) return Mathf.Sign(dx);
            return _wobblePhase >= mate._wobblePhase ? 1f : -1f;
        }

        public static float BankShape(float turn)
        {
            float x = Mathf.Clamp(turn, -1f, 1f);
            return x * x * x / (x * x + BankKnee * BankKnee) * (1f + BankKnee * BankKnee);
        }

        void FlyFree(Vector3 target, float dt)
        {
            Vector3 leader = _leader.position;
            if (!_freeLive)
            {
                _freeLive = true;
                _freeAge = 0f;
                _freeFrames = 0;
                _freeTargetPrev = target;
                _freeTargetVel = _followVel;
                _freeTrackHeading = Mathf.Atan2(_followVel.z, _followVel.x);
                _freeTrackRate = 0f;
                _freeYawRate = 0f;
                _freeLeaderPrev = leader;
                float flatSpeed = new Vector2(_followVel.x, _followVel.z).magnitude;
                _freePace = Mathf.Max(flatSpeed, CruiseSpeed * FreeMinPace);
                _freeSpeed = Mathf.Clamp(flatSpeed, CruiseSpeed * FreeMinPace, CruiseSpeed * FreeMaxPace);
                _freeClimb = _followVel.y;
                _freeBank = 0f;
                _freeFromRot = transform.rotation;

                Vector3 nose = transform.rotation * Vector3.right;
                _freeHeading = flatSpeed > 20f ? Mathf.Atan2(_followVel.z, _followVel.x)
                    : nose.x * nose.x + nose.z * nose.z > 1e-4f ? Mathf.Atan2(nose.z, nose.x)
                    : _facing > 0f ? 0f : Mathf.PI;
            }
            _freeAge += dt;
            _freeFrames++;

            float track = 1f - Mathf.Exp(-dt / FreeTrackLag);
            Vector3 targetVel = (target - _freeTargetPrev) / dt;
            _freeTargetPrev = target;
            bool fresh = _freeFrames == 2;
            if (fresh && targetVel.sqrMagnitude < FreeJump * FreeJump)
            {
                _freeTargetVel = targetVel;
                _freeTrackHeading = Mathf.Atan2(targetVel.z, targetVel.x);
            }
            else if (_freeFrames > 2 && targetVel.sqrMagnitude < FreeJump * FreeJump)
            {
                _freeTargetVel = Vector3.Lerp(_freeTargetVel, targetVel, track);
            }

            if (_freeFrames > 2 && _freeTargetVel.x * _freeTargetVel.x + _freeTargetVel.z * _freeTargetVel.z > 1f)
            {
                float trackHeading = Mathf.Atan2(_freeTargetVel.z, _freeTargetVel.x);
                float trackRate = Mathf.DeltaAngle(_freeTrackHeading * Mathf.Rad2Deg, trackHeading * Mathf.Rad2Deg)
                                  * Mathf.Deg2Rad / dt;
                _freeTrackHeading = trackHeading;
                _freeTrackRate = Mathf.Lerp(_freeTrackRate, Mathf.Clamp(trackRate, -FreeTurnMax, FreeTurnMax),
                    1f - Mathf.Exp(-dt / FreeRateLag));
            }

            Vector3 leaderVel = (leader - _freeLeaderPrev) / dt;
            _freeLeaderPrev = leader;
            float leaderPace = new Vector2(leaderVel.x, leaderVel.z).magnitude;
            if (leaderPace < FreeJump) _freePace = Mathf.Lerp(_freePace, leaderPace, track);

            float pace = Mathf.Max(1f, _freePace);
            Vector3 gap = target - transform.position;
            SlotGap = gap.magnitude;
            Vector3 want = Approach(gap, pace);

            float leaderSide = Mathf.Abs(_slotTo.y) > 1f ? Mathf.Sign(_slotTo.y)
                : transform.position.y >= leader.y ? 1f : -1f;
            want += Repel(leader, FreeSeparation, leaderSide);
            if (_mates != null)
            {
                foreach (DuelPlane mate in _mates)
                {
                    if (mate == null || mate == this || mate._falling) continue;
                    want += Repel(mate.transform.position, MateSeparation, SideFrom(mate));
                }
            }

            var flatWant = new Vector2(want.x, want.z);
            float speedWant = Mathf.Clamp(flatWant.magnitude, pace * FreeMinPace, pace * FreeMaxPace);
            float headingWant = flatWant.sqrMagnitude > 1f ? Mathf.Atan2(flatWant.y, flatWant.x) : _freeHeading;

            float turn = Mathf.DeltaAngle(_freeHeading * Mathf.Rad2Deg, headingWant * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float rate = Mathf.Clamp(_freeTrackRate + turn / FreeLag, -FreeTurnMax, FreeTurnMax);
            _freeHeading += rate * dt;

            float ease = 1f - Mathf.Exp(-dt / FreeLag);
            _freeSpeed += Mathf.Clamp((speedWant - _freeSpeed) * ease, -FreeAccel * dt, FreeAccel * dt);
            float climbWant = Mathf.Clamp(want.y, -_freeSpeed * FreeClimbMax, _freeSpeed * FreeClimbMax);
            _freeClimb += (climbWant - _freeClimb) * ease;

            transform.position += new Vector3(Mathf.Cos(_freeHeading) * _freeSpeed, _freeClimb,
                Mathf.Sin(_freeHeading) * _freeSpeed) * dt;

            _freeYawRate = Mathf.Lerp(_freeYawRate, rate, 1f - Mathf.Exp(-dt / FreeRateLag));
            float curvature = _freeYawRate / Mathf.Max(1f, _freeSpeed);
            float bank = FreeBankDeg * BankShape(curvature * FreeBankRadius);
            float eased = Mathf.Lerp(_freeBank, bank, 1f - Mathf.Exp(-FreeBankResponse * dt));
            _freeBank = Mathf.MoveTowards(_freeBank, eased, FreeBankRate * dt);
            float pitch = Mathf.Atan2(_freeClimb, _freeSpeed) * Mathf.Rad2Deg;

            Quaternion rot = Quaternion.Euler(0f, -_freeHeading * Mathf.Rad2Deg, 0f)
                             * Quaternion.Euler(0f, 0f, pitch)
                             * Quaternion.Euler(_freeBank, 0f, 0f);
            transform.rotation = Quaternion.Slerp(_freeFromRot, rot, Mathf.SmoothStep(0f, 1f, _freeAge / FreeBlendSec));
        }

        void TickWreck(Vector3 pos)
        {
            if (Surface(pos, out float surface, out bool water) && pos.y <= surface)
            {
                Crash(new Vector3(pos.x, surface, pos.z), water);
                return;
            }

            if (OutOfView(pos)) Remove();
        }

        bool Surface(Vector3 pos, out float y, out bool water)
        {
            Battlefield field = Battlefield.Current;
            if (field != null && field.Surface(pos.x, pos.z, out y, out water)) return true;

            water = false;
            y = _groundY;
            return _hasGround;
        }

        bool OutOfView(Vector3 pos)
        {
            if (!_hasView) return false;

            float margin = _size * 0.5f + FallExitMargin;
            return pos.x < _viewMinX - margin || pos.x > _viewMaxX + margin
                || pos.y < _viewMinY - margin || pos.y > _viewMaxY + margin;
        }

        bool Crash(Vector3 point, bool water)
        {
            if (_vanishing)
            {
                Remove();
                return false;
            }

            bool wrecked = false;
            if (!water)
            {
                Explosion.Spawn(point, _size);
                wrecked = PlaneWreck.Spawn(gameObject, point, Vector3.zero, true);
            }
            if (Battlefield.Current != null) Battlefield.Current.Impact(point, _size, water);
            Remove();
            return wrecked;
        }

        bool Escorting => _role == DuelRole.Escort || _role == DuelRole.Form
                       || _role == DuelRole.Support;

        float BandFloor => Escorting ? _escortFloorY : _floorY;

        float BandCeiling => Escorting ? _escortCeilingY : _ceilingY;

        float EaseIntoBand(float y, float dt)
        {
            float low = BandFloor;
            float high = BandCeiling;
            float step = ClampSpeed * dt;

            if (y < low) return Mathf.Min(low, y + step);
            if (y > high) return Mathf.Max(high, y - step);
            return y;
        }

        void AdvanceDepth(float dt)
        {
            if (_depthDuration <= 0f) { _z = _depthTo; return; }

            _depthTime = Mathf.Min(_depthTime + dt, _depthDuration);
            _z = Mathf.Lerp(_depthFrom, _depthTo,
                Mathf.SmoothStep(0f, 1f, _depthTime / _depthDuration));
            if (_depthTime >= _depthDuration) _depthDuration = 0f;
        }

        Vector3 HoldStation(Vector3 pos, float dt)
        {
            _wobbleTime += dt;
            float phase = (_wobbleTime + _wobblePhase) * Mathf.PI * 2f * WobbleHz;

            Vector3 station = _escort.position + StationOffset
                + new Vector3(Mathf.Sin(phase * 0.61f) * WobbleDrift,
                              Mathf.Sin(phase) * WobbleRise, 0f);
            station.z = pos.z;

            Vector3 carry = _hasStation ? station - _station : Vector3.zero;
            _station = station;
            _hasStation = true;

            Vector3 correction = (station - pos) * (1f - Mathf.Exp(-StationResponse * dt));
            float maxStep = StationCloseSpeed * dt;
            if (correction.sqrMagnitude > maxStep * maxStep)
                correction = correction.normalized * maxStep;

            Vector3 next = pos + KeepAirspeed(carry, carry + correction);

            Vector2 move = next - pos;
            if (move.sqrMagnitude > 1e-4f) TrackHeading(Mathf.Atan2(move.y, move.x), dt);
            return next;
        }

        static Vector3 KeepAirspeed(Vector3 carry, Vector3 move)
        {
            Vector2 lead = carry;
            float length = lead.magnitude;
            if (length < 1e-4f) return move;

            Vector2 dir = lead / length;
            float along = Vector2.Dot(move, dir);
            float least = length * StallFraction;
            if (along >= least) return move;

            return move + (Vector3)(dir * (least - along));
        }

        Vector3 Fly(Vector3 pos, float dt)
        {
            float target = RoleHeading(pos);

            if (_bounded && _role != DuelRole.Idle)
                target = FlightSteering.Contain(target, pos, _minX, _maxX, SideMargin,
                    BandFloor + FloorMargin, FloorMargin, BandCeiling, CeilingMargin);

            SteerToHeading(target, dt);

            _pace = Mathf.MoveTowards(_pace, _paceTarget, PaceRate * dt);

            float cruise = _pace;
            float floor = cruise;
            if (_role == DuelRole.Form || _role == DuelRole.Support)
            {
                cruise = FormCruise(pos);
                floor = _leadSpeed * StallFraction;
            }
            UpdateSpeed(cruise, floor, dt);

            return pos + new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * (_speed * dt);
        }

        float FormCruise(Vector3 pos)
        {
            if (_escort == null) return CruiseSpeed;

            float behind = (_escort.position.x + StationOffset.x - pos.x) * _facing;
            return _leadSpeed + Mathf.Clamp(behind * FormGain, -FormTrim, FormPush);
        }

        void UpdateSpeed(float cruise, float floor, float dt)
        {
            _speed += -Mathf.Sin(_heading) * _diveAcceleration * dt;
            _speed -= (_speed - cruise) * _speedDrag * dt;
            _speed = Mathf.Clamp(_speed, floor, cruise * _maxSpeedFactor);
        }

        Vector3 Fall(Vector3 pos, float dt)
        {
            _heading = Mathf.LerpAngle(_heading * Mathf.Rad2Deg, _fallHeadingDeg,
                1f - Mathf.Exp(-FallDiveResponse * dt)) * Mathf.Deg2Rad;
            _fallSpeed += FallSpeedGain * dt;

            return pos + new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f)
                       * (_fallSpeed * dt);
        }

        float RoleHeading(Vector3 pos)
        {
            switch (_role)
            {
                case DuelRole.Peel:
                    return PeelClimbDeg * Mathf.Deg2Rad;

                case DuelRole.Level:
                    return 0f;

                case DuelRole.Form:
                    return FormHeading(pos);

                case DuelRole.Support:
                    return Marked() ? HeadingTo(pos, LeadMark(pos)) : FormHeading(pos);

                default:
                    return 0f;
            }
        }

        float FormHeading(Vector3 pos)
        {
            if (_escort == null) return 0f;

            Vector3 station = _escort.position + StationOffset;
            float ahead = Mathf.Max(FormLookahead, (station.x - pos.x) * _facing);
            float rise = Mathf.Clamp(station.y - pos.y, -ahead, ahead);
            return Mathf.Atan2(rise, ahead * _facing);
        }

        void TrackHeading(float pathHeading, float dt)
        {
            float rate = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, pathHeading * Mathf.Rad2Deg)
                       * Mathf.Deg2Rad / dt;
            rate = Mathf.Clamp(rate, -_turnRate, _turnRate);

            _heading += rate * dt;
            _angularVelocity += (rate - _angularVelocity) * (1f - Mathf.Exp(-TrackFilter * dt));
        }

        void SteerToHeading(float targetHeading, float dt)
        {
            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, targetHeading * Mathf.Rad2Deg)
                        * Mathf.Deg2Rad;
            float rollOut = _angularVelocity / _turnResponse;
            float desiredRate = Mathf.Clamp((error - rollOut) * PilotGain, -_turnRate, _turnRate);

            float approach = 1f - Mathf.Exp(-_turnResponse * dt);
            _angularVelocity += (desiredRate - _angularVelocity) * approach;
            _heading += _angularVelocity * dt;
        }

        void UpdateBank(float dt, float depthRate)
        {
            if (_falling)
            {
                _bank += FallSpinDeg * dt;
                return;
            }

            float fromDepth = depthRate * BankPerDepthSpeed;
            float fromTurn = -_angularVelocity * Mathf.Rad2Deg * BankPerTurnRate;
            float target = Mathf.Clamp(fromDepth + fromTurn, -MaxBank, MaxBank);

            _bank = Mathf.Lerp(_bank, target, 1f - Mathf.Exp(-BankResponse * dt));
            _roll.Tick(dt, _heading, PlaneRoll.Steady(_angularVelocity, _turnRate),
                _turnRate * Mathf.Rad2Deg);
        }

        void ApplyRotation()
        {
            float flip = _roll.Angle;

            Quaternion rot = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg)
                           * Quaternion.Euler(flip + _bank * Mathf.Cos(flip * Mathf.Deg2Rad), 0f, 0f);
            if (_blendAge < UnscriptBlendSec)
                rot = Quaternion.Slerp(_blendFrom, rot, Mathf.SmoothStep(0f, 1f, _blendAge / UnscriptBlendSec));
            transform.rotation = rot;
        }

        static float HeadingTo(Vector3 from, Vector2 point)
        {
            return Mathf.Atan2(point.y - from.y, point.x - from.x);
        }

        void UpdateSupportGun(float dt)
        {
            if (_round == null) return;

            if (_burstLeft > 0)
            {
                _shotTimer -= dt;
                if (_shotTimer > 0f) return;

                _shotTimer = _shotSpacing;
                _burstLeft--;
                ShootRound();
                if (_burstLeft == 0)
                    _restTimer = Random.Range(SupportRestMin, SupportRestMax);
                return;
            }

            _restTimer -= dt;
            if (_restTimer > 0f || !AimedAtMark()) return;

            _burstLeft = Random.Range(SupportBurstMin, SupportBurstMax + 1);
            _shotTimer = 0f;
        }

        bool Marked()
        {
            if (_mark == null) return false;
            if (_escort == null) return true;

            Vector2 stray = (Vector2)_mark.position - (Vector2)_escort.position;
            return stray.sqrMagnitude <= SupportLeash * SupportLeash;
        }

        Vector2 LeadMark(Vector3 pos)
        {
            Vector2 target = _mark.position;
            float time = Vector2.Distance(pos, target) / _gunSpeed;
            return target + (Vector2)_mark.linearVelocity * time;
        }

        bool AimedAtMark()
        {
            Vector3 pos = transform.position;
            if (!Marked()) return false;
            if (Vector2.Distance(pos, _mark.position) > SupportFireRange) return false;

            float aim = HeadingTo(pos, LeadMark(pos)) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, aim))
                   <= SupportFireAngleDeg;
        }

        void ShootRound()
        {
            Vector3 dir = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f);
            Vector3 muzzle = Muzzle(dir);

            GameObject go = Instantiate(_round, muzzle,
                Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg - 90f));
            go.name = "CompanionBullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, _gunSpeed, _gunDamage, null, fromEnemy: false);

            MuzzleFlash.Spawn(muzzle, dir, _size);
            if (_shotClip != null && _audio != null)
                _audio.PlayOneShot(_shotClip, SupportShotVolume * AudioOptions.Scene);
        }

        Vector3 Muzzle(Vector3 dir) => _gun.HasValue
            ? transform.TransformPoint(_gun.Value)
            : transform.position + dir * (_size * 0.5f + MuzzleClearance);

        void OnDestroy()
        {
            Fallen.Remove(this);
            if (_round != null) Destroy(_round);
        }

        void Remove()
        {
            if (_smoke != null) _smoke.Clear();
            if (_fire != null) _fire.Extinguish();
            Destroy(gameObject);
        }
    }
}
