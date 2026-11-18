using UnityEngine;

namespace MetalRaptors
{
    public class EnemyBarrel
    {
        const float FullTurn = 360f;
        const float MinUnwindRate = 180f;
        const float MinUnwindSec = 0.35f;
        const float MaxUnwindSec = 1.2f;
        const float UnwindLookSec = 0.4f;

        public bool Active { get; private set; }

        public bool Spinning => Active || _unwinding;

        public float Spin { get; private set; }

        float _seconds;
        float _turns;
        float _dir;
        float _t;

        bool _unwinding;
        float _unwindFrom;
        float _unwindTo;
        float _unwindRate;
        float _unwindSec;
        float _unwindT;

        public void Begin(float seconds, float turns, float dir)
        {
            _seconds = Mathf.Max(0.1f, seconds);
            _turns = Mathf.Max(1f, Mathf.Round(turns));
            _dir = dir >= 0f ? 1f : -1f;
            _t = 0f;
            _unwinding = false;
            Spin = 0f;
            Active = true;
        }

        public void Step(float dt)
        {
            if (_unwinding)
            {
                Unwind(dt);
                return;
            }
            if (!Active) return;

            _t += dt;
            float u = Mathf.Clamp01(_t / _seconds);
            Spin = _dir * FullTurn * _turns * FlightSmoothing.RollProfile(u);
            if (u < 1f) return;

            Spin = 0f;
            Active = false;
        }

        public void Cancel()
        {
            if (!Active) return;
            Active = false;

            float rate = _dir * Rate();
            float ahead = Spin + rate * UnwindLookSec;
            float target = Mathf.Round(ahead / FullTurn) * FullTurn;
            float gap = Mathf.Abs(target - Spin);
            if (gap < 0.5f && Mathf.Abs(rate) < 1f)
            {
                Spin = 0f;
                return;
            }

            _unwindFrom = Spin;
            _unwindTo = target;
            _unwindRate = rate;
            _unwindSec = Mathf.Clamp(2f * gap / Mathf.Max(MinUnwindRate, Mathf.Abs(rate)),
                MinUnwindSec, MaxUnwindSec);
            _unwindT = 0f;
            _unwinding = true;
        }

        public void Stop()
        {
            Active = false;
            _unwinding = false;
            Spin = 0f;
        }

        void Unwind(float dt)
        {
            _unwindT += dt;
            float s = Mathf.Clamp01(_unwindT / _unwindSec);
            float s2 = s * s;
            float s3 = s2 * s;

            Spin = (2f * s3 - 3f * s2 + 1f) * _unwindFrom
                 + (s3 - 2f * s2 + s) * _unwindSec * _unwindRate
                 + (3f * s2 - 2f * s3) * _unwindTo;
            if (s < 1f) return;

            Spin = 0f;
            _unwinding = false;
        }

        float Rate()
        {
            float u = Mathf.Clamp01(_t / _seconds);
            return FullTurn * _turns / _seconds * Slope(u);
        }

        static float Slope(float u)
        {
            const float ramp = FlightSmoothing.RollRamp;
            float peak = 1f / (1f - ramp);
            if (u < ramp) return peak * u / ramp;
            if (u > 1f - ramp) return peak * (1f - u) / ramp;
            return peak;
        }
    }
}
