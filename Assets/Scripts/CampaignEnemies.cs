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

        public int AliveCount => _live.Count;

        public IReadOnlyList<EnemyController> Live => _live;

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

        float CeilingFor(PlaneModelConfig plane) => _worldTop - plane.OnScreenSize / 2f;

        EnemyController SpawnOne(PlaneModelConfig plane, Vector3 position)
        {
            EnemyConfig config = EnemyConfigs.For(plane, _scout, _fighter);

            var go = new GameObject("Enemy");
            go.transform.position = position;
            PlaneFactory.BuildPlaneModel(go.transform, plane, mirrored: true,
                skin: PlaneSkins.Default(plane));

            var enemy = go.AddComponent<EnemyController>();
            enemy.Initialize(config, _player, _minX, _maxX, _groundY, CeilingFor(plane),
                EdgeMargin);
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
