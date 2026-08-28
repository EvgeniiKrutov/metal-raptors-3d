using UnityEngine;

namespace MetalRaptors
{
    public enum EvadeMove
    {
        Break,
        Scissors,
        Chandelle,
        SplitDive,
        Extend,
    }

    public struct EvadePlan
    {
        public EvadeMove move;
        public float side;
        public float breakHeading;
        public float breakSeconds;
        public float jitterAmplitude;
        public float jitterHz;
    }

    public class EnemyEvade
    {
        public const float ScissorsAngle = 50f;
        const float ScissorsPhase = 0.7f;
        const int ScissorsCrosses = 3;

        const float ChandelleAngle = 62f;
        const float ChandelleUp = 1.1f;
        const float ChandelleOver = 0.9f;

        const float SplitAngle = 48f;
        const float SplitDown = 0.9f;
        const float SplitBack = 0.8f;

        const float ExtendPitch = 12f;
        const float ExtendOut = 1.5f;
        const float ExtendBack = 0.7f;

        public bool Active { get; private set; }

        public EvadeMove Move { get; private set; }

        public float Heading { get; private set; }

        public float Seconds { get; private set; }

        EvadePlan _plan;
        float _forward;
        float _t;
        float _jitter;
        float _jitterTimer;

        public void Begin(EvadePlan plan, float heading, Vector2 self, Vector2 target)
        {
            _plan = plan;
            _forward = Mathf.Cos(heading) >= 0f ? 1f : -1f;
            _t = 0f;
            _jitter = 0f;
            _jitterTimer = 0f;

            Move = plan.move;
            Seconds = Length(plan.move, plan.breakSeconds);
            Active = true;
            Heading = heading;

            Compute(0f, self, target);
        }

        public void Step(float dt, Vector2 self, Vector2 target)
        {
            if (!Active) return;

            _t += dt;
            if (_t >= Seconds) Active = false;

            Compute(dt, self, target);
        }

        public void Cancel() => Active = false;

        static float Length(EvadeMove move, float breakSeconds)
        {
            switch (move)
            {
                case EvadeMove.Scissors: return ScissorsCrosses * ScissorsPhase;
                case EvadeMove.Chandelle: return ChandelleUp + ChandelleOver;
                case EvadeMove.SplitDive: return SplitDown + SplitBack;
                case EvadeMove.Extend: return ExtendOut + ExtendBack;
                default: return breakSeconds;
            }
        }

        void Compute(float dt, Vector2 self, Vector2 target)
        {
            Vector2 away = self - target;
            if (away.sqrMagnitude < 1f) away = new Vector2(-_forward, 0f);

            float awayHeading = Mathf.Atan2(away.y, away.x);
            float towardHeading = awayHeading + Mathf.PI;

            switch (Move)
            {
                case EvadeMove.Scissors:
                {
                    int cross = Mathf.Min(ScissorsCrosses - 1, (int)(_t / ScissorsPhase));
                    float side = cross % 2 == 0 ? _plan.side : -_plan.side;
                    Heading = towardHeading + side * ScissorsAngle * Mathf.Deg2Rad;
                    break;
                }

                case EvadeMove.Chandelle:
                    Heading = _t < ChandelleUp ? Pitched(ChandelleAngle, _forward) : towardHeading;
                    break;

                case EvadeMove.SplitDive:
                    Heading = _t < SplitDown ? Pitched(-SplitAngle, -_forward) : towardHeading;
                    break;

                case EvadeMove.Extend:
                    Heading = _t < ExtendOut ? Flattened(awayHeading) : towardHeading;
                    break;

                default:
                    StepJitter(dt);
                    Heading = _plan.breakHeading + _jitter;
                    break;
            }
        }

        static float Pitched(float degrees, float forward)
        {
            float pitch = degrees * Mathf.Deg2Rad;
            return forward >= 0f ? pitch : Mathf.PI - pitch;
        }

        static float Flattened(float heading)
        {
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Sin(heading)) * Mathf.Rad2Deg,
                -ExtendPitch, ExtendPitch);
            return Pitched(pitch, Mathf.Cos(heading) >= 0f ? 1f : -1f);
        }

        void StepJitter(float dt)
        {
            _jitterTimer -= dt;
            if (_jitterTimer > 0f) return;

            _jitter = Random.Range(-1f, 1f) * _plan.jitterAmplitude * Mathf.Deg2Rad;
            _jitterTimer = 1f / Mathf.Max(0.01f, _plan.jitterHz);
        }
    }
}
