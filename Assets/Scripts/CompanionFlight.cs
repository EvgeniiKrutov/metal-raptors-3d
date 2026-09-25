using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CompanionFlight
    {
        public const float Depth = 250f;

        const float StationAhead = 65f;
        const float StationAbove = 32f;

        const float PeelSeconds = 1.6f;
        const float CloseSeconds = 2.2f;

        const float AlignLimit = 2.5f;
        const float RollLimit = 1.5f;
        const float FormLimit = 8f;

        const float BandBehind = 0.72f;
        const float BandAhead = 0.88f;
        const float BandVertical = 0.86f;
        const float StationLead = 0.12f;

        const float PaceFilter = 3f;
        const float PaceGain = 0.35f;
        const float PaceTrim = 55f;
        const float PacePush = 90f;
        const float PaceCeiling = 1.6f;

        const float FoeEntryMargin = 160f;
        const float FoeEntryRise = 70f;

        const float DuelFloorMargin = 140f;
        const float DuelCeilingMargin = 110f;
        const float EscortFloorMargin = 60f;
        const float EscortCeilingMargin = 40f;

        const float HuntMin = 6f;
        const float HuntMax = 9f;
        const float CrossSeconds = 1.6f;

        const float BackSpread = 190f;
        const float BackRise = 130f;
        const float SupportStationAhead = 40f;
        const float SupportStationAbove = 60f;

        const float BumpReach = PlaneScrapes.HitboxRadius * 2f;
        const float BumpCooldown = 0.5f;
        const float BumpDepthBand = 40f;

        enum Phase { Escort, Peel, Duel, Align, Roll, Form, Close }

        class BackPair
        {
            public DuelPlane friend;
            public DuelPlane foe;
            public DuelPlane wreck;
            public float roleTimer;
            public bool friendHunts = true;
            public bool crossing;
        }

        readonly CampaignDefinition _level;
        readonly PlayerConfig _flight;
        readonly Transform _player;
        readonly float _playZ;
        readonly float _groundY;
        readonly float _worldTop;
        readonly float _depthScale;

        DuelPlane _companion;
        DuelPlane _foe;
        DuelPlane _wreck;
        PlaneModelConfig _foePlane;

        readonly List<BackPair> _backs = new List<BackPair>();
        bool _support;
        IReadOnlyList<EnemyController> _marks;

        Phase _phase = Phase.Escort;
        bool _cinematic = true;
        bool _standDown;
        float _phaseTimer;
        float _roleTimer;
        bool _crossing;
        bool _companionHunts = true;
        float _bumpTime = -999f;

        float _camX;
        float _camY;
        float _camSpeed = DuelPlane.CruiseSpeed;
        bool _hasCam;
        float _halfView;
        float _halfHeight;
        float _floorY;
        float _ceilingY;

        public PlaneModelConfig FoePlane => _foePlane;

        CompanionFlight(CampaignDefinition level, PlayerConfig playerFlight, Transform player,
            float playZ, float groundY, float worldTop, float cameraDistance,
            PlaneModelConfig foe)
        {
            _level = level;
            _foePlane = foe != null ? foe : level.companionFoe;
            _flight = playerFlight;
            _player = player;
            _playZ = playZ;
            _groundY = groundY;
            _worldTop = worldTop;
            _depthScale = cameraDistance > 1f ? (cameraDistance + Depth) / cameraDistance : 1f;
            _floorY = groundY + DuelFloorMargin;
            _ceilingY = worldTop - DuelCeilingMargin;
        }

        public static CompanionFlight Begin(CampaignDefinition level, PlayerConfig playerFlight,
            Transform player, float playZ, float groundY, float worldTop, float cameraDistance,
            PlaneModelConfig foe = null)
        {
            if (level == null || !level.companion || player == null) return null;

            var flight = new CompanionFlight(level, playerFlight, player, playZ, groundY, worldTop,
                cameraDistance, foe);

            flight._support = level.supportCompanion;

            Vector3 slot = flight.Station;
            Vector3 station = player.position + slot;
            station.z = playZ;
            flight._companion = DuelPlane.Spawn("Companion", level.companionPlane, playerFlight,
                station, 0f, mirrored: false, flight.SkinFor(0));

            if (flight._companion == null) return null;

            flight._companion.SetEscort(player, slot);
            flight._companion.SetEscortBounds(flight.EscortFloorY, flight.EscortCeilingY);
            flight._companion.SetGround(groundY);
            flight._companion.SetRole(DuelRole.Escort, null);
            if (flight._support) flight._companion.ArmGuns(playerFlight);

            for (int i = 0; i < level.backCompanions; i++) flight.AddBackPair(i, playerFlight);
            return flight;
        }

        PlaneSkin SkinFor(int index) =>
            PlaneSkins.Companion(_level.companionPlane,
                CampaignSpeakers.SkinOf(_level.CompanionPilot(index)));

        void AddBackPair(int index, PlayerConfig playerFlight)
        {
            Vector3 at = _player.position
                       + new Vector3(-BackSpread * index, StationAbove + BackRise * index, Depth);

            var pair = new BackPair
            {
                friend = DuelPlane.Spawn($"Companion Back {index + 1}", _level.companionPlane,
                    playerFlight, at, 0f, mirrored: false, SkinFor(index + 1)),
            };
            if (pair.friend == null) return;

            pair.friend.SetGround(_groundY);
            _backs.Add(pair);

            SpawnBackFoe(pair, at + new Vector3(BackSpread, 0f, 0f), playerFlight);
            BeginBackDuel(pair);
        }

        void SpawnBackFoe(BackPair pair, Vector3 at, PlayerConfig playerFlight)
        {
            pair.foe = DuelPlane.Spawn("Background Foe", _foePlane, playerFlight, at, Mathf.PI,
                mirrored: true, PlaneSkins.Default(_foePlane));
            if (pair.foe != null) pair.foe.SetGround(_groundY);
        }

        void BeginBackDuel(BackPair pair)
        {
            pair.crossing = false;
            pair.friendHunts = Random.value < 0.5f;
            pair.roleTimer = Random.Range(HuntMin, HuntMax);
            ApplyBackRoles(pair);
        }

        void ApplyBackRoles(BackPair pair)
        {
            if (pair.friend == null || pair.foe == null) return;

            pair.friend.SetRole(pair.friendHunts ? DuelRole.Hunt : DuelRole.Break, pair.foe);
            pair.foe.SetRole(pair.friendHunts ? DuelRole.Break : DuelRole.Hunt, pair.friend);
        }

        void TickBack(float dt)
        {
            for (int i = 0; i < _backs.Count; i++)
            {
                BackPair pair = _backs[i];
                if (pair.friend == null || pair.foe == null) continue;

                pair.roleTimer -= dt;
                if (pair.roleTimer > 0f) continue;

                if (pair.crossing)
                {
                    pair.crossing = false;
                    pair.friendHunts = !pair.friendHunts;
                    pair.roleTimer = Random.Range(HuntMin, HuntMax);
                    ApplyBackRoles(pair);
                }
                else
                {
                    pair.crossing = true;
                    pair.roleTimer = CrossSeconds;
                    pair.friend.SetRole(DuelRole.Cross, pair.foe, 1f);
                    pair.foe.SetRole(DuelRole.Cross, pair.friend, -1f);
                }
            }
        }

        Vector3 Station => _support
            ? new Vector3(SupportStationAhead, SupportStationAbove, 0f)
            : new Vector3(StationAhead, StationAbove, 0f);

        float EscortFloorY => _groundY + EscortFloorMargin;

        float EscortCeilingY => _worldTop - EscortCeilingMargin;

        float StationX => _camX + _halfView * StationLead;

        float CentreX
        {
            get
            {
                if (_backs.Count > 0) return BackCentreX;
                if (_companion == null) return StationX;
                if (_foe == null) return _companion.transform.position.x;
                return (_companion.transform.position.x + _foe.transform.position.x) * 0.5f;
            }
        }

        float BackCentreX
        {
            get
            {
                float sum = 0f;
                int n = 0;
                foreach (BackPair pair in _backs)
                {
                    if (pair.friend != null) { sum += pair.friend.transform.position.x; n++; }
                    if (pair.foe != null) { sum += pair.foe.transform.position.x; n++; }
                }
                return n > 0 ? sum / n : StationX;
            }
        }

        public void SetWindow(Vector3 camPos, float halfViewWidth, float halfViewHeight)
        {
            float dt = Time.deltaTime;
            if (_hasCam && dt > 0f)
            {
                float rate = (camPos.x - _camX) / dt;
                _camSpeed += (rate - _camSpeed) * (1f - Mathf.Exp(-PaceFilter * dt));
            }
            _hasCam = true;

            _camX = camPos.x;
            _camY = camPos.y;
            _halfView = halfViewWidth * _depthScale;
            _halfHeight = halfViewHeight * _depthScale;

            ApplyView();
            if (_standDown) return;

            ApplyBand();
            ApplyPace();
        }

        void ApplyView()
        {
            float minX = _camX - _halfView;
            float maxX = _camX + _halfView;
            float minY = _camY - _halfHeight;
            float maxY = _camY + _halfHeight;

            if (_companion != null) _companion.SetView(minX, maxX, minY, maxY);
            if (_foe != null) _foe.SetView(minX, maxX, minY, maxY);
            if (_wreck != null) _wreck.SetView(minX, maxX, minY, maxY);

            foreach (BackPair pair in _backs)
            {
                if (pair.friend != null) pair.friend.SetView(minX, maxX, minY, maxY);
                if (pair.foe != null) pair.foe.SetView(minX, maxX, minY, maxY);
                if (pair.wreck != null) pair.wreck.SetView(minX, maxX, minY, maxY);
            }
        }

        void ApplyBand()
        {
            float minX = _camX - _halfView * BandBehind;
            float maxX = _camX + _halfView * BandAhead;

            _floorY = Mathf.Max(_groundY + DuelFloorMargin, _camY - _halfHeight * BandVertical);
            _ceilingY = Mathf.Min(_worldTop - DuelCeilingMargin,
                _camY + _halfHeight * BandVertical);
            if (_floorY > _ceilingY) _floorY = _ceilingY = (_floorY + _ceilingY) * 0.5f;

            if (_companion != null)
            {
                if (_support)
                {
                    float play = _halfView / _depthScale;
                    _companion.SetBounds(_camX - play * BandBehind, _camX + play * BandAhead,
                        _floorY, _ceilingY);
                }
                else
                {
                    _companion.SetBounds(minX, maxX, _floorY, _ceilingY);
                }
            }
            if (_foe != null) _foe.SetBounds(minX, maxX, _floorY, _ceilingY);

            foreach (BackPair pair in _backs)
            {
                if (pair.friend != null) pair.friend.SetBounds(minX, maxX, _floorY, _ceilingY);
                if (pair.foe != null) pair.foe.SetBounds(minX, maxX, _floorY, _ceilingY);
            }
        }

        void ApplyPace()
        {
            float pace = Mathf.Clamp(_camSpeed, DuelPlane.CruiseSpeed,
                DuelPlane.CruiseSpeed * PaceCeiling);
            float cruise = pace + Mathf.Clamp((StationX - CentreX) * PaceGain, -PaceTrim, PacePush);

            if (_companion != null) _companion.SetPace(cruise);
            if (_foe != null) _foe.SetPace(cruise);

            foreach (BackPair pair in _backs)
            {
                if (pair.friend != null) pair.friend.SetPace(cruise);
                if (pair.foe != null) pair.foe.SetPace(cruise);
            }
        }

        public bool Formed => _standDown || _companion == null
            || _phase == Phase.Escort || _phase == Phase.Peel || _phase == Phase.Duel;

        public void SetCinematic(bool value)
        {
            if (value == _cinematic) return;
            _cinematic = value;
            if (_standDown || _companion == null) return;

            if (_support)
            {
                _companion.SetMark(null);
                _companion.SetRole(value ? DuelRole.Escort : DuelRole.Support, null);
                return;
            }

            if (value)
            {
                if (_phase == Phase.Peel || _phase == Phase.Duel) BeginAlign();
            }
            else if (_phase != Phase.Peel && _phase != Phase.Duel)
            {
                Peel();
            }
        }

        public void SetTargets(IReadOnlyList<EnemyController> enemies)
        {
            _marks = enemies;
        }

        void AimSupport()
        {
            if (_companion == null || _cinematic || _standDown) return;

            Rigidbody best = null;
            float nearest = float.MaxValue;
            Vector3 from = _player.position;

            if (_marks != null)
            {
                for (int i = 0; i < _marks.Count; i++)
                {
                    EnemyController enemy = _marks[i];
                    if (enemy == null || enemy.CurrentHealth <= 0f) continue;

                    Rigidbody body = enemy.Body;
                    if (body == null) continue;

                    float range = ((Vector2)enemy.transform.position - (Vector2)from).sqrMagnitude;
                    if (range >= nearest) continue;

                    nearest = range;
                    best = body;
                }
            }

            _companion.SetMark(best);
        }

        public void Tick(float dt)
        {
            if (_standDown || _companion == null) return;

            TickBack(dt);

            if (_support)
            {
                AimSupport();
                return;
            }

            switch (_phase)
            {
                case Phase.Peel:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) BeginDuel();
                    return;

                case Phase.Duel:
                    TickDuel(dt);
                    return;

                case Phase.Align:
                    _phaseTimer -= dt;
                    if (_companion.Levelled || _phaseTimer <= 0f) BeginRoll();
                    return;

                case Phase.Roll:
                    _phaseTimer -= dt;
                    if (!_companion.Rolling || _phaseTimer <= 0f) BeginForm();
                    return;

                case Phase.Form:
                    _phaseTimer -= dt;
                    if (_companion.AtStation || _phaseTimer <= 0f) BeginClose();
                    return;

                case Phase.Close:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) Formate();
                    return;
            }
        }

        public bool CheckBump(Transform playerTr)
        {
            if (_companion == null || playerTr == null || _standDown) return false;
            if (Time.time - _bumpTime < BumpCooldown) return false;

            Vector3 pos = _companion.transform.position;
            if (Mathf.Abs(pos.z - _playZ) > BumpDepthBand) return false;

            Vector2 gap = (Vector2)pos - (Vector2)playerTr.position;
            if (gap.sqrMagnitude > BumpReach * BumpReach) return false;

            _bumpTime = Time.time;
            return true;
        }

        public void SetFoe(PlaneModelConfig plane)
        {
            if (plane == null || plane == _foePlane) return;
            _foePlane = plane;

            foreach (BackPair pair in _backs)
            {
                if (pair.foe == null) continue;

                pair.foe.Kill();
                pair.wreck = pair.foe;
                pair.foe = null;

                Vector3 at = new Vector3(_camX + _halfView + FoeEntryMargin,
                    Mathf.Clamp(pair.friend != null ? pair.friend.transform.position.y
                                                    : _player.position.y,
                        _floorY, _ceilingY),
                    _playZ + Depth);

                SpawnBackFoe(pair, at, _flight);
                BeginBackDuel(pair);
            }
        }

        public void StandDown()
        {
            _standDown = true;
            if (_companion != null) { _companion.SetMark(null); _companion.StandDown(); }
            if (_foe != null) _foe.StandDown();

            foreach (BackPair pair in _backs)
            {
                if (pair.friend != null) pair.friend.StandDown();
                if (pair.foe != null) pair.foe.StandDown();
            }
        }

        void Peel()
        {
            _phase = Phase.Peel;
            _phaseTimer = PeelSeconds;

            _companion.SetRole(DuelRole.Peel, null);
            _companion.SetDepth(_playZ + Depth, PeelSeconds);
            SpawnFoe();
        }

        void BeginAlign()
        {
            _phase = Phase.Align;
            _phaseTimer = AlignLimit;

            if (_foe != null)
            {
                _foe.Kill();
                _wreck = _foe;
                _foe = null;
            }

            _companion.HoldDepth();
            _companion.SetRole(DuelRole.Level, null);
        }

        void BeginRoll()
        {
            _phase = Phase.Roll;
            _phaseTimer = RollLimit;

            _companion.FlipUpright();
        }

        void BeginForm()
        {
            _phase = Phase.Form;
            _phaseTimer = FormLimit;

            _companion.SetEscort(_player, Station);
            _companion.SetRole(DuelRole.Form, null);
        }

        void BeginClose()
        {
            _phase = Phase.Close;
            _phaseTimer = CloseSeconds;

            _companion.SetDepth(_playZ, CloseSeconds);
        }

        void Formate()
        {
            _phase = Phase.Escort;

            _companion.SetEscort(_player, Station);
            _companion.SetRole(DuelRole.Escort, null);
        }

        void SpawnFoe()
        {
            if (_foe != null) return;

            float y = Mathf.Clamp(_player.position.y + Random.Range(-FoeEntryRise, FoeEntryRise),
                _floorY, _ceilingY);
            var entry = new Vector3(_camX + _halfView + FoeEntryMargin, y, _playZ + Depth);

            _foe = DuelPlane.Spawn("Background Foe", _foePlane, _flight, entry,
                Mathf.PI, mirrored: true, PlaneSkins.Default(_foePlane));
            if (_foe == null) return;

            _foe.SetBounds(_camX - _halfView * BandBehind, _camX + _halfView * BandAhead,
                _floorY, _ceilingY);
            _foe.SetGround(_groundY);
            _foe.SetRole(DuelRole.Hunt, _companion);
        }

        void BeginDuel()
        {
            _phase = Phase.Duel;
            _crossing = false;
            _companionHunts = true;
            _roleTimer = Random.Range(HuntMin, HuntMax);
            ApplyRoles();
        }

        void TickDuel(float dt)
        {
            if (_foe == null) return;

            _roleTimer -= dt;
            if (_roleTimer > 0f) return;

            if (_crossing)
            {
                _crossing = false;
                _companionHunts = !_companionHunts;
                _roleTimer = Random.Range(HuntMin, HuntMax);
                ApplyRoles();
                return;
            }

            _crossing = true;
            _roleTimer = CrossSeconds;
            _companion.SetRole(DuelRole.Cross, _foe, 1f);
            _foe.SetRole(DuelRole.Cross, _companion, -1f);
        }

        void ApplyRoles()
        {
            if (_foe == null) return;

            _companion.SetRole(_companionHunts ? DuelRole.Hunt : DuelRole.Break, _foe);
            _foe.SetRole(_companionHunts ? DuelRole.Break : DuelRole.Hunt, _companion);
        }
    }
}
