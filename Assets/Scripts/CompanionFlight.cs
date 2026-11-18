using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CompanionFlight
    {
        public const float Depth = 250f;

        const float StationAbove = 32f;

        const float SlotAhead = 90f;
        const float SlotAbove = 50f;
        const float EntryClearance = 20f;
        const string LeadPilot = "roussel";

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
        const float PaceTrim = 55f * FlightPace.Fast;
        const float PacePush = 90f * FlightPace.Fast;
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

        const float KillGap = 1f;

        const float ArrowTip = 95f;
        const float ArrowBack = 120f;
        const float ArrowArm = 90f;

        const float BackSpread = 190f;
        const float BackRise = 130f;
        const float SupportStationAhead = 40f;
        const float SupportStationAbove = 60f;

        float BumpReach => PlaneScrapes.HitboxRadius * PlaneModelConfig.LevelScale
                           * (1f + _level.PlayerPlane.hitboxScale);
        const float BumpCooldown = 0.5f;
        const float BumpDepthBand = 40f;

        enum Phase { Escort, Peel, Duel, Align, Roll, Form, Close }

        class Doomed
        {
            public DuelPlane foe;
            public System.Action after;
            public bool revocable;
        }

        class BackPair
        {
            public DuelPlane friend;
            public DuelPlane foe;
            public DuelPlane wreck;
            public float roleTimer;
            public bool friendHunts = true;
            public bool crossing;
            public bool peeling;
            public bool formed;
        }

        readonly CampaignDefinition _level;
        readonly PlayerConfig _flight;
        readonly Transform _player;
        readonly float _playZ;
        readonly float _groundY;
        readonly float _worldTop;
        readonly float _lift;
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
        bool _flyIn;
        bool _standDown;
        float _phaseTimer;
        float _roleTimer;
        bool _crossing;
        bool _companionHunts = true;
        float _bumpTime = -999f;
        bool _regrouping;
        float _regroupTime;
        bool _parade;
        float _paradeTime;
        bool _slaved;
        float _facing = 1f;
        bool _fight;
        readonly List<Doomed> _doomed = new List<Doomed>();
        float _doomIn;
        float _lastKill = -999f;

        float _camX;
        float _camY;
        float _camSpeed = DuelPlane.CruiseSpeed;
        bool _hasCam;
        float _halfView;
        float _halfHeight;
        float _floorY;
        float _ceilingY;

        public PlaneModelConfig FoePlane => _foePlane;

        public bool Slaved => _slaved;

        public bool CompanionGone => _companion == null;

        public float Facing
        {
            set
            {
                float facing = value < 0f ? -1f : 1f;
                _facing = facing;
                if (_companion != null) _companion.SetFacing(facing);
                foreach (BackPair pair in _backs)
                    if (pair.friend != null) pair.friend.SetFacing(facing);
            }
        }

        public void Close(float seconds)
        {
            if (_companion == null && _backs.Count == 0) return;

            _slaved = true;
            _regrouping = false;
            _parade = false;

            DropFoe();

            if (_companion != null)
            {
                _companion.SetMark(null);
                _companion.Slave(_player, Slot(0), seconds);
            }

            for (int i = 0; i < _backs.Count; i++)
            {
                BackPair pair = _backs[i];
                DropBackFoe(pair);
                pair.formed = true;
                if (pair.friend != null) pair.friend.Slave(_player, Slot(i + 1), seconds);
            }
        }

        public void Join()
        {
            if (_companion == null && _backs.Count == 0) return;

            _slaved = true;
            _regrouping = false;
            _parade = false;

            DropFoe();

            var mates = new List<DuelPlane>();
            if (_companion != null) mates.Add(_companion);
            foreach (BackPair pair in _backs)
                if (pair.friend != null) mates.Add(pair.friend);

            if (_companion != null)
            {
                _companion.SetMark(null);
                _companion.Join(_player, Slot(0), mates);
            }

            for (int i = 0; i < _backs.Count; i++)
            {
                BackPair pair = _backs[i];
                DropBackFoe(pair);
                pair.formed = true;
                if (pair.friend != null) pair.friend.Join(_player, Slot(i + 1), mates);
            }
        }

        public bool Joined(float reach)
        {
            if (_companion != null && !_companion.IsFalling && _companion.SlotGap > reach) return false;
            foreach (BackPair pair in _backs)
                if (pair.friend != null && !pair.friend.IsFalling && pair.friend.SlotGap > reach) return false;
            return true;
        }

        public void Arrow(float seconds)
        {
            if (_companion != null) _companion.Reslot(ArrowSlot(0), seconds, true);
            for (int i = 0; i < _backs.Count; i++)
                if (_backs[i].friend != null) _backs[i].friend.Reslot(ArrowSlot(i + 1), seconds, true);
        }

        public void Unarrow(float seconds)
        {
            if (_companion != null) _companion.Reslot(Slot(0), seconds, true);
            for (int i = 0; i < _backs.Count; i++)
                if (_backs[i].friend != null) _backs[i].friend.Reslot(Slot(i + 1), seconds, true);
        }

        public void ManualFollow(bool on)
        {
            if (_companion != null) _companion.SetManualFollow(on);
            foreach (BackPair pair in _backs)
                if (pair.friend != null) pair.friend.SetManualFollow(on);
        }

        public void FreeFollow(bool on, float settleSec)
        {
            if (_companion != null) _companion.SetFreeFollow(on, settleSec);
            foreach (BackPair pair in _backs)
                if (pair.friend != null) pair.friend.SetFreeFollow(on, settleSec);
        }

        public void Follow(float dt)
        {
            if (_companion != null) _companion.FollowNow(dt);
            foreach (BackPair pair in _backs)
                if (pair.friend != null) pair.friend.FollowNow(dt);
        }

        public void ArrowSlots(List<Vector3> into)
        {
            if (_companion != null && !_companion.IsFalling) into.Add(ArrowSlot(0));
            for (int i = 0; i < _backs.Count; i++)
                if (_backs[i].friend != null && !_backs[i].friend.IsFalling) into.Add(ArrowSlot(i + 1));
        }

        Vector3 ArrowSlot(int index)
        {
            switch (_level.CompanionPilot(index))
            {
                case "roussel": return new Vector3(0f, 0f, -ArrowTip);
                case "crane": return new Vector3(-ArrowBack, 0f, ArrowArm);
                case "marchand": return new Vector3(-ArrowBack, 0f, -ArrowTip - ArrowArm);
                default: return Slot(index);
            }
        }

        public void Planes(List<Transform> into)
        {
            if (_companion != null && !_companion.IsFalling) into.Add(_companion.transform);
            foreach (BackPair pair in _backs)
                if (pair.friend != null && !pair.friend.IsFalling) into.Add(pair.friend.transform);
        }

        public void DownCompanion()
        {
            if (_companion != null && !_companion.IsFalling) _companion.Kill();
        }

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
            _lift = Mathf.Max(Span(level.companionPlane), Span(_foePlane)) * 0.5f
                    * (PlaneModelConfig.LevelScale - 1f);
            _floorY = groundY + DuelFloorMargin + _lift;
            _ceilingY = worldTop - DuelCeilingMargin - _lift;
        }

        public static CompanionFlight Begin(CampaignDefinition level, PlayerConfig playerFlight,
            Transform player, float playZ, float groundY, float worldTop, float cameraDistance,
            PlaneModelConfig foe = null, bool flyIn = false)
        {
            if (level == null || !level.companion || player == null) return null;

            var flight = new CompanionFlight(level, playerFlight, player, playZ, groundY, worldTop,
                cameraDistance, foe);

            flight._support = level.supportCompanion;
            flight._flyIn = flyIn;
            flight._parade = flight._support && !flyIn;

            Vector3 slot = flight.Slot(0);
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

        public static Vector3 SlotFor(CampaignDefinition level, int index)
        {
            if (level.CompanionPilot(index) == LeadPilot)
                return new Vector3(SlotAhead, SlotAbove, 0f);

            int trail = 0;
            for (int i = 0; i < index; i++)
                if (level.CompanionPilot(i) != LeadPilot) trail++;

            int rank = trail / 2 + 1;
            float side = trail % 2 == 0 ? 1f : -1f;
            return new Vector3(-SlotAhead * rank, SlotAbove * side * rank, 0f);
        }

        static int FlightSize(CampaignDefinition level) =>
            level != null && level.companion ? 1 + Mathf.Max(0, level.backCompanions) : 0;

        public static float EntryLead(CampaignDefinition level)
        {
            float lead = 0f;
            for (int i = 0; i < FlightSize(level); i++) lead = Mathf.Max(lead, SlotFor(level, i).x);
            return lead;
        }

        public static float EntryLift(CampaignDefinition level)
        {
            float low = 0f;
            for (int i = 0; i < FlightSize(level); i++) low = Mathf.Min(low, SlotFor(level, i).y);
            return low < 0f ? EntryClearance - low : 0f;
        }

        Vector3 Slot(int index) => SlotFor(_level, index);

        PlaneSkin SkinFor(int index) =>
            PlaneSkins.Companion(_level.companionPlane,
                CampaignSpeakers.SkinOf(_level.CompanionPilot(index)));

        void AddBackPair(int index, PlayerConfig playerFlight)
        {
            Vector3 slot = Slot(index + 1);
            Vector3 at = _player.position + (_flyIn ? slot
                : _parade ? slot + new Vector3(0f, 0f, Depth)
                : new Vector3(-BackSpread * index, StationAbove + BackRise * index, Depth));

            var pair = new BackPair
            {
                friend = DuelPlane.Spawn($"Companion Back {index + 1}", _level.companionPlane,
                    playerFlight, at, 0f, mirrored: false, SkinFor(index + 1)),
            };
            if (pair.friend == null) return;

            pair.friend.SetGround(_groundY);
            _backs.Add(pair);

            if (_flyIn || _parade)
            {
                pair.formed = true;
                pair.friend.SetEscort(_player, slot);
                pair.friend.SetEscortBounds(EscortFloorY, EscortCeilingY);
                pair.friend.SetRole(DuelRole.Escort, null);
                return;
            }

            if (!_fight)
            {
                pair.friend.SetRole(DuelRole.Cruise, null);
                return;
            }

            SpawnBackFoe(pair, at + new Vector3(BackSpread, 0f, 0f), playerFlight);
            BeginBackDuel(pair);
        }

        bool Gathered => _flyIn || _parade || _regrouping;

        public void BeginFight()
        {
            _fight = true;
            _doomed.RemoveAll(doomed => doomed.revocable);
            if (_standDown || _slaved || Gathered) return;

            foreach (BackPair pair in _backs)
            {
                if (pair.friend == null || pair.foe != null) continue;

                SpawnBackFoe(pair, BackFoeEntry(pair), _flight);
                if (pair.foe == null) continue;
                if (pair.peeling) pair.foe.SetRole(DuelRole.Hunt, pair.friend);
                else BeginBackDuel(pair);
            }

            if (_support || _companion == null || _foe != null) return;
            if (_phase != Phase.Peel && _phase != Phase.Duel) return;

            SpawnFoe();
            if (_phase == Phase.Duel) BeginDuel();
        }

        public void EndFight()
        {
            _fight = false;

            foreach (BackPair pair in _backs)
            {
                DuelPlane foe = pair.foe;
                if (foe == null) continue;

                Doom(foe, KillGap, () =>
                {
                    if (pair.foe != foe) return;
                    pair.foe = null;
                    if (foe != null) pair.wreck = foe;
                    pair.peeling = false;
                    pair.crossing = false;
                    if (pair.friend != null) pair.friend.SetRole(DuelRole.Cruise, null);
                }, true);
            }

            DuelPlane own = _foe;
            if (own == null) return;

            Doom(own, KillGap, () =>
            {
                if (_foe != own) return;
                _foe = null;
                if (own != null) _wreck = own;
                _crossing = false;
                if (_companion != null && (_phase == Phase.Peel || _phase == Phase.Duel))
                    _companion.SetRole(DuelRole.Cruise, null);
            }, true);
        }

        void Doom(DuelPlane foe, float delay, System.Action after, bool revocable = false)
        {
            if (foe == null || foe.IsFalling)
            {
                after?.Invoke();
                return;
            }

            if (!InView(foe.transform.position))
            {
                Object.Destroy(foe.gameObject);
                after?.Invoke();
                return;
            }

            if (_doomed.Count == 0) _doomIn = Mathf.Max(delay, _lastKill + KillGap - Time.time);
            _doomed.Add(new Doomed { foe = foe, after = after, revocable = revocable });
        }

        void TickDoomed(float dt)
        {
            if (_doomed.Count == 0) return;

            _doomIn -= dt;
            if (_doomIn > 0f) return;

            Doomed next = _doomed[0];
            _doomed.RemoveAt(0);

            bool shot = next.foe != null && !next.foe.IsFalling;
            if (shot)
            {
                next.foe.Kill();
                _lastKill = Time.time;
            }
            next.after?.Invoke();
            _doomIn = shot ? KillGap : 0f;
        }

        void DropFoe()
        {
            DuelPlane foe = _foe;
            if (foe == null) return;

            _foe = null;
            Doom(foe, 0f, () => { if (foe != null) _wreck = foe; });
        }

        Vector3 BackFoeEntry(BackPair pair) => new Vector3(_camX + _halfView + FoeEntryMargin,
            Mathf.Clamp(pair.friend != null ? pair.friend.transform.position.y : _player.position.y,
                _floorY, _ceilingY),
            _playZ + Depth);

        void PeelBacks()
        {
            foreach (BackPair pair in _backs)
            {
                if (pair.friend == null) continue;

                pair.peeling = true;
                pair.roleTimer = PeelSeconds;
                pair.friend.SetRole(DuelRole.Peel, null);
                pair.friend.SetDepth(_playZ + Depth, PeelSeconds);

                if (!_fight || pair.foe != null) continue;
                SpawnBackFoe(pair, BackFoeEntry(pair), _flight);
                if (pair.foe != null) pair.foe.SetRole(DuelRole.Hunt, pair.friend);
            }
        }

        void SpawnBackFoe(BackPair pair, Vector3 at, PlayerConfig playerFlight)
        {
            pair.foe = DuelPlane.Spawn("Background Foe", _foePlane, playerFlight, at, Mathf.PI,
                mirrored: true, PlaneSkins.RandomEnemy(_foePlane, _level.enemySkin));
            if (pair.foe != null) pair.foe.SetGround(_groundY);
        }

        void BeginBackDuel(BackPair pair)
        {
            pair.peeling = false;
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
                if (pair.friend == null) continue;
                if (pair.foe == null)
                {
                    if (!pair.peeling) continue;

                    pair.roleTimer -= dt;
                    if (pair.roleTimer > 0f) continue;

                    pair.peeling = false;
                    pair.friend.SetRole(DuelRole.Cruise, null);
                    continue;
                }

                pair.roleTimer -= dt;
                if (pair.roleTimer > 0f) continue;

                if (pair.peeling)
                {
                    BeginBackDuel(pair);
                }
                else if (pair.crossing)
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
            : Slot(0);

        float EscortFloorY => _groundY + EscortFloorMargin + _lift;

        float EscortCeilingY => _worldTop - EscortCeilingMargin - _lift;

        static float Span(PlaneModelConfig plane) => plane != null ? plane.OnScreenSize : 0f;

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
            if (_standDown || _slaved) return;

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
            foreach (Doomed doomed in _doomed)
                if (doomed.foe != null) doomed.foe.SetView(minX, maxX, minY, maxY);

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

            _floorY = Mathf.Max(_groundY + DuelFloorMargin + _lift,
                _camY - _halfHeight * BandVertical);
            _ceilingY = Mathf.Min(_worldTop - DuelCeilingMargin - _lift,
                _camY + _halfHeight * BandVertical);
            if (_floorY > _ceilingY) _floorY = _ceilingY = (_floorY + _ceilingY) * 0.5f;

            if (_companion != null)
            {
                if (_support)
                {
                    float play = _halfView / _depthScale;
                    float behind = _facing < 0f ? BandAhead : BandBehind;
                    float ahead = _facing < 0f ? BandBehind : BandAhead;
                    _companion.SetBounds(_camX - play * behind, _camX + play * ahead,
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
            float floor = _flight != null ? Mathf.Clamp01(_flight.energyMinFraction) : 1f;
            float pace = Mathf.Clamp(_camSpeed, DuelPlane.CruiseSpeed * floor,
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

        public bool Formed
        {
            get
            {
                if (_standDown || _slaved || _companion == null) return true;
                if (_phase != Phase.Escort && _phase != Phase.Peel && _phase != Phase.Duel)
                    return false;
                return !_parade || BacksFormed;
            }
        }

        bool BacksFormed
        {
            get
            {
                foreach (BackPair pair in _backs)
                    if (pair.friend != null && !pair.formed) return false;
                return true;
            }
        }

        public bool Regrouped
        {
            get
            {
                if (_standDown || _slaved || _companion == null) return true;
                if (!_regrouping) return false;
                if (_regroupTime >= FormLimit) return true;
                if (_regroupTime < CloseSeconds) return false;
                if (!_support && _phase != Phase.Escort) return false;
                if (!_companion.AtStation) return false;
                return BacksFormed;
            }
        }

        public void Regroup()
        {
            if (_standDown || _companion == null || _regrouping) return;

            _regrouping = true;
            _regroupTime = 0f;
            _flyIn = true;

            if (_support)
            {
                _companion.SetMark(null);
                _companion.SetEscort(_player, Slot(0));
                _companion.SetEscortBounds(EscortFloorY, EscortCeilingY);
                _companion.SetRole(DuelRole.Escort, null);
            }

            for (int i = 0; i < _backs.Count; i++)
            {
                Gather(_backs[i], i);
                if (_backs[i].friend != null) _backs[i].friend.SetDepth(_playZ, CloseSeconds);
            }
        }

        void Parade()
        {
            _parade = true;
            _paradeTime = 0f;

            BeginAlign();
            for (int i = 0; i < _backs.Count; i++) Gather(_backs[i], i);
        }

        void Gather(BackPair pair, int index)
        {
            DropBackFoe(pair);
            if (pair.friend == null) return;

            pair.peeling = false;
            pair.crossing = false;
            pair.formed = false;
            pair.friend.SetEscort(_player, Slot(index + 1));
            pair.friend.SetEscortBounds(EscortFloorY, EscortCeilingY);
            pair.friend.SetRole(DuelRole.Form, null);
        }

        void DropBackFoe(BackPair pair)
        {
            DuelPlane foe = pair.foe;
            if (foe == null) return;

            pair.foe = null;
            Doom(foe, 0f, () => { if (foe != null) pair.wreck = foe; });
        }

        bool InView(Vector3 pos) =>
            Mathf.Abs(pos.x - _camX) <= _halfView && Mathf.Abs(pos.y - _camY) <= _halfHeight;

        void TickRegroup(float dt)
        {
            _regroupTime += dt;
            if (_regroupTime >= CloseSeconds) SettleBacks(_regroupTime);
        }

        void TickParade(float dt)
        {
            _paradeTime += dt;
            SettleBacks(_paradeTime);
        }

        void SettleBacks(float elapsed)
        {
            foreach (BackPair pair in _backs)
            {
                if (pair.friend == null || pair.formed) continue;
                if (!pair.friend.AtStation && elapsed < FormLimit) continue;

                pair.friend.FlipUpright();
                if (pair.friend.Rolling && elapsed < FormLimit) continue;

                pair.formed = true;
                pair.friend.SetRole(DuelRole.Escort, null);
            }
        }

        public void SetCinematic(bool value)
        {
            if (value == _cinematic) return;
            _cinematic = value;
            if (_standDown || _slaved || _companion == null) return;

            if (!value && (_flyIn || _parade)) BreakFormation();

            if (_support)
            {
                _companion.SetMark(null);
                if (value && !_flyIn && _level.companionForms)
                {
                    Parade();
                    return;
                }

                _phase = Phase.Escort;
                _companion.SetRole(value ? DuelRole.Escort : DuelRole.Support, null);
                return;
            }

            if (value)
            {
                if (_level.companionForms && (_phase == Phase.Peel || _phase == Phase.Duel))
                    BeginAlign();
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
            TickDoomed(dt);
            if (_standDown || _slaved || _companion == null) return;

            TickBack(dt);
            if (_regrouping) TickRegroup(dt);
            if (_parade) TickParade(dt);

            if (_support && _phase == Phase.Escort)
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
                    if (!_companion.AtStation && _phaseTimer > 0f) return;

                    if (_support) Formate();
                    else BeginClose();
                    return;

                case Phase.Close:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) Formate();
                    return;
            }
        }

        public bool CheckBump(Transform playerTr)
        {
            if (_companion == null || playerTr == null || _standDown || _slaved) return false;
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

                DropBackFoe(pair);

                SpawnBackFoe(pair, BackFoeEntry(pair), _flight);
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

        void BreakFormation()
        {
            _flyIn = false;
            _regrouping = false;
            _parade = false;
            _companion.SetEscort(_player, Station);
            PeelBacks();
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

            DropFoe();

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

            _companion.SetEscort(_player, Slot(0));
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

            _companion.SetEscort(_player, Slot(0));
            _companion.SetRole(DuelRole.Escort, null);
        }

        void SpawnFoe()
        {
            if (_foe != null || !_fight) return;

            float y = Mathf.Clamp(_player.position.y + Random.Range(-FoeEntryRise, FoeEntryRise),
                _floorY, _ceilingY);
            var entry = new Vector3(_camX + _halfView + FoeEntryMargin, y, _playZ + Depth);

            _foe = DuelPlane.Spawn("Background Foe", _foePlane, _flight, entry,
                Mathf.PI, mirrored: true, PlaneSkins.RandomEnemy(_foePlane, _level.enemySkin));
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
            if (_foe == null) _companion.SetRole(DuelRole.Cruise, null);
            else ApplyRoles();
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
