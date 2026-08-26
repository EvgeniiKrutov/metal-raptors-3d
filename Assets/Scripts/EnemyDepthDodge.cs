using UnityEngine;

namespace MetalRaptors
{
    public class EnemyDepthDodge
    {
        public const float ClearDepth = 35f;

        const float HomeEpsilon = 1f;

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

        bool _homing;
        float _homeZ;
        float _homeBank;
        float _homeCarry;
        float _homeRoll;
        float _homeBack;
        float _homeLevel;

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
            _homing = false;
            Z = baseZ;
            Bank = 0f;
            Active = true;
        }

        public void Release()
        {
            if (!Active || _homing) return;

            float span = Mathf.Abs(Z - _baseZ);
            bool slide = span >= HomeEpsilon;

            _homeZ = Z;
            _homeBank = Bank;
            _homeCarry = slide ? -_bank : 0f;
            _homeRoll = RollSeconds(_homeBank, _homeCarry);
            _homeBack = slide ? _back * span / Mathf.Max(HomeEpsilon, Mathf.Abs(_depth)) : 0f;
            _homeLevel = slide ? RollSeconds(_homeCarry, 0f) : 0f;
            _t = 0f;
            _homing = true;

            if (_homeRoll + _homeBack + _homeLevel <= 0f) Cancel();
        }

        public void Step(float dt)
        {
            if (!Active) return;

            _t += dt;
            if (_homing) { StepHome(_t); return; }

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

        void StepHome(float t)
        {
            if (Phase(ref t, _homeRoll, out float u))
            { Z = _homeZ; Bank = Mathf.Lerp(_homeBank, _homeCarry, Ease(u)); return; }
            if (Phase(ref t, _homeBack, out u))
            { Z = Mathf.Lerp(_homeZ, _baseZ, Ease(u)); Bank = _homeCarry; return; }
            if (Phase(ref t, _homeLevel, out u))
            { Z = _baseZ; Bank = _homeCarry * (1f - Ease(u)); return; }

            Cancel();
        }

        float RollSeconds(float from, float to) =>
            _roll * Mathf.Abs(to - from) / Mathf.Max(1f, Mathf.Abs(_bank));

        public void Cancel()
        {
            Active = false;
            _homing = false;
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
