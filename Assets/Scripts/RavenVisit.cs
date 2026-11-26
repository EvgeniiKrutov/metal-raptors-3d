using UnityEngine;

namespace MetalRaptors
{
    public class RavenVisit
    {
        const float StartRelSpeed = 160f;
        const float TopRelSpeed = 270f;
        const float RelAccel = 180f;
        const float RelBrake = 200f;
        const float Arrive = 2f;
        const float LaneResponse = 3f;
        const float HoldResponse = 2.5f;
        const float MaxPitchDeg = 18f;
        const float HeadingResponse = 6f;
        const float LeaveClimbDeg = 55f;
        const float LeaveTurnSec = 1.4f;
        const float LeaveSpeedGain = 1.7f;
        const float LeaveAccelSec = 2f;
        const float LeaveMaxSec = 8f;

        enum Stage { Enter, Hold, Leave, Gone }

        readonly EnemyController _plane;
        readonly float _facing;
        Vector2 _pos;
        float _rel;
        float _heading;
        float _leaveAge;
        Stage _stage;

        public EnemyController Plane => _plane;

        public bool Arrived => _stage != Stage.Enter;

        public bool Gone => _stage == Stage.Gone;

        public RavenVisit(EnemyController plane, Vector2 start, float facing)
        {
            _plane = plane;
            _pos = start;
            _facing = facing < 0f ? -1f : 1f;
            _rel = StartRelSpeed;
            _heading = _facing > 0f ? 0f : Mathf.PI;
            if (_plane != null) _plane.ScriptTo(start, _heading);
        }

        public void Leave()
        {
            if (_stage == Stage.Gone) return;
            _stage = Stage.Leave;
            _leaveAge = 0f;
        }

        public void Tick(float dt, Vector2 mark, float pace, float topY)
        {
            if (_plane == null)
            {
                _stage = Stage.Gone;
                return;
            }
            if (dt <= 0f || _stage == Stage.Gone) return;

            Vector2 before = _pos;
            switch (_stage)
            {
                case Stage.Enter:
                    Enter(dt, mark, pace);
                    break;
                case Stage.Hold:
                    Keep(dt, mark, pace);
                    break;
                case Stage.Leave:
                    Climb(dt, pace);
                    break;
            }

            Steer((_pos - before) / dt, dt);
            _plane.ScriptSteer(_pos, _heading, dt);

            if (_stage == Stage.Leave && (_pos.y > topY || _leaveAge >= LeaveMaxSec)) _stage = Stage.Gone;
        }

        void Enter(float dt, Vector2 mark, float pace)
        {
            float left = (mark.x - _pos.x) * _facing;
            float cap = Mathf.Min(TopRelSpeed, Mathf.Sqrt(2f * RelBrake * Mathf.Max(0f, left)));
            _rel = Mathf.MoveTowards(_rel, cap, (cap > _rel ? RelAccel : RelBrake) * dt);

            _pos.x += _facing * (pace + _rel) * dt;
            _pos.y += (mark.y - _pos.y) * (1f - Mathf.Exp(-LaneResponse * dt));

            if ((mark.x - _pos.x) * _facing > Arrive) return;
            _pos.x = mark.x;
            _stage = Stage.Hold;
        }

        void Keep(float dt, Vector2 mark, float pace)
        {
            _pos.x += _facing * pace * dt;
            _pos += (mark - _pos) * (1f - Mathf.Exp(-HoldResponse * dt));
        }

        void Climb(float dt, float pace)
        {
            _leaveAge += dt;
            float climb = LeaveClimbDeg * Mathf.SmoothStep(0f, 1f, _leaveAge / LeaveTurnSec) * Mathf.Deg2Rad;
            float speed = pace * Mathf.Lerp(1f, LeaveSpeedGain,
                Mathf.SmoothStep(0f, 1f, _leaveAge / LeaveAccelSec));
            _pos += new Vector2(_facing * Mathf.Cos(climb), Mathf.Sin(climb)) * speed * dt;
        }

        void Steer(Vector2 velocity, float dt)
        {
            float level = _facing > 0f ? 0f : Mathf.PI;
            if (velocity.sqrMagnitude < 1f) return;

            float pitch = Mathf.Atan2(velocity.y, Mathf.Abs(velocity.x));
            if (_stage != Stage.Leave)
                pitch = Mathf.Clamp(pitch, -MaxPitchDeg * Mathf.Deg2Rad, MaxPitchDeg * Mathf.Deg2Rad);

            float goal = _facing > 0f ? level + pitch : level - pitch;
            _heading = Mathf.LerpAngle(_heading * Mathf.Rad2Deg, goal * Mathf.Rad2Deg,
                1f - Mathf.Exp(-HeadingResponse * dt)) * Mathf.Deg2Rad;
        }
    }
}
