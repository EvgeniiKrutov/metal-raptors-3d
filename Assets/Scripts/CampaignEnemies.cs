using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignEnemies
    {
        const float SpawnAhead = 110f;
        const float SpawnStagger = 90f;
        const float WindowMargin = 70f;
        const float EdgeMargin = 90f;
        const float CeilingPad = 160f;
        const float LeashScreens = 2f;

        const float SpreadStagger = 160f;
        const float SpreadTopFrom = -0.6f, SpreadTopTo = 0.9f;
        const float SpreadSideLow = 0.75f, SpreadSideHigh = 0.85f;
        const float SpreadAheadGap = 120f;

        enum SpawnEdge { Right, Top }

        static readonly SpawnEdge[] Edges = { SpawnEdge.Right, SpawnEdge.Top };

        readonly List<EnemyController> _live = new List<EnemyController>();
        readonly Dictionary<EnemyController, PlaneModelConfig> _planes =
            new Dictionary<EnemyController, PlaneModelConfig>();
        readonly EnemyConfig _scout;
        readonly EnemyConfig _fighter;
        readonly Rigidbody _player;
        readonly float _groundY;
        readonly float _worldTop;

        float _minX;
        float _maxX;

        public float ModelScale { get; set; } = 1f;

        public int AliveCount => _live.Count;

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
            return count;
        }

        public CampaignEnemies(Rigidbody player, float groundY, float worldTop,
            CampaignDefinition level)
        {
            _player = player;
            _groundY = groundY;
            _worldTop = worldTop;

            _scout = EnemyConfigs.Load(EnemyRole.Scout);
            _fighter = EnemyConfigs.Load(EnemyRole.Fighter);

            if (level == null) return;
            EnemyConfigs.Scale(_scout, level.enemyHealthScale, level.enemyRotationScale);
            EnemyConfigs.Scale(_fighter, level.enemyHealthScale, level.enemyRotationScale);
        }

        public void SetWindow(float camX, float halfViewWidth)
        {
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
                _live[i].SetBounds(_minX, _maxX);
                if (_live[i].transform.position.x < leash) _live[i].Reappear(ahead);
            }
        }

        public void Spawn(EnemyGroup[] groups, float camX, float halfViewWidth)
        {
            if (groups == null) return;

            SetWindow(camX, halfViewWidth);

            int index = 0;
            foreach (EnemyGroup group in groups)
            {
                if (group.kind != EnemyKind.Plane) continue;

                for (int i = 0; i < group.count; i++, index++)
                {
                    EnemyConfig config = EnemyConfigs.For(group.plane, _scout, _fighter);
                    SpawnOne(group.plane, SpawnPoint(camX, halfViewWidth, index, config,
                        CeilingFor(group.plane)));
                }
            }
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

                    EnemyConfig config = EnemyConfigs.For(group.plane, _scout, _fighter);
                    SpawnOne(group.plane, SpreadPoint(camPos, halfViewWidth, halfViewHeight, edge,
                        config, CeilingFor(group.plane)));
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
            SpawnEdge edge, EnemyConfig config, float ceilingY)
        {
            EnemyConfigs.SpawnBand(config, _groundY, ceilingY, out float minY, out float maxY);
            maxY = Mathf.Max(minY, Mathf.Min(maxY, _worldTop - CeilingPad));

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
                    return new Vector3(Random.Range(from, to), y, z);

                if (y > ceilingY) low = high;
            }

            return new Vector3(camPos.x + halfViewWidth + SpawnAhead + extra,
                Random.Range(low, high), z);
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
            config.health = Mathf.Max(1f,
                EnemyConfigs.For(plane, _scout, _fighter).health * Mathf.Max(0.01f, healthScale));
            if (flySpeed > 0f) config.flySpeed = flySpeed;

            float z = _player != null ? _player.position.z : 0f;
            return SpawnOne(plane, new Vector3(position.x, position.y, z), config, skin);
        }

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
            Object.Destroy(enemy.gameObject);
        }

        float CeilingFor(PlaneModelConfig plane) =>
            _worldTop - plane.OnScreenSize * ModelScale / 2f;

        EnemyController SpawnOne(PlaneModelConfig plane, Vector3 position,
            EnemyConfig config = null, PlaneSkin skin = null)
        {
            if (config == null) config = EnemyConfigs.For(plane, _scout, _fighter);

            var go = new GameObject("Enemy");
            go.transform.position = position;
            Transform model = PlaneFactory.BuildPlaneModel(go.transform, plane, mirrored: true,
                skin: skin ?? PlaneSkins.Default(plane));
            if (!Mathf.Approximately(ModelScale, 1f)) model.localScale *= ModelScale;

            bool gunned = plane.gunLift > 0f;
            Vector3 gun = gunned
                ? PlaneFactory.GunLocal(go, model, plane, ModelScale, mirrored: true)
                : Vector3.zero;

            Vector3 nose = PlaneFactory.NoseLocal(go, model, plane);

            var enemy = go.AddComponent<EnemyController>();
            enemy.Initialize(config, _player, _minX, _maxX, _groundY, CeilingFor(plane),
                EdgeMargin);
            if (gunned) enemy.MountGun(gun);
            enemy.MountNose(nose);
            enemy.OnDestroyed += OnDestroyed;
            _live.Add(enemy);
            _planes[enemy] = plane;
            return enemy;
        }

        public void StandDown()
        {
            foreach (EnemyController enemy in _live)
                if (enemy != null) enemy.StandDown();
        }

        Vector3 SpawnPoint(float camX, float halfViewWidth, int index, EnemyConfig config,
            float ceilingY)
        {
            EnemyConfigs.SpawnBand(config, _groundY, ceilingY, out float minY, out float maxY);
            maxY = Mathf.Max(minY, Mathf.Min(maxY, _worldTop - CeilingPad));

            float z = _player != null ? _player.position.z : 0f;

            return new Vector3(camX + halfViewWidth + SpawnAhead + index * SpawnStagger,
                Random.Range(minY, maxY), z);
        }

        void OnDestroyed(EnemyController enemy)
        {
            _live.Remove(enemy);
            _planes.Remove(enemy);
        }
    }
}
