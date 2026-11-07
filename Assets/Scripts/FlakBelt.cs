using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class FlakBelt
    {
        const float GapMin = 1.2f, GapMax = 1.8f;
        const float FirstMax = 0.5f;
        const float Ahead = 120f;
        const float Forget = 250f;
        const float InViewShare = 0.9f;
        const int SeedStep = 131;

        readonly List<AaGun> _guns = new List<AaGun>();
        readonly Transform _target;
        readonly CampaignTerrain _terrain;
        readonly float _z;
        readonly float _cruise;
        readonly float _planeLength;
        readonly float _facing;
        readonly int _seed;

        float _next = float.NaN;
        bool _placing = true;
        int _placed;
        float _camX, _halfWidth;

        public FlakBelt(Transform target, CampaignTerrain terrain, float z, float cruise, float planeLength,
            float facing, int seed)
        {
            _target = target;
            _terrain = terrain;
            _z = z;
            _cruise = cruise;
            _planeLength = planeLength;
            _facing = facing < 0f ? -1f : 1f;
            _seed = seed;
        }

        public bool Idle => !_placing && _guns.Count == 0;

        public void Stop() => _placing = false;

        public void Tick(float camX, float halfWidth)
        {
            _camX = camX;
            _halfWidth = halfWidth;

            float screen = halfWidth * 2f;
            float edge = camX + _facing * (halfWidth + Ahead);
            if (float.IsNaN(_next)) _next = edge + _facing * Random.Range(0f, FirstMax) * screen;

            if (_placing && (edge - _next) * _facing >= 0f)
            {
                Place(_next);
                _next += _facing * Random.Range(GapMin, GapMax) * screen;
            }

            for (int i = _guns.Count - 1; i >= 0; i--)
            {
                AaGun gun = _guns[i];
                if (gun != null && Mathf.Abs(gun.X - camX) <= halfWidth + Forget) continue;

                if (gun != null) Object.Destroy(gun.gameObject);
                _guns.RemoveAt(i);
            }
        }

        public void Fire(Vector3 at)
        {
            AaGun shooter = null;
            float best = float.MaxValue;

            foreach (AaGun gun in _guns)
            {
                if (gun == null || !gun.Grounded) continue;
                if (Mathf.Abs(gun.X - _camX) > _halfWidth * InViewShare) continue;

                float gap = Mathf.Abs(gun.X - at.x);
                if (gap >= best) continue;
                best = gap;
                shooter = gun;
            }

            if (shooter != null) shooter.Shoot();
        }

        void Place(float x)
        {
            _placed++;
            float spot = AaGun.ClearSpot(_terrain, x, _z);
            _guns.Add(AaGun.Place(spot, _z, _target, _cruise, _planeLength, _seed + _placed * SeedStep,
                lamp: false));
        }
    }
}
