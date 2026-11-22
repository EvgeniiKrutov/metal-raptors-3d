using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public struct SkyFrame
    {
        public float camX;
        public float anchorY;
        public float depthZ;
        public float halfWidth;
        public float halfHeight;
        public float camVelX;

        public Vector3 World(Vector3 at) => new Vector3(camX + at.x, anchorY + at.y, depthZ + at.z);

        public Vector3 Local(Vector3 world) =>
            new Vector3(world.x - camX, world.y - anchorY, world.z - depthZ);
    }

    public class SkyPath
    {
        enum Leg { Run, Loop, Settle, Hold }

        struct Drift
        {
            public Vector3 amp;
            public float start;
        }

        const float TurnEdgeShare = 0.55f;
        const float HardEdgeShare = 0.9f;
        const float EntryShare = 1.15f;
        const float PaceShare = 1f / 3f;
        const float EntryPaceShare = 1.3f;

        const float LoopShare = 0.62f;
        const float LoopPaceShare = 0.7f;
        const float RollShare = 0.42f;
        const float DriftShare = 1.1f;
        const int ArcSteps = 48;
        const float WideAspect = 16f / 9f;

        const float RollRateDeg = 300f;
        const float UpResponse = 12f;
        const float BlendSec = 0.8f;

        const float SpeedResponse = 1.2f;
        const float AccelShare = 0.25f;

        const float SettleShare = 0.4f;
        const float SettleRoomShare = 0.2f;
        const float SettleFit = 0.95f;
        const float SettleMinSec = 1.5f;
        const float SettleMaxSec = 4.5f;
        const float SlotDriftShare = 0.03f;
        const float BobShare = 0.012f;
        const float BobHz = 0.23f;
        const float SwayShare = 0.025f;
        const float SwayHz = 0.11f;
        const float RhythmSpread = 0.2f;

        readonly DuelPlane _plane;
        readonly Vector3 _lane;
        readonly float _bobHz;
        readonly float _swayHz;
        readonly List<Drift> _drifts = new List<Drift>();
        readonly Vector3[] _arc = new Vector3[ArcSteps + 1];

        Leg _leg;
        float _x;
        float _dir = 1f;
        float _at;
        float _arcLength = 1f;
        float _rollLength = 1f;
        float _rolled = float.MaxValue;
        float _want;
        bool _mayTurn = true;
        float _center;
        float _span = TurnEdgeShare;
        float _loopScale = 1f;
        float _origin;
        bool _locked;

        Vector3 _settleFrom;
        Vector3 _settleTo;
        Vector3 _settleVel;
        float _settleSec = 1f;
        Vector3 _holdAt;
        float _holdFacing = 1f;
        float _holdTime;

        bool _wantHold;
        Vector3 _slot;
        float _wantFacing = 1f;

        float _speed;
        float _travel;
        float _age;
        Quaternion _startVisual = Quaternion.identity;
        Vector3 _pos;
        Vector3 _forward = Vector3.right;
        Vector3 _up = Vector3.up;

        SkyPath(DuelPlane plane, Vector3 lane)
        {
            _plane = plane;
            _lane = lane;
            _bobHz = BobHz * Random.Range(1f - RhythmSpread, 1f + RhythmSpread);
            _swayHz = SwayHz * Random.Range(1f - RhythmSpread, 1f + RhythmSpread);
        }

        public DuelPlane Plane => _plane;

        public bool Alive => _plane != null && _plane.Scripted;

        public Vector3 Position => _pos;

        public Vector3 Forward => _forward;

        public Vector3 Up => _up;

        public float Origin { set => _origin = value; }

        public void Lock(float origin)
        {
            if (_locked) return;
            _x -= origin;
            _origin = origin;
            _locked = true;
        }

        public void Unlock()
        {
            if (!_locked) return;
            _x += _origin;
            _origin = 0f;
            _locked = false;
        }

        public float Heading => _leg == Leg.Settle || _leg == Leg.Hold ? _holdFacing : _dir;

        public bool MayTurn { set => _mayTurn = value; }

        public float LoopScale { set => _loopScale = Mathf.Max(0.1f, value); }

        public void TurnZone(float center, float span)
        {
            _center = center;
            _span = span;
        }

        public void ClearZone() => TurnZone(0f, TurnEdgeShare);

        public void Want(float dir) => _want = dir < 0f ? -1f : 1f;

        public void RequestHold(Vector3 slot, float facing)
        {
            _wantHold = true;
            _slot = slot;
            _wantFacing = facing < 0f ? -1f : 1f;
        }

        public void RequestRun() => _wantHold = false;

        public static SkyPath Enter(DuelPlane plane, SkyFrame frame, Vector3 lane, float facing, bool settled)
        {
            if (plane == null) return null;

            var path = new SkyPath(plane, lane);
            Vector3 at = plane.transform.position;
            Vector3 nose = plane.transform.right;
            float face = facing < 0f ? -1f : 1f;
            float dir = Mathf.Abs(nose.x) > 0.05f ? Mathf.Sign(nose.x) : face;

            path._startVisual = plane.Visual;
            path._up = plane.VisualUp;
            path._forward = nose;
            path._pos = at;
            plane.BeginScript();

            if (settled)
            {
                Vector3 local = frame.Local(at);
                path.RequestHold(local, face);
                path.BeginHold(local, dir);
            }
            else
            {
                path._speed = Mathf.Clamp(plane.Motion.x * dir, 0f, Pace(frame) * EntryPaceShare);
                path.BeginRun(at, dir, frame, false);
            }

            path.Tick(0f, frame);
            return path;
        }

        public static SkyPath Arrive(DuelPlane plane, SkyFrame frame, Vector3 lane, float dir, float lead)
        {
            if (plane == null) return null;

            var path = new SkyPath(plane, lane);
            float d = dir < 0f ? -1f : 1f;
            float entry = (EntryShare + Mathf.Max(0f, lead)) * frame.halfWidth;
            Vector3 at = frame.World(new Vector3(-d * entry, lane.y, lane.z));

            plane.BeginScript();
            path._age = BlendSec;
            path._speed = Pace(frame);
            path._pos = at;
            path._forward = new Vector3(d, 0f, 0f);
            path._up = Vector3.up;
            path.BeginRun(at, d, frame, false);
            path.Tick(0f, frame);
            return path;
        }

        public static float Pace(SkyFrame frame) => PaceShare * frame.halfWidth;

        static float Span(SkyFrame frame) => Mathf.Min(frame.halfWidth, frame.halfHeight * WideAspect);

        bool Rolled => _rolled >= _rollLength;

        public void Tick(float dt, SkyFrame frame)
        {
            if (!Alive) return;

            _age += dt;
            if (_leg == Leg.Run || _leg == Leg.Loop)
            {
                float step = (Pace(frame) - _speed) * (1f - Mathf.Exp(-SpeedResponse * dt));
                float cap = AccelShare * frame.halfWidth * dt;
                _speed += Mathf.Clamp(step, -cap, cap);
            }

            Advance(dt, frame);
            Pose(dt, frame);
        }

        void Advance(float dt, SkyFrame frame)
        {
            float width = frame.halfWidth;

            switch (_leg)
            {
                case Leg.Hold:
                {
                    _holdTime += dt;
                    if (!_wantHold || _wantFacing != _holdFacing)
                    {
                        Vector3 here = frame.World(_holdAt + HoldOffset(_holdTime, frame));
                        _speed = Mathf.Clamp(frame.camVelX * _holdFacing, 0f, Pace(frame));
                        BeginRun(here, _holdFacing, frame, false);
                        return;
                    }
                    _holdAt = Vector3.MoveTowards(_holdAt, _slot, SlotDriftShare * width * dt);
                    return;
                }

                case Leg.Settle:
                {
                    _at += dt;
                    if (!_wantHold)
                    {
                        Vector3 here = SettlePoint(_at, out Vector3 velocity);
                        _speed = Mathf.Clamp((velocity.x + frame.camVelX) * _holdFacing, 0f, Pace(frame));
                        BeginRun(frame.World(here), _holdFacing, frame, false);
                        return;
                    }
                    if (_at >= _settleSec) BeginHold(_settleTo, _holdFacing);
                    return;
                }

                case Leg.Run:
                {
                    if (_want == _dir) _want = 0f;
                    float u = _pos.x - frame.camX;

                    if (_wantHold && Rolled && _dir == _wantFacing && _dir * u <= SettleRoomShare * width)
                    {
                        BeginSettle(frame);
                        return;
                    }

                    float move = Step(new Vector3(_dir, 0f, 0f), dt, frame);
                    _at += move;
                    if (!Rolled) _rolled += move;

                    bool hard = _dir * u >= HardEdgeShare * width;
                    float side = _locked ? _pos.x - _origin : u - _center * width;
                    bool turn = (_want != 0f && _want != _dir)
                                || (_mayTurn && _dir * side >= _span * width);
                    bool ready = _speed >= LoopPaceShare * Pace(frame);
                    if (Rolled && (hard || (turn && ready))) BeginLoop(frame);
                    return;
                }

                default:
                {
                    _at += Step(LoopTangent(Mathf.Clamp01(_at / _arcLength)), dt, frame);
                    if (_at < _arcLength) return;

                    BeginRun(LoopBase(frame) + _arc[ArcSteps], -_dir, frame, true);
                    return;
                }
            }
        }

        float Step(Vector3 tangent, float dt, SkyFrame frame)
        {
            Vector3 slope = DriftSlope(frame);
            float stretch = Mathf.Max(0.2f, (tangent + slope).magnitude);
            float move = _speed * dt / stretch;
            _travel += move;
            return move;
        }

        Vector3 RunBase(SkyFrame frame) =>
            new Vector3(_origin + _x + _dir * _at, frame.anchorY + _lane.y, frame.depthZ + _lane.z);

        Vector3 LoopBase(SkyFrame frame) =>
            new Vector3(_origin + _x, frame.anchorY + _lane.y, frame.depthZ + _lane.z);

        void Pose(float dt, SkyFrame frame)
        {
            Vector3 pos;
            Vector3 fwd;
            Vector3 aim = Vector3.up;
            float roll = 0f;

            switch (_leg)
            {
                case Leg.Run:
                    pos = RunBase(frame) + DriftOffset(frame, _travel);
                    fwd = (new Vector3(_dir, 0f, 0f) + DriftSlope(frame)).normalized;
                    if (!Rolled) roll = 180f * (1f - Ease(_rolled / _rollLength));
                    PruneDrifts(frame);
                    break;

                case Leg.Loop:
                {
                    float t = Mathf.Clamp01(_at / _arcLength);
                    float f = t * ArcSteps;
                    int i = Mathf.Min((int)f, ArcSteps - 1);
                    pos = LoopBase(frame) + Vector3.Lerp(_arc[i], _arc[i + 1], f - i) + DriftOffset(frame, _travel);
                    fwd = (LoopTangent(t) + DriftSlope(frame)).normalized;
                    float theta = Mathf.PI * Ease(t);
                    aim = new Vector3(-_dir * Mathf.Sin(theta), Mathf.Cos(theta), 0f);
                    PruneDrifts(frame);
                    break;
                }

                case Leg.Settle:
                {
                    Vector3 local = SettlePoint(_at, out Vector3 velocity);
                    pos = frame.World(local);
                    fwd = Nose(velocity, frame);
                    break;
                }

                default:
                    pos = frame.World(_holdAt + HoldOffset(_holdTime, frame));
                    fwd = Nose(HoldVelocity(_holdTime, frame), frame);
                    break;
            }

            Vector3 target = Vector3.ProjectOnPlane(aim, fwd);
            if (target.sqrMagnitude < 1e-6f) target = Vector3.ProjectOnPlane(Vector3.up, fwd);
            if (target.sqrMagnitude < 1e-6f) target = _up;
            target.Normalize();
            if (roll != 0f) target = Quaternion.AngleAxis(roll, fwd) * target;

            Vector3 up = Vector3.ProjectOnPlane(_up, fwd);
            up = up.sqrMagnitude < 1e-6f ? target : up.normalized;
            float angle = Vector3.SignedAngle(up, target, fwd);
            float limit = RollRateDeg * dt;
            float turn = Mathf.Clamp(angle * (1f - Mathf.Exp(-UpResponse * dt)), -limit, limit);
            _up = Quaternion.AngleAxis(turn, fwd) * up;

            _pos = pos;
            _forward = fwd;

            Quaternion visual = Quaternion.LookRotation(Vector3.Cross(fwd, _up), _up);
            if (_age < BlendSec)
                visual = Quaternion.Slerp(_startVisual, visual, Mathf.SmoothStep(0f, 1f, _age / BlendSec));
            _plane.ScriptPose(pos, visual);
        }

        void BeginRun(Vector3 from, float dir, SkyFrame frame, bool rollOut)
        {
            var lane = new Vector3(from.x, frame.anchorY + _lane.y, frame.depthZ + _lane.z);
            AddDrift(from - lane);

            _leg = Leg.Run;
            _x = from.x - _origin;
            _dir = dir < 0f ? -1f : 1f;
            _at = 0f;
            _rollLength = RollShare * Span(frame);
            _rolled = rollOut ? 0f : float.MaxValue;
        }

        void BeginLoop(SkyFrame frame)
        {
            _x += _dir * _at;
            _leg = Leg.Loop;
            _at = 0f;
            _arcLength = LoopShare * _loopScale * Span(frame);
            BuildArc();
        }

        void BuildArc()
        {
            float step = _arcLength / ArcSteps;
            Vector3 last = LoopTangent(0f);
            _arc[0] = Vector3.zero;
            for (int i = 1; i <= ArcSteps; i++)
            {
                Vector3 next = LoopTangent((float)i / ArcSteps);
                _arc[i] = _arc[i - 1] + (last + next) * (0.5f * step);
                last = next;
            }
        }

        Vector3 LoopTangent(float t)
        {
            float theta = Mathf.PI * Ease(t);
            return new Vector3(_dir * Mathf.Cos(theta), Mathf.Sin(theta), 0f);
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        void AddDrift(Vector3 amp)
        {
            if (amp.sqrMagnitude < 1e-4f) return;
            _drifts.Add(new Drift { amp = amp, start = _travel });
        }

        Vector3 DriftOffset(SkyFrame frame, float travel)
        {
            float span = DriftShare * frame.halfWidth;
            Vector3 sum = Vector3.zero;
            foreach (Drift drift in _drifts)
            {
                float t = Mathf.Clamp01((travel - drift.start) / span);
                sum += drift.amp * (1f - t * t * t * (t * (6f * t - 15f) + 10f));
            }
            return sum;
        }

        Vector3 DriftSlope(SkyFrame frame)
        {
            float span = DriftShare * frame.halfWidth;
            Vector3 sum = Vector3.zero;
            foreach (Drift drift in _drifts)
            {
                float t = Mathf.Clamp01((_travel - drift.start) / span);
                sum -= drift.amp * (30f * t * t * (t - 1f) * (t - 1f) / span);
            }
            return sum;
        }

        void PruneDrifts(SkyFrame frame)
        {
            float span = DriftShare * frame.halfWidth;
            for (int i = _drifts.Count - 1; i >= 0; i--)
                if (_travel - _drifts[i].start >= span) _drifts.RemoveAt(i);
        }

        void BeginSettle(SkyFrame frame)
        {
            float reach = SettleShare * frame.halfWidth;
            Vector3 local = frame.Local(_pos);
            Vector3 velocity = _forward * _speed - new Vector3(frame.camVelX, 0f, 0f);
            velocity.x = Mathf.Max(0f, velocity.x * _dir) * _dir;
            float ahead = Mathf.Max(velocity.x * _dir, 1f);

            _leg = Leg.Settle;
            _holdFacing = _dir;
            _settleFrom = local;
            _settleTo = new Vector3(local.x + _dir * reach, _slot.y, _slot.z);
            _settleVel = velocity;
            _settleSec = Mathf.Clamp(SettleFit * 3f * reach / ahead, SettleMinSec, SettleMaxSec);
            _at = 0f;
            _drifts.Clear();
        }

        Vector3 SettlePoint(float time, out Vector3 velocity)
        {
            float span = _settleSec;
            float s = Mathf.Clamp01(time / span);
            float s2 = s * s;
            float s3 = s2 * s;

            float h00 = 2f * s3 - 3f * s2 + 1f;
            float h10 = s3 - 2f * s2 + s;
            float h01 = -2f * s3 + 3f * s2;
            float d00 = 6f * s2 - 6f * s;
            float d10 = 3f * s2 - 4f * s + 1f;
            float d01 = -6f * s2 + 6f * s;

            velocity = s >= 1f ? Vector3.zero
                : (d00 * _settleFrom + d10 * span * _settleVel + d01 * _settleTo) / span;
            return h00 * _settleFrom + h10 * span * _settleVel + h01 * _settleTo;
        }

        void BeginHold(Vector3 at, float facing)
        {
            _leg = Leg.Hold;
            _holdAt = at;
            _holdFacing = facing < 0f ? -1f : 1f;
            _holdTime = 0f;
            _speed = 0f;
            _drifts.Clear();
        }

        Vector3 HoldOffset(float time, SkyFrame frame) => new Vector3(
            SwayShare * frame.halfWidth * Mathf.Sin(2f * Mathf.PI * _swayHz * time),
            BobShare * frame.halfHeight * Mathf.Sin(2f * Mathf.PI * _bobHz * time),
            0f);

        Vector3 HoldVelocity(float time, SkyFrame frame) => new Vector3(
            SwayShare * frame.halfWidth * 2f * Mathf.PI * _swayHz * Mathf.Cos(2f * Mathf.PI * _swayHz * time),
            BobShare * frame.halfHeight * 2f * Mathf.PI * _bobHz * Mathf.Cos(2f * Mathf.PI * _bobHz * time),
            0f);

        Vector3 Nose(Vector3 velocity, SkyFrame frame) =>
            new Vector3(_holdFacing * Pace(frame), velocity.y, velocity.z).normalized;
    }
}
