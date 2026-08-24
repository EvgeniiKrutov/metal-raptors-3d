using UnityEngine;

namespace MetalRaptors
{
    public class EnemyLoop
    {
        public bool Active { get; private set; }

        public float Heading { get; private set; }

        float _from;
        float _dir;
        float _seconds;
        float _t;

        public void Begin(float heading, float seconds)
        {
            _from = heading;
            _dir = Mathf.Cos(heading) >= 0f ? 1f : -1f;
            _seconds = Mathf.Max(0.05f, seconds);
            _t = 0f;
            Heading = heading;
            Active = true;
        }

        public void Step(float dt)
        {
            if (!Active) return;

            _t += dt;
            float u = Mathf.Clamp01(_t / _seconds);
            Heading = _from + _dir * Mathf.PI * u;
            if (u >= 1f) Active = false;
        }

        public void Cancel() => Active = false;
    }
}
