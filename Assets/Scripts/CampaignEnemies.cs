using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignEnemies
    {
        const float SpawnAhead = 110f;
        const float SpawnStagger = 90f;
        const float SpawnGapShare = 1.25f;
        const int ApartTries = 8, ApartRolls = 4;
        const float WindowMargin = 70f;
        const float EdgeMargin = 90f;
        const float CeilingPad = 160f;
        const float LeashScreens = 2f;

        const float SpreadStagger = 160f;
        const float SpreadTopFrom = -0.6f, SpreadTopTo = 0.9f;
        const float SpreadSideLow = 0.75f, SpreadSideHigh = 0.85f;
        const float SpreadAheadGap = 120f;

        const float BehindSpacing = 70f;
        const int PursuitCap = 2;
        const float ReinforceMinSec = 2f, ReinforceMaxSec = 3f;

        struct Reserve
        {
            public PlaneModelConfig plane;
            public bool sneak, high;
        }

        enum SpawnEdge { Right, Top }

        static readonly SpawnEdge[] Edges = { SpawnEdge.Right, SpawnEdge.Top };

        readonly List<EnemyController> _live = new List<EnemyController>();
        readonly Dictionary<(EnemyConfig, PlaneModelConfig), EnemyConfig> _lifted =
            new Dictionary<(EnemyConfig, PlaneModelConfig), EnemyConfig>();
        readonly Dictionary<EnemyController, PlaneModelConfig> _planes =
            new Dictionary<EnemyController, PlaneModelConfig>();
        readonly Queue<Reserve> _reserve = new Queue<Reserve>();
        readonly HashSet<EnemyController> _leaving = new HashSet<EnemyController>();
        readonly HashSet<EnemyController> _bosses = new HashSet<EnemyController>();
        readonly EnemyConfig _scout;
        readonly EnemyConfig _fighter;
        readonly Dictionary<PlaneModelConfig, EnemyConfig> _armed =
            new Dictionary<PlaneModelConfig, EnemyConfig>();
        readonly Dictionary<EnemyRole, EnemyConfig> _aces = new Dictionary<EnemyRole, EnemyConfig>();
        readonly Dictionary<EnemyConfig, EnemyConfig> _veterans =
            new Dictionary<EnemyConfig, EnemyConfig>();
        readonly bool _scoutFixed;
        readonly bool _fighterFixed;
        readonly Rigidbody _player;
        readonly float _groundY;
        readonly string _skin;
        readonly string[] _duelistSkins;

        float _minX;
        float _maxX;
        float _camX;
        float _halfWidth;
        float _reinforceIn = -1f;

        public float ModelScale { get; set; } = 1f;

        public float WorldTop { get; set; }

        public bool LiftClearance { get; set; }

        public bool Pursuit { get; set; }

        public bool BossFight { get; set; }

        public PlaneModelConfig PacedPlane { get; set; }

        public bool Duelists { get; set; }

        public float PursuitMinY { get; set; }

        public float PursuitMaxY { get; set; }

        public float PursuitMidY { get; set; }

        public float PursuitSpacing { get; set; }

        public float Facing { get; set; } = 1f;

        public event System.Action<EnemyController> Downed;

        public int AliveCount => _live.Count + _reserve.Count;

        public IReadOnlyList<EnemyController> Live => _live;

        public int CountAlive(PlaneModelConfig plane)
        {
            int count = 0;
            foreach (EnemyController enemy in _live)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                if (_planes.TryGetValue(enemy, out PlaneModelConfig model) && model == plane)
                    count++;
            }
            foreach (Reserve reserve in _reserve)
                if (reserve.plane == plane) count++;
            return count;
        }

        public CampaignEnemies(Rigidbody player, float groundY, float worldTop,
            CampaignDefinition level, float pace = 1f)
        {
            _player = player;
            _groundY = groundY;
            WorldTop = worldTop;

            _scout = EnemyConfigs.Load(EnemyRole.Scout, pace);
            _fighter = EnemyConfigs.Load(EnemyRole.Fighter, pace);

            if (level == null) return;
            _skin = level.enemySkin;
            _duelistSkins = level.duelistSkins;
            EnemyConfigs.Scale(_scout, level.enemyRotationScale);
            EnemyConfigs.Scale(_fighter, level.enemyRotationScale);
            _scoutFixed = level.scoutShotsPerSec > 0f;
            _fighterFixed = level.fighterShotsPerSec > 0f;
            if (_scoutFixed) _scout.fireRate = 1f / level.scoutShotsPerSec;
            if (level.scoutFireCone > 0f) _scout.fireAngleThreshold = level.scoutFireCone;
            if (_fighterFixed) _fighter.fireRate = 1f / level.fighterShotsPerSec;
        }

        EnemyConfig Base(PlaneModelConfig plane)
        {
            EnemyConfig config = EnemyConfigs.For(plane, _scout, _fighter);
            bool fixedRate = config == _scout ? _scoutFixed : _fighterFixed;
            return fixedRate ? config : EnemyConfigs.Armed(plane, config, _armed);
        }

        public void SetWindow(float camX, float halfViewWidth)
        {
            _camX = camX;
            _halfWidth = halfViewWidth;
            _minX = camX - halfViewWidth + WindowMargin;
            _maxX = camX + halfViewWidth - WindowMargin;

            float leash = camX - halfViewWidth * LeashScreens;
            float ahead = camX + halfViewWidth + SpawnAhead;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }
                if (_leaving.Contains(_live[i]))
                {
                    if (_live[i].IsAlive && Gone(_live[i].transform.position.x)) Dismiss(_live[i]);
                    continue;
                }
                _live[i].SetBounds(_minX, _maxX);
                if (_live[i].transform.position.x < leash && !EnemyRoles.IsAce(_live[i].Role))
                    _live[i].Reappear(ahead);
            }
        }

        bool Gone(float x) => (x - _camX) * Facing < -(_halfWidth + SpawnAhead);

        public void Retreat()
        {
            ClearReserve();
            foreach (EnemyController enemy in _live)
            {
                if (enemy == null || !enemy.IsAlive) continue;

                var pursuit = enemy.GetComponent<EnemyPursuit>();
                if (pursuit == null) continue;

                enemy.StandDown();
                pursuit.Leave();
                _leaving.Add(enemy);
            }
        }

        public List<EnemyController> Spawn(EnemyGroup[] groups, float camX, float halfViewWidth)
        {
            var spawned = new List<EnemyController>();
            if (groups == null) return spawned;

            SetWindow(camX, halfViewWidth);

            int index = 0;
            foreach (EnemyGroup group in groups)
            {
                if (group.kind != EnemyKind.Plane) continue;

                for (int i = 0; i < group.count; i++, index++)
                {
                    EnemyConfig config = Lifted(WaveConfig(group.plane), group.plane);
                    spawned.Add(SpawnOne(group.plane, SpawnPoint(camX, halfViewWidth, index, config,
                        CeilingFor(group.plane), Gap(group.plane)), config));
                }
            }
            return spawned;
        }

        public List<EnemyController> SpawnAces(EnemyRole role, float camX, float halfViewWidth)
        {
            var spawned = new List<EnemyController>();
            if (!EnemyRoles.IsAce(role)) return spawned;

            SetWindow(camX, halfViewWidth);

            PlaneModelConfig plane = PlaneModels.EnemyFor(role);
            EnemyConfig config = Lifted(AceConfig(role, plane), plane);
            spawned.Add(SpawnOne(plane, SpawnPoint(camX, halfViewWidth, 0, config,
                CeilingFor(plane), Gap(plane)), config));
            return spawned;
        }

        EnemyConfig WaveConfig(PlaneModelConfig plane) =>
            Duelists && plane == PlaneModels.EnemyFor(EnemyRole.Duelist)
                ? AceConfig(EnemyRole.Duelist, plane)
                : Base(plane);

        float PlayerTurnDeg
        {
            get
            {
                var pilot = _player != null ? _player.GetComponent<CubeController>() : null;
                return pilot != null ? pilot.BaseTurnDeg : 0f;
            }
        }

        EnemyConfig AceConfig(EnemyRole role, PlaneModelConfig plane)
        {
            if (_aces.TryGetValue(role, out EnemyConfig config) && config != null) return config;

            var pilot = _player != null ? _player.GetComponent<CubeController>() : null;
            config = EnemyConfigs.Ace(role, _fighter, _scout,
                pilot != null ? pilot.BaseSpeed : 0f, pilot != null ? pilot.BaseTurnDeg : 0f);
            if (!_fighterFixed) config.fireRate = EnemyConfigs.FireGap(plane, config.fireRate);

            _aces[role] = config;
            return config;
        }

        public void SpawnBehind(EnemyGroup[] groups, float camX, float halfViewWidth, float entryY)
        {
            if (groups == null) return;

            SetWindow(camX, halfViewWidth);

            int index = 0;
            foreach (EnemyGroup group in groups)
            {
                if (group.kind != EnemyKind.Plane) continue;

                for (int i = 0; i < group.count; i++)
                {
                    if (Pursuit && Pursuers >= PursuitCap)
                    {
                        _reserve.Enqueue(new Reserve
                            { plane = group.plane, sneak = group.sneak, high = group.high });
                        continue;
                    }

                    float side = index % 2 == 0 ? 1f : -1f;
                    float y = entryY + side * BehindSpacing * ((index + 1) / 2);
                    if (Pursuit)
                        y = EnemyPursuit.Apart(Mathf.Clamp(y, PursuitMinY, PursuitMaxY),
                            PursuitMinY, PursuitMaxY, PursuitSpacing);

                    SpawnOne(group.plane, BehindPoint(index, y, Gap(group.plane)), sneak: group.sneak,
                        high: group.high);
                    index++;
                }
            }
        }

        public void Reinforce(float dt)
        {
            if (_reserve.Count == 0) return;
            if (Pursuers >= PursuitCap)
            {
                _reinforceIn = -1f;
                return;
            }

            if (_reinforceIn < 0f) _reinforceIn = Random.Range(ReinforceMinSec, ReinforceMaxSec);
            _reinforceIn -= dt;
            if (_reinforceIn > 0f) return;

            _reinforceIn = -1f;
            Reserve next = _reserve.Dequeue();
            float low = next.high ? Mathf.Clamp(PursuitMidY, PursuitMinY, PursuitMaxY) : PursuitMinY;
            float y = EnemyPursuit.Apart(Random.Range(low, PursuitMaxY), PursuitMinY, PursuitMaxY,
                PursuitSpacing);
            SpawnOne(next.plane, BehindPoint(0, y, Gap(next.plane)), sneak: next.sneak, high: next.high);
        }

        int Pursuers
        {
            get
            {
                int count = 0;
                foreach (EnemyController enemy in _live)
                    if (enemy != null && enemy.IsAlive) count++;
                return count;
            }
        }

        Vector3 BehindPoint(int index, float y, float gap)
        {
            float z = _player != null ? _player.position.z : 0f;
            var at = new Vector3(_camX - Facing * (_halfWidth + SpawnAhead + index * SpawnStagger), y, z);
            return Apart(at, gap, y, y, false, new Vector2(-Facing, 0f));
        }

        float Gap(PlaneModelConfig plane) =>
            plane != null ? plane.OnScreenSize * ModelScale * SpawnGapShare : 0f;

        Vector3 Apart(Vector3 at, float gap, float rollFrom, float rollTo, bool rollX, Vector2 push)
        {
            if (gap <= 0f) return at;

            for (int i = 0; i < ApartTries; i++)
            {
                if (!Crowded(at, gap)) break;

                if (i < ApartRolls && rollTo > rollFrom)
                {
                    float value = Random.Range(rollFrom, rollTo);
                    if (rollX) at.x = value;
                    else at.y = value;
                }
                else
                {
                    at += (Vector3)(push * gap);
                }
            }
            return at;
        }

        bool Crowded(Vector3 at, float gap)
        {
            foreach (EnemyController enemy in _live)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                if (((Vector2)enemy.transform.position - (Vector2)at).sqrMagnitude < gap * gap) return true;
            }
            return false;
        }

        public void SpawnSpread(EnemyGroup[] groups, Vector3 camPos, float halfViewWidth,
            float halfViewHeight)
        {
            if (groups == null) return;

            SetWindow(camPos.x, halfViewWidth);

            var edges = new List<SpawnEdge>();
            foreach (EnemyGroup group in groups)
            {
                if (group.kind != EnemyKind.Plane) continue;

                for (int i = 0; i < group.count; i++)
                {
                    if (edges.Count == 0) Shuffle(edges);

                    SpawnEdge edge = edges[edges.Count - 1];
                    edges.RemoveAt(edges.Count - 1);

                    EnemyConfig config = Lifted(Base(group.plane),
                        group.plane);
                    SpawnOne(group.plane, SpreadPoint(camPos, halfViewWidth, halfViewHeight, edge,
                        config, CeilingFor(group.plane), Gap(group.plane)), config);
                }
            }
        }

        static void Shuffle(List<SpawnEdge> into)
        {
            into.AddRange(Edges);
            for (int i = into.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (into[i], into[j]) = (into[j], into[i]);
            }
        }

        Vector3 SpreadPoint(Vector3 camPos, float halfViewWidth, float halfViewHeight,
            SpawnEdge edge, EnemyConfig config, float ceilingY, float gap)
        {
            EnemyConfigs.SpawnBand(config, _groundY, ceilingY, out float minY, out float maxY);
            maxY = Mathf.Max(minY, Mathf.Min(maxY, WorldTop - CeilingPad));

            float z = _player != null ? _player.position.z : 0f;
            float extra = Random.Range(0f, SpreadStagger);

            float low = Mathf.Max(minY, camPos.y - halfViewHeight * SpreadSideLow);
            float high = Mathf.Min(maxY, camPos.y + halfViewHeight * SpreadSideHigh);
            if (low > high)
            {
                low = minY;
                high = maxY;
            }

            if (edge == SpawnEdge.Top)
            {
                float y = camPos.y + halfViewHeight + SpawnAhead + extra * 0.5f;
                float playerX = _player != null ? _player.position.x : camPos.x;
                float from = Mathf.Max(camPos.x + halfViewWidth * SpreadTopFrom,
                    playerX + SpreadAheadGap);
                float to = camPos.x + halfViewWidth * SpreadTopTo;

                if (y <= ceilingY && from <= to)
                    return Apart(new Vector3(Random.Range(from, to), y, z), gap, from, to, true, Vector2.up);

                if (y > ceilingY) low = high;
            }

            return Apart(new Vector3(camPos.x + halfViewWidth + SpawnAhead + extra,
                Random.Range(low, high), z), gap, low, high, false, Vector2.right);
        }

        public void Capture(List<PlaneSnapshot> into)
        {
            foreach (EnemyController enemy in _live)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                if (!_planes.TryGetValue(enemy, out PlaneModelConfig plane)) continue;

                into.Add(new PlaneSnapshot
                {
                    plane = plane,
                    position = enemy.transform.position,
                    heading = enemy.Heading,
                    health = enemy.CurrentHealth,
                });
            }
        }

        public void Restore(IReadOnlyList<PlaneSnapshot> saved, float camX, float halfViewWidth)
        {
            if (saved == null) return;

            SetWindow(camX, halfViewWidth);

            float z = _player != null ? _player.position.z : 0f;
            foreach (PlaneSnapshot snapshot in saved)
            {
                if (snapshot.plane == null) continue;

                var at = new Vector3(snapshot.position.x, snapshot.position.y, z);
                SpawnOne(snapshot.plane, at).Restore(snapshot.heading, snapshot.health);
            }
        }

        public EnemyController SpawnBoss(PlaneModelConfig plane, PlaneSkin skin, float healthScale,
            Vector2 position, float camX, float halfViewWidth, float flySpeed = 0f)
        {
            if (plane == null) return null;

            SetWindow(camX, halfViewWidth);

            EnemyConfig config = Object.Instantiate(_fighter);
            if (!_fighterFixed) config.fireRate = EnemyConfigs.FireGap(plane, config.fireRate);
            config.health = Mathf.Max(1f,
                EnemyConfigs.For(plane, _scout, _fighter).health * Mathf.Max(0.01f, healthScale));
            if (flySpeed > 0f) config.flySpeed = flySpeed;
            Raise(config, ClearanceLift(plane));

            float z = _player != null ? _player.position.z : 0f;
            EnemyController boss = SpawnOne(plane, new Vector3(position.x, position.y, z), config, skin,
                boss: true);
            _bosses.Add(boss);
            return boss;
        }

        public EnemyController SpawnAt(PlaneModelConfig plane, Vector2 position)
        {
            if (plane == null) return null;

            float z = _player != null ? _player.position.z : 0f;
            return SpawnOne(plane, new Vector3(position.x, position.y, z));
        }

        public float CeilingOf(PlaneModelConfig plane) => CeilingFor(plane);

        public EnemyController SpawnEscort(PlaneModelConfig plane, Vector2 position)
        {
            if (plane == null) return null;

            float z = _player != null ? _player.position.z : 0f;
            EnemyController escort = SpawnOne(plane, new Vector3(position.x, position.y, z));
            escort.HideHealthBar();
            escort.BeginScript();
            return escort;
        }

        public void Dismiss(EnemyController enemy)
        {
            if (enemy == null) return;

            _live.Remove(enemy);
            _planes.Remove(enemy);
            _leaving.Remove(enemy);
            _bosses.Remove(enemy);
            Object.Destroy(enemy.gameObject);
        }

        float CeilingFor(PlaneModelConfig plane) =>
            WorldTop - plane.OnScreenSize * ModelScale / 2f;

        float ClearanceLift(PlaneModelConfig plane) =>
            LiftClearance ? plane.OnScreenSize * 0.5f * Mathf.Max(0f, ModelScale - 1f) : 0f;

        EnemyConfig Lifted(EnemyConfig config, PlaneModelConfig plane)
        {
            float lift = ClearanceLift(plane);
            if (config == null || lift <= 0f) return config;

            var key = (config, plane);
            if (_lifted.TryGetValue(key, out EnemyConfig copy) && copy != null) return copy;

            copy = Object.Instantiate(config);
            Raise(copy, lift);
            _lifted[key] = copy;
            return copy;
        }

        static void Raise(EnemyConfig config, float lift)
        {
            config.minAltitudeMargin += lift;
            config.safeAltitudeMargin += lift;
            config.deckCeilingMargin += lift;
        }

        EnemyController SpawnOne(PlaneModelConfig plane, Vector3 position,
            EnemyConfig config = null, PlaneSkin skin = null, bool sneak = false, bool high = false,
            bool boss = false)
        {
            if (config == null) config = Lifted(Base(plane), plane);
            if (!Pursuit && !boss) config = EnemyConfigs.Veteran(plane, config, PlayerTurnDeg, _veterans);

            var go = new GameObject("Enemy");
            go.transform.position = position;
            PlaneSkin paint = skin ?? EnemySkin(plane);
            Transform model = PlaneFactory.BuildPlaneModel(go.transform, plane, mirrored: true,
                skin: paint);
            if (!Mathf.Approximately(ModelScale, 1f)) model.localScale *= ModelScale;
            WreckSource.Attach(go, model, plane, paint);

            bool gunned = PlaneFactory.GunMuzzleLocal(go, model, plane, out Vector3 gun);
            if (!gunned && plane.gunLift > 0f)
            {
                gunned = true;
                gun = PlaneFactory.GunLocal(go, model, plane, ModelScale, mirrored: true);
            }

            Vector3 nose = PlaneFactory.NoseLocal(go, model, plane);
            Vector3? engine = PlaneFactory.EngineLocal(go, model, plane);

            var enemy = go.AddComponent<EnemyController>();
            enemy.BossFight = BossFight;
            enemy.Initialize(config, _player, _minX, _maxX, _groundY, CeilingFor(plane),
                EdgeMargin);
            enemy.AssignPilot(plane.pilot);
            if (gunned) enemy.MountGun(gun);
            enemy.MountNose(nose);
            enemy.MountEngine(engine);
            if (plane == PacedPlane && !Pursuit) enemy.PaceWith(_player);
            enemy.OnDestroyed += OnDestroyed;
            _live.Add(enemy);
            _planes[enemy] = plane;
            if (Pursuit)
                EnemyPursuit.Begin(enemy, _player, new PursuitBand
                {
                    minY = PursuitMinY, maxY = PursuitMaxY, midY = PursuitMidY, spacing = PursuitSpacing,
                }, sneak, high, Facing);
            return enemy;
        }

        PlaneSkin EnemySkin(PlaneModelConfig plane) =>
            string.IsNullOrEmpty(_skin) && _duelistSkins != null
            && plane == PlaneModels.EnemyFor(EnemyRole.Duelist)
                ? PlaneSkins.RandomOf(plane, _duelistSkins)
                : PlaneSkins.RandomEnemy(plane, _skin);

        public void DismissAll()
        {
            ClearReserve();
            for (int i = _live.Count - 1; i >= 0; i--) Dismiss(_live[i]);
        }

        public void StandDown()
        {
            ClearReserve();
            foreach (EnemyController enemy in _live)
                if (enemy != null) enemy.StandDown();
        }

        void ClearReserve()
        {
            _reserve.Clear();
            _reinforceIn = -1f;
        }

        Vector3 SpawnPoint(float camX, float halfViewWidth, int index, EnemyConfig config,
            float ceilingY, float gap)
        {
            EnemyConfigs.SpawnBand(config, _groundY, ceilingY, out float minY, out float maxY);
            maxY = Mathf.Max(minY, Mathf.Min(maxY, WorldTop - CeilingPad));

            float z = _player != null ? _player.position.z : 0f;

            return Apart(new Vector3(camX + halfViewWidth + SpawnAhead + index * SpawnStagger,
                Random.Range(minY, maxY), z), gap, minY, maxY, false, Vector2.right);
        }

        void OnDestroyed(EnemyController enemy)
        {
            if (enemy.ByPlayer && !_bosses.Contains(enemy)
                && _planes.TryGetValue(enemy, out PlaneModelConfig plane))
                PilotProfile.Score(PilotRanks.PointsFor(plane));

            _live.Remove(enemy);
            _planes.Remove(enemy);
            _bosses.Remove(enemy);
            _leaving.Remove(enemy);
            Downed?.Invoke(enemy);
        }
    }
}
