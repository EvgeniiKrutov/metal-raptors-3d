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

        const float BandFactor = 1.25f;
        const float FoeEntryFactor = 1.75f;
        const float FoeEntryMargin = 100f;
        const float FoeEntryRise = 70f;

        const float DuelFloorMargin = 140f;
        const float DuelCeilingMargin = 110f;
        const float EscortFloorMargin = 60f;
        const float EscortCeilingMargin = 40f;

        const float HuntMin = 6f;
        const float HuntMax = 9f;
        const float CrossSeconds = 1.6f;

        const float BumpReach = PlaneScrapes.HitboxRadius * 2f;
        const float BumpCooldown = 0.5f;
        const float BumpDepthBand = 40f;

        enum Phase { Escort, Peel, Duel, Align, Roll, Form, Close }

        readonly CampaignDefinition _level;
        readonly PlayerConfig _flight;
        readonly Transform _player;
        readonly float _playZ;
        readonly float _groundY;
        readonly float _worldTop;

        DuelPlane _companion;
        DuelPlane _foe;

        Phase _phase = Phase.Escort;
        bool _cinematic = true;
        bool _standDown;
        float _phaseTimer;
        float _roleTimer;
        bool _crossing;
        bool _companionHunts = true;
        float _bumpTime = -999f;

        float _camX;
        float _halfView;

        CompanionFlight(CampaignDefinition level, PlayerConfig playerFlight, Transform player,
            float playZ, float groundY, float worldTop)
        {
            _level = level;
            _flight = playerFlight;
            _player = player;
            _playZ = playZ;
            _groundY = groundY;
            _worldTop = worldTop;
        }

        public static CompanionFlight Begin(CampaignDefinition level, PlayerConfig playerFlight,
            Transform player, float playZ, float groundY, float worldTop)
        {
            if (level == null || !level.companion || player == null) return null;

            var flight = new CompanionFlight(level, playerFlight, player, playZ, groundY, worldTop);

            Vector3 station = player.position + Station;
            station.z = playZ;
            flight._companion = DuelPlane.Spawn("Companion", level.companionPlane, playerFlight,
                station, 0f, mirrored: false);

            if (flight._companion == null) return null;

            flight._companion.SetEscort(player, Station);
            flight._companion.SetEscortBounds(flight.EscortFloorY, flight.EscortCeilingY);
            flight._companion.SetRole(DuelRole.Escort, null);
            return flight;
        }

        static Vector3 Station => new Vector3(StationAhead, StationAbove, 0f);

        float FloorY => _groundY + DuelFloorMargin;

        float CeilingY => _worldTop - DuelCeilingMargin;

        float EscortFloorY => _groundY + EscortFloorMargin;

        float EscortCeilingY => _worldTop - EscortCeilingMargin;

        public void SetWindow(float camX, float halfViewWidth)
        {
            _camX = camX;
            _halfView = halfViewWidth;
            if (_standDown) return;

            float minX = camX - halfViewWidth * BandFactor;
            float maxX = camX + halfViewWidth * BandFactor;

            if (_companion != null) _companion.SetBounds(minX, maxX, FloorY, CeilingY);
            if (_foe != null) _foe.SetBounds(minX, maxX, FloorY, CeilingY);
        }

        public bool Formed => _standDown || _companion == null
            || _phase == Phase.Escort || _phase == Phase.Peel || _phase == Phase.Duel;

        public void SetCinematic(bool value)
        {
            if (value == _cinematic) return;
            _cinematic = value;
            if (_standDown || _companion == null) return;

            if (value)
            {
                if (_phase == Phase.Peel || _phase == Phase.Duel) BeginAlign();
            }
            else if (_phase != Phase.Peel && _phase != Phase.Duel)
            {
                Peel();
            }
        }

        public void Tick(float dt)
        {
            if (_standDown || _companion == null) return;

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

        public void StandDown()
        {
            _standDown = true;
            if (_companion != null) _companion.StandDown();
            if (_foe != null) _foe.StandDown();
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
                FloorY, CeilingY);
            var entry = new Vector3(_camX + _halfView * FoeEntryFactor + FoeEntryMargin, y,
                _playZ + Depth);

            _foe = DuelPlane.Spawn("Background Foe", _level.companionFoe, _flight, entry,
                Mathf.PI, mirrored: true);
            if (_foe == null) return;

            _foe.SetBounds(_camX - _halfView * BandFactor, _camX + _halfView * BandFactor,
                FloorY, CeilingY);
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
