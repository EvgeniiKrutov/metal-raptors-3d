using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignAssault
    {
        const float GapMin = 3f, GapMax = 5f;
        const float QuietTailSec = 5f;
        const int PairSize = 2;

        readonly ICampaignScriptHost _host;
        readonly EnemyGroup[] _planes;
        readonly float _seconds;
        readonly float _pairsAt;
        readonly float _single;
        readonly List<float> _arrivals = new List<float>();
        readonly List<EnemyGroup> _due = new List<EnemyGroup>();

        float _elapsed;
        int _alive;
        bool _lone;
        bool _started;

        public bool Over => _elapsed >= _seconds;

        public float Left => Mathf.Max(0f, _seconds - _elapsed);

        public CampaignAssault(ICampaignScriptHost host, CampaignStep step)
        {
            _host = host;
            _planes = step.groups ?? new EnemyGroup[0];
            _seconds = Mathf.Max(0f, step.seconds);
            _pairsAt = step.pairsAt;
            _single = step.single;
        }

        public void Tick(float dt)
        {
            _elapsed += dt;
            if (_planes.Length == 0) return;

            bool pairs = _elapsed >= _pairsAt;
            int alive = _host.EnemiesAlive;
            if (alive < _alive && pairs) _lone = Random.value < _single;
            if (alive == 0) _lone = false;

            int cap = pairs && !_lone ? PairSize : 1;
            while (_arrivals.Count > 0 && alive + _arrivals.Count > cap)
                _arrivals.RemoveAt(_arrivals.Count - 1);
            while (alive + _arrivals.Count < cap)
                _arrivals.Add(_started ? Random.Range(GapMin, GapMax) : 0f);
            _started = true;

            if (_elapsed < _seconds - QuietTailSec) Arrive(dt);
            _alive = _host.EnemiesAlive;
        }

        void Arrive(float dt)
        {
            _due.Clear();
            for (int i = _arrivals.Count - 1; i >= 0; i--)
            {
                _arrivals[i] -= dt;
                if (_arrivals[i] > 0f) continue;

                _arrivals.RemoveAt(i);
                _due.Add(Pick());
            }

            if (_due.Count > 0) _host.SpawnWave(_due.ToArray());
        }

        EnemyGroup Pick()
        {
            EnemyGroup group = _planes[Random.Range(0, _planes.Length)];
            return new EnemyGroup(group.plane, 1, group.sneak, group.high);
        }
    }
}
