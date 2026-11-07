using UnityEngine;

namespace MetalRaptors
{
    public class StealthMeter
    {
        const float LightFillSec = 2f;
        const float HeadlightFillSec = 15f;
        const float BurstFill = 0.08f;
        const float BurstEverySec = 0.6f;
        const float DrainSec = 2f;
        const float Empty = 0.001f;

        float _burstClock;
        bool _wasFiring;

        public float Level { get; private set; }

        public bool Locked { get; private set; }

        public bool Touched { get; private set; }

        public float LockedFor { get; private set; }

        public void Tick(float dt, bool lit, bool headlight, bool firing)
        {
            if (dt <= 0f) return;

            Touched = lit || headlight || firing;

            if (lit) Level += dt / LightFillSec;
            else if (headlight) Level += dt / HeadlightFillSec;
            else Level -= dt / DrainSec;

            if (firing)
            {
                _burstClock -= dt;
                if (!_wasFiring || _burstClock <= 0f)
                {
                    Level += BurstFill;
                    _burstClock = BurstEverySec;
                }
            }
            _wasFiring = firing;

            Level = Mathf.Clamp01(Level);
            if (Level >= 1f) Locked = true;
            else if (Level <= Empty) Locked = false;

            LockedFor = Locked ? LockedFor + dt : 0f;
        }

        public void Alarm()
        {
            Level = 1f;
            Locked = true;
            LockedFor = 0f;
        }

        public void Clear()
        {
            Level = 0f;
            Locked = false;
            Touched = false;
            LockedFor = 0f;
            _wasFiring = false;
            _burstClock = 0f;
        }
    }
}
