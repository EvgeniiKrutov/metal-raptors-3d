using UnityEngine;

namespace MetalRaptors
{
    public class PlaneRoll
    {
        public const float InvertedDelay = 1f;
        public const float RollRateFraction = 1f;
        public const float LevelCos = 0.5f;
        public const float SteadyTurnFraction = 0.15f;

        const float HalfTurn = 180f;
        const float MinRollRate = 20f;

        readonly float _uprightSign;

        float _angle;
        float _invertedTime;
        float _from;
        float _direction = 1f;
        float _progress = -1f;
        float _seconds = 1f;

        public PlaneRoll(bool mirrored)
        {
            _uprightSign = mirrored ? -1f : 1f;
        }

        public float Angle => _angle;

        public bool Rolling => _progress >= 0f;

        public static bool Steady(float turnRate, float maxTurnRate)
        {
            return Mathf.Abs(turnRate) <= maxTurnRate * SteadyTurnFraction;
        }

        public float Tick(float dt, float headingRad, bool steady, float maxTurnRateDeg)
        {
            if (Rolling) return Advance(dt);

            _invertedTime = steady && Inverted(headingRad) ? _invertedTime + dt : 0f;
            if (_invertedTime < InvertedDelay) return _angle;

            Begin(maxTurnRateDeg);
            return _angle;
        }

        public void Flip(float headingRad, float maxTurnRateDeg)
        {
            if (Rolling || !Inverted(headingRad)) return;
            Begin(maxTurnRateDeg);
        }

        void Begin(float maxTurnRateDeg)
        {
            _from = _angle;
            _direction = Random.value < 0.5f ? -1f : 1f;
            _seconds = HalfTurn / Mathf.Max(MinRollRate, maxTurnRateDeg * RollRateFraction);
            _progress = 0f;
        }

        bool Inverted(float headingRad)
        {
            float nose = Mathf.Cos(headingRad);
            if (Mathf.Abs(nose) < LevelCos) return false;

            return nose * Mathf.Cos(_angle * Mathf.Deg2Rad) * _uprightSign < 0f;
        }

        float Advance(float dt)
        {
            _progress = Mathf.Min(1f, _progress + dt / _seconds);
            _angle = _from + _direction * HalfTurn * Mathf.SmoothStep(0f, 1f, _progress);

            if (_progress >= 1f)
            {
                _angle = Wrap(_from + _direction * HalfTurn);
                _progress = -1f;
                _invertedTime = 0f;
            }
            return _angle;
        }

        static float Wrap(float degrees)
        {
            return degrees - 360f * Mathf.Floor((degrees + HalfTurn) / 360f);
        }
    }
}
