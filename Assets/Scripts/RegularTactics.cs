using UnityEngine;

namespace MetalRaptors
{
    public class RegularTactics
    {
        public const float TailHoldSec = 4f;
        public const float CircleSec = 2.5f;
        public const float ScissorsConeDeg = 45f;
        public const float BaitLeadMin = 0.5f;
        public const float BaitLeadMax = 0.7f;

        const float ExtendMinSec = 0.8f;
        const float ExtendMaxSec = 1.2f;
        const float ClimbMinDeg = 10f;
        const float ClimbMaxDeg = 20f;

        public bool Extending { get; private set; }

        float _t;
        float _seconds;
        float _heading;

        public void BeginExtend(float heading)
        {
            float forward = Mathf.Cos(heading) >= 0f ? 1f : -1f;
            float pitch = Random.Range(ClimbMinDeg, ClimbMaxDeg) * Mathf.Deg2Rad;
            _heading = forward >= 0f ? pitch : Mathf.PI - pitch;
            _seconds = Random.Range(ExtendMinSec, ExtendMaxSec);
            _t = 0f;
            Extending = true;
        }

        public float Step(float dt)
        {
            if (!Extending) return float.NaN;

            _t += dt;
            if (_t >= _seconds)
            {
                Extending = false;
                return float.NaN;
            }
            return _heading;
        }

        public void Cancel() => Extending = false;
    }
}
