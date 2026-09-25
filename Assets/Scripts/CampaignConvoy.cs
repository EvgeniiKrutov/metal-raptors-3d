using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignConvoy
    {
        const float SpawnMargin = 140f;

        readonly List<GroundVehicle> _live = new List<GroundVehicle>();
        readonly Rigidbody _player;
        readonly CampaignTerrain _land;
        readonly float _roadZ;
        readonly float _roadLift;

        public IReadOnlyList<GroundVehicle> Live => _live;

        public int AliveCount
        {
            get
            {
                for (int i = _live.Count - 1; i >= 0; i--)
                    if (_live[i] == null) _live.RemoveAt(i);
                return _live.Count;
            }
        }

        public CampaignConvoy(Rigidbody player, CampaignTerrain land, float roadZ, float roadLift)
        {
            _player = player;
            _land = land;
            _roadZ = roadZ;
            _roadLift = roadLift;
        }

        public void Spawn(EnemyKind kind, int count, float rightEdgeX)
        {
            if (count <= 0) return;

            bool tank = kind == EnemyKind.Tank;
            if (!(tank ? EnemyTank.Measure() : EnemyTruck.Measure())) return;

            float length = tank ? EnemyTank.BodyLength : EnemyTruck.BodyLength;
            if (length <= 0f) return;

            float queue = length + GroundVehicle.QueueGap;
            float spawnX = Mathf.Max(rightEdgeX + SpawnMargin, RearOfConvoy() + queue);

            for (int i = 0; i < count; i++) SpawnOne(tank, length, spawnX + i * queue);
        }

        public void Capture(List<VehicleSnapshot> into)
        {
            foreach (GroundVehicle vehicle in _live)
            {
                if (vehicle == null || !vehicle.IsAlive) continue;

                into.Add(new VehicleSnapshot
                {
                    kind = vehicle is EnemyTank ? EnemyKind.Tank : EnemyKind.Truck,
                    x = vehicle.transform.position.x,
                    health = vehicle.CurrentHealth,
                });
            }
        }

        public void Restore(IReadOnlyList<VehicleSnapshot> saved)
        {
            if (saved == null) return;

            foreach (VehicleSnapshot snapshot in saved)
            {
                bool tank = snapshot.kind == EnemyKind.Tank;
                if (!(tank ? EnemyTank.Measure() : EnemyTruck.Measure())) continue;

                float length = tank ? EnemyTank.BodyLength : EnemyTruck.BodyLength;
                GroundVehicle vehicle = SpawnOne(tank, length, snapshot.x);
                if (vehicle != null) vehicle.Restore(snapshot.health);
            }
        }

        GroundVehicle SpawnOne(bool tank, float length, float x)
        {
            var point = new Vector3(x, ProceduralTerrain.BaseLevel, _roadZ);
            GroundVehicle vehicle = tank
                ? (GroundVehicle)EnemyTank.Spawn(point, StopLine(length), _roadLift, _player, _land)
                : EnemyTruck.Spawn(point, StopLine(length), _roadLift, _player, _land);

            if (vehicle == null) return null;

            vehicle.SetAhead(_live.Count > 0 ? _live[_live.Count - 1] : null);
            vehicle.OnDestroyed += Remove;
            _live.Add(vehicle);
            return vehicle;
        }

        public void StandDown()
        {
            foreach (GroundVehicle vehicle in _live)
                if (vehicle != null) vehicle.StandDown();
        }

        static float StopLine(float length) => Airfield.Current != null
            ? Airfield.Current.FenceX + GroundVehicle.FenceStandoff + length * 0.5f
            : float.NegativeInfinity;

        float RearOfConvoy()
        {
            float rear = float.NegativeInfinity;
            foreach (GroundVehicle vehicle in _live)
                if (vehicle != null) rear = Mathf.Max(rear, vehicle.RearX);
            return rear;
        }

        void Remove(GroundVehicle vehicle)
        {
            int index = _live.IndexOf(vehicle);
            if (index < 0) return;

            if (index + 1 < _live.Count)
                _live[index + 1].SetAhead(index > 0 ? _live[index - 1] : null);

            _live.RemoveAt(index);
        }
    }
}
