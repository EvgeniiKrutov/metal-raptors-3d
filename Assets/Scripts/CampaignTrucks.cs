using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignTrucks
    {
        const float SpawnMargin = 140f;
        const float QueueGap = 1.6f;

        readonly List<EnemyTruck> _live = new List<EnemyTruck>();
        readonly Rigidbody _player;
        readonly CampaignTerrain _land;
        readonly float _roadZ;
        readonly float _roadLift;

        public IReadOnlyList<EnemyTruck> Live => _live;

        public int AliveCount
        {
            get
            {
                for (int i = _live.Count - 1; i >= 0; i--)
                    if (_live[i] == null) _live.RemoveAt(i);
                return _live.Count;
            }
        }

        public CampaignTrucks(Rigidbody player, CampaignTerrain land, float roadZ, float roadLift)
        {
            _player = player;
            _land = land;
            _roadZ = roadZ;
            _roadLift = roadLift;
        }

        public void Spawn(int count, float rightEdgeX)
        {
            if (count <= 0 || !EnemyTruck.Measure()) return;

            float spawnX = rightEdgeX + SpawnMargin;
            float queue = EnemyTruck.BodyLength * QueueGap;
            float stopX = Airfield.Current != null
                ? Airfield.Current.FenceX + EnemyTruck.FenceStandoff + EnemyTruck.BodyLength * 0.5f
                : float.NegativeInfinity;

            for (int i = 0; i < count; i++)
            {
                var point = new Vector3(spawnX + i * queue, ProceduralTerrain.BaseLevel, _roadZ);
                EnemyTruck truck = EnemyTruck.Spawn(point, stopX + i * queue, _roadLift, _player,
                    _land);
                if (truck == null) continue;

                truck.OnDestroyed += Remove;
                _live.Add(truck);
            }
        }

        public void StandDown()
        {
            foreach (EnemyTruck truck in _live)
                if (truck != null) truck.StandDown();
        }

        void Remove(EnemyTruck truck) => _live.Remove(truck);
    }
}
