using UnityEngine;

namespace MetalRaptors
{
    public enum DuelRole { Escort, Peel, Hunt, Break, Cross, Level, Form, Idle }

    public class DuelPlane : MonoBehaviour
    {
        const string ShotClip = "Sounds/bullet_shot_1";

        public const float CruiseSpeed = 175f;

        const float PaceRate = 60f;
        const float PilotGain = 5f;

        const float BankPerDepthSpeed = 0.40f;
        const float BankPerTurnRate = 0.22f;
        const float MaxBank = 80f;
        const float BankResponse = 4.5f;

        const float StationResponse = 1.8f;
        const float StationCloseSpeed = 220f;
        const float TrackFilter = 8f;
        const float WobbleHz = 0.19f;
        const float WobbleRise = 10f;
        const float WobbleDrift = 12f;

        const float PeelClimbDeg = 24f;

        const float LevelToleranceDeg = 10f;
        const float FormLookahead = 130f;
        const float FormGain = 0.55f;
        const float FormPush = 95f;
        const float FormTrim = 70f;
        const float FormMinSpeed = 40f;
        const float FormReach = 70f;

        const float JinkHz = 0.5f;
        const float JinkDeg = 22f;
        const float BreakCrossDeg = 55f;
        const float BreakFlipSeconds = 2.4f;
        const float BreakFlipRate = 1.6f;

        const float CrossLead = 280f;
        const float CrossSpread = 110f;
        const float CrossHold = 90f;

        const float BreakOffRange = 190f;
        const float ReattackRange = 380f;
        const float OvershootSeconds = 1.1f;
        const float OvershootTurnDeg = 26f;
        const float RepositionArcDeg = 40f;
        const float RepositionSeconds = 3f;
        const float SeparationRange = 130f;
        const float SeparationWeight = 1.8f;

        const float FireRange = 470f;
        const float FireAngleDeg = 12f;
        const float ShotSpacing = 0.085f;
        const int BurstMin = 4;
        const int BurstMax = 8;
        const float RestMin = 0.9f;
        const float RestMax = 1.9f;
        const float MuzzleClearance = 6f;
        const float TracerSpeed = 430f;
        const float ShotVolume = 0.045f;

        const float SideMargin = 200f;
        const float FloorMargin = 100f;
        const float CeilingMargin = 100f;
        const float ClampSpeed = 120f;

        const float FallDiveDeg = PlaneFall.DiveDeg;
        const float FallSpinDeg = PlaneFall.RollRateDeg;
        const float FallDiveResponse = PlaneFall.DiveResponse;
        const float FallSpeedGain = PlaneFall.SpeedGain;
        const float FallExitMargin = 60f;

        enum PassState { Approach, Overshoot, Reposition }

        DuelRole _role = DuelRole.Idle;
        DuelPlane _opponent;
        Transform _escort;
        Vector3 _stationOffset;
        Vector3 _station;
        bool _hasStation;
        Vector2 _crossPoint;
        bool _hasCrossPoint;

        PassState _pass;
        float _passTimer;
        float _passHeading;
        float _passSide = 1f;
        float _breakSide = 1f;
        float _breakBlend = 1f;
        float _breakFlipTimer;

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
        float _roleTime;
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

        float _shotTimer, _restTimer;
        int _burstLeft;

        bool _falling;
        float _fallSpeed;
        float _fallHeadingDeg;

        SmokeTrail _smoke;
        PlaneFire _fire;
        AudioSource _audio;
        AudioClip _shotClip;

        public bool IsFalling => _falling;

        public bool Levelled =>
            Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, 0f)) <= LevelToleranceDeg;

        public bool Rolling => _roll != null && _roll.Rolling;

        public bool AtStation
        {
            get
            {
                if (_escort == null) return true;

                Vector2 gap = (Vector2)(_escort.position + _stationOffset)
                            - (Vector2)transform.position;
                return gap.sqrMagnitude <= FormReach * FormReach;
            }
        }

        public Vector2 Velocity =>
            new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading)) * _speed;

        public static DuelPlane Spawn(string name, PlaneModelConfig plane, PlayerConfig flight,
            Vector3 position, float heading, bool mirrored)
        {
            if (plane == null)
            {
                Debug.LogError("DuelPlane: no plane model configured.");
                return null;
            }

            if (flight == null) flight = ScriptableObject.CreateInstance<PlayerConfig>();

            var go = new GameObject(name);
            go.transform.position = position;
            PlaneFactory.BuildPlaneModel(go.transform, plane, mirrored,
                mirrored ? PlaneSkins.Default(plane) : null);

            foreach (Collider col in go.GetComponentsInChildren<Collider>()) Destroy(col);

            var duel = go.AddComponent<DuelPlane>();
            duel._size = plane.onScreenSize;
            duel._leadSpeed = Mathf.Max(1f, flight.flySpeed);
            duel._turnRate = flight.rotationSpeed * Mathf.Deg2Rad;
            duel._turnResponse = flight.turnResponsiveness / Mathf.Max(0.0001f, flight.mass);
            duel._diveAcceleration = flight.diveAcceleration;
            duel._speedDrag = flight.speedDrag;
            duel._maxSpeedFactor = Mathf.Max(1f, flight.maxSpeedMultiplier);
            duel._heading = heading;
            duel._roll = new PlaneRoll(mirrored);
            duel._z = position.z;
            duel._depthTo = position.z;
            duel._wobblePhase = Random.Range(0f, 10f);
            duel._restTimer = Random.Range(RestMin, RestMax);
            duel._smoke = go.AddComponent<SmokeTrail>();

            duel._shotClip = Resources.Load<AudioClip>(ShotClip);
            duel._audio = go.AddComponent<AudioSource>();
            duel._audio.playOnAwake = false;
            duel._audio.spatialBlend = 0f;

            duel.ApplyRotation();
            return duel;
        }

        public void SetEscort(Transform target, Vector3 offset)
        {
            _escort = target;
            _stationOffset = offset;
            _hasStation = false;
        }

        public void SetRole(DuelRole role, DuelPlane opponent, float crossSide = 1f)
        {
            if (_falling) return;

            _role = role;
            _opponent = opponent;
            _roleTime = 0f;
            _burstLeft = 0;
            _restTimer = Mathf.Max(_restTimer, Random.Range(RestMin, RestMax) * 0.5f);

            _pass = PassState.Approach;
            _breakSide = Random.value < 0.5f ? 1f : -1f;
            _breakBlend = _breakSide;
            _breakFlipTimer = BreakFlipSeconds;
            if (role == DuelRole.Escort) _hasStation = false;
            if (role == DuelRole.Cross) AimCross(crossSide);
        }

        public void FlipUpright()
        {
            if (_falling || _roll == null) return;
            _roll.Flip(_heading, _turnRate * Mathf.Rad2Deg);
        }

        public void SetDepth(float z, float seconds)
        {
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

            _role = DuelRole.Idle;
            _opponent = null;
            _escort = null;
            _burstLeft = 0;
            _bounded = false;
        }

        public void Kill()
        {
            if (_falling) return;

            _falling = true;
            _role = DuelRole.Idle;
            _opponent = null;
            _escort = null;
            _burstLeft = 0;
            _fallSpeed = Mathf.Max(_speed, CruiseSpeed);
            _fallHeadingDeg = Mathf.Cos(_heading) >= 0f ? FallDiveDeg : 180f - FallDiveDeg;

            Explosion.Spawn(transform.position, _size);
            if (_smoke != null) _smoke.Ignite(_size);
            _fire = PlaneFire.Ignite(gameObject, _size);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _roleTime += dt;

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
            if (_role == DuelRole.Hunt) UpdateGun(dt);
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

        void Crash(Vector3 point, bool water)
        {
            if (!water) Explosion.Spawn(point, _size);
            if (Battlefield.Current != null) Battlefield.Current.Impact(point, _size, water);
            Remove();
        }

        bool Escorting => _role == DuelRole.Escort || _role == DuelRole.Form;

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

            Vector3 station = _escort.position + _stationOffset
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

            Vector3 next = pos + carry + correction;

            Vector2 move = next - pos;
            if (move.sqrMagnitude > 1e-4f) TrackHeading(Mathf.Atan2(move.y, move.x), dt);
            return next;
        }

        Vector3 Fly(Vector3 pos, float dt)
        {
            TickEngagement(pos, dt);

            float target = Separate(RoleHeading(pos), pos);

            if (_bounded && _role != DuelRole.Idle)
                target = FlightSteering.Contain(target, pos, _minX, _maxX, SideMargin,
                    BandFloor + FloorMargin, FloorMargin, BandCeiling, CeilingMargin);

            SteerToHeading(target, dt);

            _pace = Mathf.MoveTowards(_pace, _paceTarget, PaceRate * dt);

            float cruise = _pace;
            float floor = cruise;
            if (_role == DuelRole.Form)
            {
                cruise = FormCruise(pos);
                floor = FormMinSpeed;
            }
            UpdateSpeed(cruise, floor, dt);

            return pos + new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f) * (_speed * dt);
        }

        float FormCruise(Vector3 pos)
        {
            if (_escort == null) return CruiseSpeed;

            float behind = _escort.position.x + _stationOffset.x - pos.x;
            return _leadSpeed + Mathf.Clamp(behind * FormGain, -FormTrim, FormPush);
        }

        void UpdateSpeed(float cruise, float floor, float dt)
        {
            _speed += -Mathf.Sin(_heading) * _diveAcceleration * dt;
            _speed -= (_speed - cruise) * _speedDrag * dt;
            _speed = Mathf.Clamp(_speed, floor, cruise * _maxSpeedFactor);
        }

        void TickEngagement(Vector3 pos, float dt)
        {
            if (_opponent == null) return;

            if (_role == DuelRole.Break)
            {
                _breakFlipTimer -= dt;
                if (_breakFlipTimer <= 0f)
                {
                    _breakSide = -_breakSide;
                    _breakFlipTimer = BreakFlipSeconds * Random.Range(0.8f, 1.3f);
                }
                _breakBlend = Mathf.MoveTowards(_breakBlend, _breakSide, BreakFlipRate * dt);
                return;
            }

            if (_role != DuelRole.Hunt) return;

            float range = Vector2.Distance(pos, _opponent.transform.position);

            switch (_pass)
            {
                case PassState.Approach:
                    if (range > BreakOffRange) return;

                    _pass = PassState.Overshoot;
                    _passTimer = OvershootSeconds;
                    _passSide = Random.value < 0.5f ? 1f : -1f;
                    _passHeading = _heading + OvershootTurnDeg * Mathf.Deg2Rad * _passSide;
                    return;

                case PassState.Overshoot:
                    _passTimer -= dt;
                    if (_passTimer > 0f) return;

                    _pass = PassState.Reposition;
                    _passTimer = RepositionSeconds;
                    return;

                case PassState.Reposition:
                    _passTimer -= dt;
                    if (range >= ReattackRange || _passTimer <= 0f) _pass = PassState.Approach;
                    return;
            }
        }

        float Separate(float heading, Vector3 pos)
        {
            if (_opponent == null || _role == DuelRole.Idle) return heading;

            Vector2 away = (Vector2)pos - (Vector2)_opponent.transform.position;
            float gap = away.magnitude;
            if (gap >= SeparationRange || gap < 0.01f) return heading;

            Vector2 dir = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading))
                        + away / gap * ((1f - gap / SeparationRange) * SeparationWeight);
            return dir.sqrMagnitude > 1e-6f ? Mathf.Atan2(dir.y, dir.x) : heading;
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
                {
                    if (_escort == null) return 0f;

                    Vector3 station = _escort.position + _stationOffset;
                    float ahead = Mathf.Max(FormLookahead, station.x - pos.x);
                    float rise = Mathf.Clamp(station.y - pos.y, -ahead, ahead);
                    return Mathf.Atan2(rise, ahead);
                }

                case DuelRole.Hunt:
                {
                    if (_opponent == null) return _heading;

                    if (_pass == PassState.Overshoot) return _passHeading;
                    if (_pass == PassState.Reposition)
                        return HeadingTo(pos, _opponent.transform.position)
                             + RepositionArcDeg * _passSide * Mathf.Deg2Rad;
                    return HeadingTo(pos, Lead(pos));
                }

                case DuelRole.Break:
                {
                    if (_opponent == null) return _heading;

                    return AwayHeading(pos)
                         + BreakCrossDeg * _breakBlend * Mathf.Deg2Rad
                         + Mathf.Sin(_roleTime * Mathf.PI * 2f * JinkHz) * JinkDeg * Mathf.Deg2Rad;
                }

                case DuelRole.Cross:
                {
                    if (!_hasCrossPoint) return _heading;

                    Vector2 to = _crossPoint - (Vector2)pos;
                    return to.sqrMagnitude > CrossHold * CrossHold
                        ? Mathf.Atan2(to.y, to.x)
                        : _heading;
                }

                default:
                    return 0f;
            }
        }

        void AimCross(float side)
        {
            _hasCrossPoint = false;
            if (_opponent == null) return;

            Vector2 pos = transform.position;
            Vector2 to = (Vector2)_opponent.transform.position - pos;
            if (to.sqrMagnitude < 1f) return;

            Vector2 offset = new Vector2(-to.y, to.x).normalized * (CrossSpread * side);
            _crossPoint = pos + to.normalized * CrossLead + offset;
            _hasCrossPoint = true;
        }

        float AwayHeading(Vector3 pos)
        {
            Vector2 away = (Vector2)pos - (Vector2)_opponent.transform.position;
            return away.sqrMagnitude > 1f ? Mathf.Atan2(away.y, away.x) : _heading;
        }

        Vector2 Lead(Vector3 pos)
        {
            Vector2 target = _opponent.transform.position;
            Vector2 travel = _opponent.Velocity;
            float time = Vector2.Distance(pos, target) / TracerSpeed;
            return target + travel * time;
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

            transform.rotation = Quaternion.Euler(0f, 0f, _heading * Mathf.Rad2Deg)
                               * Quaternion.Euler(flip + _bank * Mathf.Cos(flip * Mathf.Deg2Rad),
                                                  0f, 0f);
        }

        static float HeadingTo(Vector3 from, Vector2 point)
        {
            return Mathf.Atan2(point.y - from.y, point.x - from.x);
        }

        void UpdateGun(float dt)
        {
            if (_opponent == null || _opponent.IsFalling) return;

            if (_burstLeft > 0)
            {
                _shotTimer -= dt;
                if (_shotTimer > 0f) return;

                _shotTimer = ShotSpacing;
                _burstLeft--;
                Shoot();
                if (_burstLeft == 0) _restTimer = Random.Range(RestMin, RestMax);
                return;
            }

            _restTimer -= dt;
            if (_restTimer > 0f || !Aimed()) return;

            _burstLeft = Random.Range(BurstMin, BurstMax + 1);
            _shotTimer = 0f;
        }

        bool Aimed()
        {
            Vector3 pos = transform.position;
            if (Vector2.Distance(pos, _opponent.transform.position) > FireRange) return false;

            float aim = HeadingTo(pos, Lead(pos)) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, aim)) <= FireAngleDeg;
        }

        void Shoot()
        {
            Vector3 dir = new Vector3(Mathf.Cos(_heading), Mathf.Sin(_heading), 0f);
            Vector3 muzzle = transform.position + dir * (_size * 0.5f + MuzzleClearance);

            Tracer.Spawn(muzzle, dir, TracerSpeed);
            MuzzleFlash.Spawn(muzzle, dir, _size);
            if (_shotClip != null && _audio != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Sfx);
        }

        void Remove()
        {
            if (_smoke != null) _smoke.Clear();
            if (_fire != null) _fire.Extinguish();
            Destroy(gameObject);
        }
    }
}
