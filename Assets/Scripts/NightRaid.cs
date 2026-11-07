using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class NightRaid
    {
        const float PatrolBeamGap = 40f;
        const float GunBeamGap = 40f;
        const float FormationFloor = 70f;
        const float ColumnGap = 50f;
        const float DiagonalGap = 317f;
        const float PinchGap = 345f;
        const float BehindPace = 2.5f;
        const float BehindQuietSec = 2f;
        const int ShapesPerTier = 3;
        const int Tiers = 3;
        static readonly int[] TierShapes = { 3, 6, 8 };
        const int WeaveShape = 6;
        const int BehindShape = 7;
        const float InViewShare = 0.8f;
        const float CallInSec = 4f;
        const int GunSeedStep = 101;

        readonly CampaignEnemies _enemies;
        readonly Transform _player;
        readonly CubeController _pilot;
        readonly PlaneShooter _shooter;
        readonly PlaneSearchlight _headlight;
        readonly CampaignTerrain _terrain;
        readonly StealthEye _eye;
        readonly float _playZ;
        readonly float _radius;
        readonly float _planeLength;
        readonly float _cruise;
        readonly float _groundY;
        readonly int _seed;
        readonly StealthMeter _meter = new StealthMeter();
        readonly List<PatrolLamp> _patrols = new List<PatrolLamp>();
        readonly List<EnemyController> _called = new List<EnemyController>();
        readonly List<int> _deck = new List<int>();

        static readonly EnemyGroup[] Lone = { new EnemyGroup(PlaneModels.Albatros, 1) };

        AaGun _gun;
        EnemyGroup[] _backup = Lone;
        EnemyGroup[] _hitBackup;
        EnemyGroup[] _owedGroups;
        bool _owed;
        bool _served;
        bool _on;
        bool _streaming;
        bool _lastBehind;
        int _tiers = 1;
        int _next = -1;
        int _deckTiers;
        int _lastShape = -1;
        float _clearFor;
        int _guns;
        int _formations;
        float _camX, _halfWidth;

        public NightRaid(CampaignEnemies enemies, Transform player, PlaneShooter shooter,
            PlaneSearchlight headlight, CampaignTerrain terrain, Transform hud, float playZ,
            float radius, float planeLength, float cruise, float groundY, int seed)
        {
            _enemies = enemies;
            _player = player;
            _pilot = player != null ? player.GetComponent<CubeController>() : null;
            _shooter = shooter;
            _headlight = headlight;
            _terrain = terrain;
            _playZ = playZ;
            _radius = radius;
            _planeLength = planeLength;
            _cruise = cruise;
            _groundY = groundY;
            _seed = seed;
            if (hud != null) _eye = new StealthEye(hud);
        }

        public bool Live => _on && (_gun == null || !_gun.Down);

        public bool GunDown => _gun == null || _gun.Down;

        public bool GunInView => _gun != null && _gun.X - _camX <= _halfWidth * InViewShare;

        public float GunX => _gun != null ? _gun.X : _camX;

        public bool Alarmed => _meter.Locked || Hunted();

        bool Hunted()
        {
            if (_enemies == null) return false;

            foreach (EnemyController enemy in _enemies.Live)
                if (enemy != null && enemy.IsAlive && !Quiet(enemy)) return true;
            return false;
        }

        bool Quiet(EnemyController enemy)
        {
            foreach (PatrolLamp patrol in _patrols)
                if (patrol != null && patrol.Enemy == enemy) return !patrol.Aware;
            return false;
        }

        public void SetBackup(EnemyGroup[] groups)
        {
            _backup = groups != null && groups.Length > 0 ? groups : Lone;
        }

        public void SetOn(bool on)
        {
            _on = on;
            if (on) return;

            _streaming = false;
            _meter.Clear();
            if (_gun != null) _gun.Track(false);
        }

        public void SetPatrols(bool on, int tiers)
        {
            _streaming = on;
            _tiers = Mathf.Clamp(tiers, 1, Tiers);
            if (_next >= 0 && _deckTiers == _tiers) _deck.Add(_next);
            _next = -1;
        }

        int Draw()
        {
            if (_deckTiers != _tiers)
            {
                _deck.Clear();
                _deckTiers = _tiers;
            }
            if (_deck.Count == 0) Shuffle(TierShapes[_tiers - 1]);

            int last = _deck.Count - 1;
            int shape = _deck[last];
            _deck.RemoveAt(last);
            return shape;
        }

        void Shuffle(int count)
        {
            for (int i = 0; i < count; i++) _deck.Add(i);
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
            }

            int first = _deck.Count - 1;
            if (first > 0 && _deck[first] == _lastShape) (_deck[first], _deck[0]) = (_deck[0], _deck[first]);
        }

        bool Passed()
        {
            foreach (PatrolLamp patrol in _patrols)
            {
                EnemyController enemy = patrol.Enemy;
                if (enemy == null || !enemy.IsAlive) continue;

                float x = enemy.transform.position.x;
                if (patrol.Course < 0f ? x >= _camX - _halfWidth : x <= _camX + _halfWidth) return false;
            }
            return true;
        }

        void Stream(float dt)
        {
            if (!Passed())
            {
                _clearFor = 0f;
                return;
            }

            if (_next < 0) _next = Draw();
            _clearFor += dt;
            if ((_lastBehind || _next == BehindShape) && _clearFor < BehindQuietSec) return;

            SpawnFormation(_next);
            _next = -1;
            _clearFor = 0f;
        }

        void SpawnFormation(int shape)
        {
            if (_enemies == null) return;

            float top = _enemies.CeilingOf(PlaneModels.Albatros) - PatrolLamp.BobHeight;
            float bottom = _groundY + FormationFloor;
            if (top < bottom) top = bottom;
            float middle = (top + bottom) * 0.5f;

            _formations++;
            _lastShape = shape;
            _lastBehind = shape == BehindShape;
            float reach = _halfWidth + PatrolLamp.LampReach + PatrolBeamGap;
            float x = _lastBehind ? _camX - reach : _camX + reach;
            float phase = Random.value * Mathf.PI * 2f;
            foreach (Vector2 slot in Shape(shape, top, bottom))
            {
                var at = new Vector2(_lastBehind ? x - slot.x : x + slot.x, slot.y);
                EnemyController enemy = _enemies.SpawnAt(PlaneModels.Albatros, at);
                PatrolLamp patrol = PatrolLamp.Mount(enemy, _cruise, _halfWidth, Lost, _formations, phase);
                if (patrol == null) continue;

                if (_lastBehind) patrol.Overtake(_cruise * BehindPace);
                else if (shape == WeaveShape) patrol.Weave(slot.y > middle ? -1f : 1f, bottom, top);
                _patrols.Add(patrol);
            }
        }

        static Vector2[] Shape(int shape, float top, float bottom)
        {
            float middle = (top + bottom) * 0.5f;
            float step = (top - bottom) / 3f;
            switch (shape)
            {
                case 0: return Column(top, top - ColumnGap, top - ColumnGap * 2f);
                case 1: return Column(bottom, bottom + ColumnGap, bottom + ColumnGap * 2f);
                case 3: return Diagonal(top, -step);
                case 4: return Diagonal(bottom, step);
                case 5:
                    return new[]
                    {
                        new Vector2(0f, top), new Vector2(0f, bottom),
                        new Vector2(PinchGap, middle + ColumnGap * 0.5f),
                        new Vector2(PinchGap, middle - ColumnGap * 0.5f),
                    };
                case WeaveShape:
                    float lag = (top - bottom) / Mathf.Tan(PatrolLamp.WeaveSlopeDeg * Mathf.Deg2Rad) * 0.5f;
                    return new[] { new Vector2(0f, top), new Vector2(lag, bottom) };
                case BehindShape: return Shape(Random.Range(0, ShapesPerTier), top, bottom);
                default: return Column(top, middle, bottom);
            }
        }

        static Vector2[] Column(params float[] heights)
        {
            var slots = new Vector2[heights.Length];
            for (int i = 0; i < heights.Length; i++) slots[i] = new Vector2(0f, heights[i]);
            return slots;
        }

        static Vector2[] Diagonal(float from, float step)
        {
            var slots = new Vector2[4];
            for (int i = 0; i < slots.Length; i++) slots[i] = new Vector2(DiagonalGap * i, from + step * i);
            return slots;
        }

        void Lost(EnemyController enemy)
        {
            if (_enemies != null) _enemies.Dismiss(enemy);
        }

        public void PlaceGun(EnemyGroup[] hit)
        {
            _hitBackup = hit != null && hit.Length > 0 ? hit : null;
            _guns++;
            float x = _camX + _halfWidth + AaGun.BeamSpan + GunBeamGap;
            float spot = AaGun.ClearSpot(_terrain, x, _playZ);
            _gun = AaGun.Place(spot, _playZ, _player, _cruise, _planeLength, _seed + _guns * GunSeedStep);
        }

        public void ForgetGun()
        {
            _gun = null;
            _hitBackup = null;
            _meter.Clear();
        }

        public void BlowGun()
        {
            if (_gun != null) _gun.Blow();
        }

        public void OnBomb(Vector3 point, float radius)
        {
            if (_gun == null || !_gun.InBlast(point, radius)) return;

            if (_gun.Hurt)
            {
                _gun.Blow();
                PilotProfile.Score(PilotRanks.AaGunPoints);
                return;
            }

            _gun.Damage();
            if (!_meter.Locked) _meter.Alarm();
            Call(_hitBackup);
        }

        public void Tick(float dt, Vector3 cam, float halfWidth, bool quiet)
        {
            _camX = cam.x;
            _halfWidth = halfWidth;
            _patrols.RemoveAll(patrol => patrol == null);

            bool live = Live && !quiet && _player != null;
            if (live) Watch(dt);
            else
            {
                if (_gun != null) _gun.Track(false);
                if (!quiet) _meter.Clear();
                if (!_meter.Locked) Forgive();
                foreach (PatrolLamp patrol in _patrols) patrol.ConsumeHit();
            }

            if (live && _streaming && !Alarmed) Stream(dt);
            else _clearFor = 0f;

            if (_eye != null)
                _eye.Set(_meter.Level, _meter.Locked, live && (_meter.Level > 0.001f || _meter.Touched));
        }

        void Watch(float dt)
        {
            Vector3 at = _player.position;
            bool beam = _gun != null && _gun.Sees(at, _radius);

            bool spotted = false;
            bool alarm = false;
            int cause = 0;
            foreach (PatrolLamp patrol in _patrols)
            {
                if (patrol.ConsumeHit() || Rammed(patrol))
                {
                    alarm = true;
                    cause = patrol.Formation;
                }
                if (patrol.Sees(at, _radius))
                {
                    spotted = true;
                    if (cause == 0) cause = patrol.Formation;
                }
            }

            bool headlight = _headlight != null && _headlight.IsOn;
            bool firing = _shooter != null && _shooter.Firing;
            _meter.Tick(dt, beam || spotted, headlight, firing);
            if (alarm) _meter.Alarm();

            if (!_meter.Locked)
            {
                Forgive();
                if (_gun != null) _gun.Track(false);
                return;
            }

            if (_gun != null) _gun.Track(true);
            if (Scramble(cause))
            {
                _owed = false;
                _owedGroups = null;
                _served = true;
            }
            else if (alarm || _meter.LockedFor >= CallInSec) Call();
            Serve();
        }

        bool Scramble(int formation)
        {
            if (formation == 0)
                foreach (PatrolLamp patrol in _patrols)
                    if (patrol.Enemy != null && patrol.Enemy.IsAlive)
                        formation = Mathf.Max(formation, patrol.Formation);
            if (formation == 0) return false;

            foreach (PatrolLamp patrol in _patrols)
                if (patrol.Formation == formation) patrol.Alert();
            return true;
        }

        bool Rammed(PatrolLamp patrol)
        {
            EnemyController enemy = patrol.Enemy;
            Collider hitbox = enemy != null && enemy.IsAlive ? enemy.Hitbox : null;
            if (hitbox == null || !hitbox.enabled) return false;

            Vector3 at = _player.position;
            if ((hitbox.ClosestPoint(at) - at).sqrMagnitude > _radius * _radius) return false;

            patrol.Alert();
            if (_pilot != null) _pilot.Scrape();
            enemy.MarkStrike(true);
            enemy.Scrape();
            return true;
        }

        void Call(EnemyGroup[] groups = null)
        {
            if (!_served)
            {
                _owed = true;
                if (groups != null) _owedGroups = groups;
            }
            Serve();
        }

        void Serve()
        {
            _called.RemoveAll(plane => plane == null || !plane.IsAlive);
            if (!_owed || _called.Count > 0 || _enemies == null) return;

            EnemyGroup[] groups = _owedGroups ?? _backup;
            _owed = false;
            _owedGroups = null;
            _served = true;
            _called.AddRange(_enemies.Spawn(groups, _camX, _halfWidth));
        }

        void Forgive()
        {
            _owed = false;
            _owedGroups = null;
            _served = false;
        }
    }
}
