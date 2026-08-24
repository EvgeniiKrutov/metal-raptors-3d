using UnityEngine;

namespace MetalRaptors
{
    public class EnemyDepthDodge
    {
        public const float ClearDepth = 35f;

        public bool Active { get; private set; }

        public float Z { get; private set; }

        public float Bank { get; private set; }

        public bool Clear => Active && Mathf.Abs(Z - _baseZ) >= ClearDepth;

        float _baseZ;
        float _depth;
        float _bank;
        float _roll;
        float _out;
        float _hold;
        float _back;
        float _t;

        public void Begin(float baseZ, float depth, float bank, float rollSeconds,
            float outSeconds, float holdSeconds, float backSeconds)
        {
            _baseZ = baseZ;
            _depth = depth;
            _bank = bank;
            _roll = Mathf.Max(0f, rollSeconds);
            _out = Mathf.Max(0.01f, outSeconds);
            _hold = Mathf.Max(0f, holdSeconds);
            _back = Mathf.Max(0.01f, backSeconds);
            _t = 0f;
            Z = baseZ;
            Bank = 0f;
            Active = true;
        }

        public void Step(float dt)
        {
            if (!Active) return;

            _t += dt;
            float t = _t;
            float far = _baseZ + _depth;

            if (Phase(ref t, _roll, out float u)) { Z = _baseZ; Bank = _bank * Ease(u); return; }
            if (Phase(ref t, _out, out u)) { Z = Mathf.Lerp(_baseZ, far, Ease(u)); Bank = _bank; return; }
            if (Phase(ref t, _roll, out u)) { Z = far; Bank = _bank * (1f - Ease(u)); return; }
            if (Phase(ref t, _hold, out u)) { Z = far; Bank = 0f; return; }
            if (Phase(ref t, _roll, out u)) { Z = far; Bank = -_bank * Ease(u); return; }
            if (Phase(ref t, _back, out u)) { Z = Mathf.Lerp(far, _baseZ, Ease(u)); Bank = -_bank; return; }
            if (Phase(ref t, _roll, out u)) { Z = _baseZ; Bank = -_bank * (1f - Ease(u)); return; }

            Cancel();
        }

        public void Cancel()
        {
            Active = false;
            Z = _baseZ;
            Bank = 0f;
        }

        static bool Phase(ref float t, float span, out float u)
        {
            if (t < span)
            {
                u = span > 0f ? t / span : 1f;
                return true;
            }

            t -= span;
            u = 1f;
            return false;
        }

        static float Ease(float u) => Mathf.SmoothStep(0f, 1f, u);
    }
}
