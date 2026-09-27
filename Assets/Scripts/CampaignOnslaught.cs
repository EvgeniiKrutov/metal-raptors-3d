using UnityEngine;

namespace MetalRaptors
{
    public class CampaignOnslaught
    {
        const float QuietTailSec = 10f;
        const float TankRespawnSec = 8f;

        static readonly float[] AlbatrosAt = { 0.1f, 0.5f, 0.8f };

        readonly struct Stage
        {
            public readonly float from;
            public readonly int trucks;
            public readonly float truckGap;
            public readonly int truckGroup;
            public readonly bool tank;

            public Stage(float from, int trucks, float truckGap, int truckGroup, bool tank)
            {
                this.from = from;
                this.trucks = trucks;
                this.truckGap = truckGap;
                this.truckGroup = truckGroup;
                this.tank = tank;
            }
        }

        static readonly Stage[] GroundStages =
        {
            new Stage(0f, 1, 10f, 1, false),
            new Stage(1f / 3f, 2, 7f, 1, true),
            new Stage(2f / 3f, 3, 5f, 2, true),
        };

        static readonly Stage[] AirStages =
        {
            new Stage(0f, 2, 8f, 1, false),
            new Stage(1f / 3f, 3, 6f, 2, true),
            new Stage(2f / 3f, 3, 4f, 2, true),
        };

        readonly ICampaignScriptHost _host;
        readonly Stage[] _stages;
        readonly float _seconds;
        readonly bool _air;

        float _elapsed;
        float _truckWait;
        float _tankWait;
        float _albatrosLead;
        int _albatrosNext;

        public bool Warned { get; private set; }

        public bool Over => _elapsed >= _seconds;

        public float Left => Mathf.Max(0f, _seconds - _elapsed);

        public CampaignOnslaught(ICampaignScriptHost host, float seconds, bool air, bool warned)
        {
            _host = host;
            _seconds = Mathf.Max(0f, seconds);
            _air = air;
            _stages = air ? AirStages : GroundStages;
            Warned = warned;
        }

        public void Tick(float dt)
        {
            _elapsed += dt;
            if (_elapsed >= _seconds - QuietTailSec) return;

            Stage stage = StageAt(_elapsed / _seconds);
            TickTrucks(stage, dt);
            TickTank(stage, dt);

            if (_air) TickAlbatros(dt);
        }

        Stage StageAt(float progress)
        {
            Stage stage = _stages[0];
            foreach (Stage next in _stages)
                if (progress >= next.from) stage = next;
            return stage;
        }

        void TickTrucks(Stage stage, float dt)
        {
            _truckWait -= dt;
            if (_truckWait > 0f) return;

            int room = stage.trucks - _host.CountAlive(EnemyKind.Truck, null);
            if (room <= 0) return;

            int count = Mathf.Min(room, stage.truckGroup);
            _host.SpawnWave(new[] { new EnemyGroup(EnemyKind.Truck, count) });
            _truckWait = stage.truckGap;
        }

        void TickTank(Stage stage, float dt)
        {
            if (!stage.tank || _host.CountAlive(EnemyKind.Tank, null) > 0) return;

            _tankWait -= dt;
            if (_tankWait > 0f) return;

            _host.SpawnWave(new[] { new EnemyGroup(EnemyKind.Tank, 1) });
            _tankWait = TankRespawnSec;
        }

        void TickAlbatros(float dt)
        {
            if (_albatrosNext >= AlbatrosAt.Length) return;
            if (_elapsed < _seconds * AlbatrosAt[_albatrosNext]) return;
            if (_host.CountAlive(EnemyKind.Plane, PlaneModels.Albatros) > 0) return;

            if (!Warned)
            {
                Warned = true;
                _albatrosLead = _host.WarnIncoming(1);
            }

            _albatrosLead -= dt;
            if (_albatrosLead > 0f) return;

            _host.SpawnWave(new[] { new EnemyGroup(PlaneModels.Albatros, 1) });
            _albatrosNext++;
        }
    }
}
