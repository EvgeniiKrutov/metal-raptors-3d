using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignDefense
    {
        const float SilentLeadSec = 1f;
        const int PairSize = 2;

        static readonly PlaneModelConfig[] RandomPool =
            { PlaneModels.Eindecker, PlaneModels.Albatros, PlaneModels.Fokker };

        readonly ICampaignScriptHost _host;
        readonly EnemyGroup[][] _groups;

        int _group = -1;
        bool _awaiting;
        float _lead;
        bool _settle;
        bool _done;

        public bool Warned { get; private set; }

        public bool WarnedPair { get; private set; }

        public bool Done => _done;

        public CampaignDefense(ICampaignScriptHost host, CampaignStep step, bool warned, bool warnedPair)
        {
            _host = host;
            _groups = step.waves ?? new EnemyGroup[0][];
            Warned = warned;
            WarnedPair = warnedPair;
        }

        public void Tick(float dt)
        {
            if (_done) return;

            if (_group < 0)
            {
                BeginGroup(0);
                return;
            }

            if (_awaiting)
            {
                _lead -= dt;
                if (_lead > 0f) return;

                _awaiting = false;
                _settle = true;
                _host.SpawnWave(Resolve(_groups[_group]));
                return;
            }

            if (_settle)
            {
                _settle = false;
                return;
            }

            if (_host.EnemiesAlive > 0) return;
            BeginGroup(_group + 1);
        }

        void BeginGroup(int index)
        {
            _group = index;
            if (_group >= _groups.Length)
            {
                _done = true;
                return;
            }

            int planes = 0;
            foreach (EnemyGroup group in _groups[_group])
                if (group.kind == EnemyKind.Plane) planes += group.count;

            if (planes == 0)
            {
                _lead = 0f;
                _awaiting = true;
                return;
            }

            bool announce = !Warned || (!WarnedPair && planes >= PairSize);
            Warned = true;
            if (planes >= PairSize) WarnedPair = true;

            _lead = announce ? _host.WarnIncoming(planes) : SilentLeadSec;
            _awaiting = true;
        }

        static EnemyGroup[] Resolve(EnemyGroup[] groups)
        {
            var resolved = new List<EnemyGroup>();
            foreach (EnemyGroup group in groups)
            {
                if (group.kind != EnemyKind.Plane || group.plane != null)
                {
                    resolved.Add(group);
                    continue;
                }

                for (int i = 0; i < group.count; i++)
                    resolved.Add(new EnemyGroup(RandomPool[Random.Range(0, RandomPool.Length)], 1));
            }
            return resolved.ToArray();
        }
    }
}
