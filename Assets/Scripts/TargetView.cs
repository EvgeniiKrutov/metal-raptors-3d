using UnityEngine;

namespace MetalRaptors
{
    public class TargetView
    {
        const int Capacity = 64;

        readonly float[] _time = new float[Capacity];
        readonly Vector2[] _pos = new Vector2[Capacity];
        readonly Vector2[] _vel = new Vector2[Capacity];
        int _head;
        int _count;
        Vector2 _restPos;
        Vector2 _restVel;

        public void Reset(Vector2 pos, Vector2 vel)
        {
            _head = 0;
            _count = 0;
            _restPos = pos;
            _restVel = vel;
        }

        public void Record(float time, Vector2 pos, Vector2 vel)
        {
            _head = (_head + 1) % Capacity;
            _time[_head] = time;
            _pos[_head] = pos;
            _vel[_head] = vel;
            _count = Mathf.Min(_count + 1, Capacity);
        }

        public void Sample(float time, float delay, out Vector2 pos, out Vector2 vel)
        {
            if (_count == 0)
            {
                pos = _restPos;
                vel = _restVel;
                return;
            }

            float at = time - Mathf.Max(0f, delay);
            int newer = _head;

            for (int i = 1; i < _count; i++)
            {
                int older = (_head - i + Capacity) % Capacity;
                if (_time[older] <= at)
                {
                    float span = _time[newer] - _time[older];
                    float u = span > 0f ? Mathf.Clamp01((at - _time[older]) / span) : 1f;
                    Vector2 p = Vector2.Lerp(_pos[older], _pos[newer], u);
                    vel = Vector2.Lerp(_vel[older], _vel[newer], u);
                    pos = p + vel * Mathf.Max(0f, time - at);
                    return;
                }
                newer = older;
            }

            int oldest = (_head - (_count - 1) + Capacity) % Capacity;
            vel = _vel[oldest];
            pos = _pos[oldest] + vel * Mathf.Max(0f, time - _time[oldest]);
        }
    }
}
