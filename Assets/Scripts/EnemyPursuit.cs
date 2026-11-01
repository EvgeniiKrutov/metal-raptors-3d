using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public struct PursuitBand
    {
        public float minY, maxY, midY, spacing;
    }

    public class EnemyPursuit : MonoBehaviour
    {
        const float GapNear = 170f;
        const float GapFar = 330f;
        const float GapHard = 120f;
        const float GapHz = 0.07f;
        const float ArriveSlack = 90f;
        const float ReachSlope = 0.3f;

        const float LegMinSec = 3f;
        const float LegMaxSec = 5f;
        const float LegStepMin = 0.8f;
        const float LegStepMax = 1.2f;
        const float LegResponse = 0.9f;
        const float LegSway = 18f;
        const float SwayHzMin = 0.14f;
        const float SwayHzMax = 0.22f;
        const float HighLift = 0.3f;
        const float HighDip = 0.4f;

        const float StalkMinSec = 2.5f;
        const float StalkMaxSec = 4.5f;
        const float AttackMinSec = 2f;
        const float AttackMaxSec = 3.5f;

        const float Lookahead = 150f;
        const float MaxClimbDeg = 40f;
        const float TurnLag = 0.35f;
        const float TurnResponse = 6f;

        const float MinCruise = 120f;
        const float CruiseResponse = 2f;
        const float CatchUp = 1.2f;
        const float CatchUpMax = 130f;
        const float FallBackMax = 70f;
        const float OvertakeBrake = 2f;
        const float SpeedResponse = 2.5f;
        const float MinTilt = 0.5f;

        const float RetreatDeg = -14f;
        const float RetreatSlow = 90f;

        const float LeaveSec = 1.6f;

        const float ModeMinSec = 6f;
        const float ModeMaxSec = 10f;
        const int ModeRepeatMax = 2;
        const float UnderTop = 0.4f;
        const float UnderBottom = 1f;
        const float UnderLegMove = 0.35f;
        const float UnderSlack = 10f;
        const float RefResponse = 0.6f;

        static readonly List<EnemyPursuit> Active = new List<EnemyPursuit>();

        EnemyController _enemy;
        Rigidbody _target;
        float _dir = 1f;
        float _minY;
        float _maxY;
        float _midY;
        float _spacing;
        bool _high;

        Vector2 _pos;
        float _heading;
        float _turnRate;
        float _vx;
        float _cruise;
        float _seed;

        float _legY;
        float _slotY;
        float _legLeft;
        float _swayHz;
        bool _attacking;
        float _phaseLeft;
        bool _arrived;

        bool _mixed;
        bool _under;
        float _modeLeft;
        int _streak;
        float _refY;
        float _depth;

        bool _leaving;
        float _leaveFrom;
        float _leaveTo;
        float _leaveT;
        float _leaveSpeed;

        public static EnemyPursuit Begin(EnemyController enemy, Rigidbody target, PursuitBand band,
            bool sneak = false, bool high = false, float facing = 1f)
        {
            if (enemy == null) return null;

            var pursuit = enemy.gameObject.AddComponent<EnemyPursuit>();
            pursuit._enemy = enemy;
            pursuit._target = target;
            pursuit._dir = facing < 0f ? -1f : 1f;
            pursuit._minY = band.minY;
            pursuit._maxY = Mathf.Max(band.minY, band.maxY);
            pursuit._midY = band.midY;
            pursuit._spacing = Mathf.Max(1f, band.spacing);
            pursuit._high = high;
            pursuit._pos = pursuit.Mirror(enemy.transform.position);
            pursuit._seed = Random.Range(0f, 100f);
            pursuit._cruise = Mathf.Max(MinCruise,
                target != null ? target.linearVelocity.x * pursuit._dir : 0f);
            pursuit._vx = pursuit._cruise + CatchUpMax;
            pursuit._slotY = pursuit._pos.y;
            pursuit._refY = target != null ? target.position.y : pursuit._pos.y;
            pursuit._mixed = sneak;
            pursuit._legLeft = Random.Range(LegMinSec, LegMaxSec);
            if (sneak && Random.value < 0.5f && !pursuit.UnderTaken()) pursuit.Duck();
            else
            {
                float low = pursuit.LegFloor;
                pursuit._legY = Apart(Mathf.Clamp(pursuit._pos.y, low, pursuit._maxY), low,
                    pursuit._maxY, pursuit._spacing, pursuit);
            }
            pursuit._streak = 1;
            pursuit._modeLeft = Random.Range(ModeMinSec, ModeMaxSec);
            pursuit.Stalk();
            Active.Add(pursuit);

            enemy.BeginScript();
            enemy.Exposed = true;
            enemy.Pursuing = true;
            enemy.HoldFire = true;
            enemy.ScriptTo(pursuit.World(pursuit._pos), pursuit.WorldHeading(0f));
            return pursuit;
        }

        Vector2 Mirror(Vector3 point) => new Vector2(point.x * _dir, point.y);

        Vector3 World(Vector2 point) => new Vector3(point.x * _dir, point.y, 0f);

        float WorldHeading(float heading) => _dir > 0f ? heading : Mathf.PI - heading;

        bool High => _high || (_mixed && !_under);

        float LegFloor => High ? Mathf.Min(_maxY, _midY + _spacing * HighLift) : _minY;

        float HoldFloor => High ? Mathf.Clamp(_midY - _spacing * HighDip, _minY, _maxY) : _minY;

        float UnderCeiling => Mathf.Clamp(_refY - _spacing * UnderTop, _minY, _maxY);

        public static float Apart(float y, float low, float high, float spacing,
            EnemyPursuit self = null)
        {
            foreach (EnemyPursuit other in Active)
            {
                if (other == null || other == self) continue;

                float taken = other._legY;
                if (Mathf.Abs(y - taken) >= spacing) continue;

                float up = taken + spacing, down = taken - spacing;
                bool upFits = up <= high, downFits = down >= low;
                if (upFits && (!downFits || Mathf.Abs(up - y) <= Mathf.Abs(down - y))) y = up;
                else if (downFits) y = down;
            }
            return Mathf.Clamp(y, low, high);
        }

        void OnDestroy() => Active.Remove(this);

        public void Leave()
        {
            if (_leaving) return;

            _leaving = true;
            _under = false;
            _leaveT = 0f;
            _leaveFrom = _heading;
            _leaveTo = _maxY - _pos.y >= _pos.y - _minY ? Mathf.PI : -Mathf.PI;
            _leaveSpeed = Mathf.Max(MinCruise, _cruise);
        }

        void Depart(float dt)
        {
            _leaveT = Mathf.Min(1f, _leaveT + dt / LeaveSec);
            _heading = Mathf.Lerp(_leaveFrom, _leaveTo, Mathf.SmoothStep(0f, 1f, _leaveT));

            _pos += new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading)) * (_leaveSpeed * dt);
            _pos.y = Mathf.Clamp(_pos.y, _minY, _maxY);

            _enemy.ScriptSteer(World(_pos), WorldHeading(_heading), dt);
        }

        void Update()
        {
            if (_enemy == null || !_enemy.IsAlive)
            {
                Destroy(this);
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_leaving)
            {
                Depart(dt);
                return;
            }

            Vector2 player = _target != null ? Mirror(_target.position) : _pos;
            if (_target != null)
                _cruise = Mathf.Lerp(_cruise, Mathf.Max(MinCruise, _target.linearVelocity.x * _dir),
                    1f - Mathf.Exp(-CruiseResponse * dt));

            float aim;
            float want;

            if (_enemy.StandingDown || _target == null)
            {
                aim = RetreatDeg * Mathf.Deg2Rad;
                want = _cruise - RetreatSlow;
            }
            else
            {
                if (_mixed) Shift(player.y, dt);
                Vector2 slot = Weave(player, dt);
                float along = slot.x - _pos.x;
                if (!_arrived && along < ArriveSlack)
                {
                    _arrived = true;
                    _enemy.HoldFire = false;
                }

                bool strike = _attacking && (!_under || _pos.y <= UnderCeiling + UnderSlack);
                aim = strike
                    ? Mathf.Atan2(player.y - _pos.y, player.x - _pos.x)
                    : Mathf.Atan2(slot.y - _pos.y, slot.x + Lookahead - _pos.x);
                if (_under && strike) aim = Mathf.Max(aim, 0f);

                want = _cruise + Mathf.Clamp(along * CatchUp, -FallBackMax, CatchUpMax);
                float room = player.x - GapHard - _pos.x;
                if (room < 0f) want = Mathf.Min(want, _cruise + room * OvertakeBrake);
            }

            float climb = MaxClimbDeg * Mathf.Deg2Rad;
            Turn(Mathf.Clamp(aim, -climb, climb), dt);

            _vx = Mathf.Lerp(_vx, want, 1f - Mathf.Exp(-SpeedResponse * dt));
            float speed = _vx / Mathf.Max(MinTilt, Mathf.Cos(_heading));
            float lastY = _pos.y;
            _pos += new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading)) * (speed * dt);
            if (_under) _pos.y = Mathf.Min(_pos.y, Mathf.Max(lastY, UnderCeiling));

            float floor = Mathf.Min(HoldFloor, lastY);
            if (_pos.y < floor || _pos.y > _maxY)
            {
                _pos.y = Mathf.Clamp(_pos.y, floor, _maxY);
                _heading = Mathf.MoveTowards(_heading, 0f, _enemy.MaxTurnRate * dt);
            }

            _enemy.ScriptSteer(World(_pos), WorldHeading(_heading), dt);
        }

        Vector2 Weave(Vector2 player, float dt)
        {
            float gap = Mathf.Lerp(GapNear, GapFar, Mathf.PerlinNoise(_seed, Time.time * GapHz));
            float slotX = player.x - gap;

            _phaseLeft -= dt;
            if (_phaseLeft <= 0f)
            {
                if (_attacking) Stalk();
                else if (slotX - _pos.x < ArriveSlack) Attack();
            }

            _legLeft -= dt;
            if (_legLeft <= 0f) NextLeg();
            if (_under) _legY = _refY - _depth;

            _slotY = Mathf.Lerp(_slotY, _legY, 1f - Mathf.Exp(-LegResponse * dt));
            float sway = Mathf.Sin((Time.time + _seed) * _swayHz * 2f * Mathf.PI) * LegSway;
            if (_under) return new Vector2(slotX, Mathf.Clamp(_slotY + sway, HoldFloor, UnderCeiling));

            float slotY = Mathf.Max(_slotY + sway, player.y - gap * ReachSlope);
            return new Vector2(slotX, Mathf.Clamp(slotY, HoldFloor, _maxY));
        }

        void NextLeg()
        {
            _legLeft = Random.Range(LegMinSec, LegMaxSec);

            if (_under)
            {
                float top = _spacing * UnderTop, bottom = _spacing * UnderBottom;
                float move = (bottom - top) * UnderLegMove;
                float higher = _depth - move, lower = _depth + move;
                bool higherFits = higher >= top, lowerFits = lower <= bottom;
                _depth = higherFits && (!lowerFits || Random.value < 0.5f)
                    ? Random.Range(top, higher)
                    : Random.Range(Mathf.Min(lower, bottom), bottom);
                return;
            }

            float low = LegFloor;
            float step = _spacing * Random.Range(LegStepMin, LegStepMax);
            float y = _legY + (Random.value < 0.5f ? step : -step);
            if (y > _maxY) y = _legY - step;
            else if (y < low) y = _legY + step;
            _legY = Apart(Mathf.Clamp(y, low, _maxY), low, _maxY, _spacing, this);
        }

        void Turn(float aim, float dt)
        {
            float maxRate = Mathf.Max(0.1f, _enemy.MaxTurnRate);
            float error = Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, aim * Mathf.Rad2Deg)
                          * Mathf.Deg2Rad;
            float desired = Mathf.Clamp(error / TurnLag, -maxRate, maxRate);

            _turnRate = Mathf.Lerp(_turnRate, desired, 1f - Mathf.Exp(-TurnResponse * dt));
            _heading += _turnRate * dt;
        }

        void Stalk()
        {
            _attacking = false;
            _phaseLeft = Random.Range(StalkMinSec, StalkMaxSec);
            _swayHz = Random.Range(SwayHzMin, SwayHzMax);
        }

        void Attack()
        {
            _attacking = true;
            _phaseLeft = Random.Range(AttackMinSec, AttackMaxSec);
        }

        void Shift(float playerY, float dt)
        {
            if (!GunnerAutopilot.Diving)
                _refY = Mathf.Lerp(_refY, playerY, 1f - Mathf.Exp(-RefResponse * dt));

            _modeLeft -= dt;
            if (_modeLeft > 0f) return;

            _modeLeft = Random.Range(ModeMinSec, ModeMaxSec);
            bool under = Random.value < 0.5f;
            if (under == _under && _streak >= ModeRepeatMax) under = !under;
            if (under && !_under && UnderTaken()) under = false;

            if (under == _under)
            {
                _streak++;
                return;
            }

            _streak = 1;
            if (under) Duck();
            else Rise();
            Stalk();
        }

        bool UnderTaken()
        {
            foreach (EnemyPursuit other in Active)
                if (other != null && other != this && other._under) return true;
            return false;
        }

        void Duck()
        {
            _under = true;
            _depth = _spacing * Random.Range(UnderTop, UnderBottom);
            _legY = _refY - _depth;
            _legLeft = Random.Range(LegMinSec, LegMaxSec);
        }

        void Rise()
        {
            _under = false;
            float low = LegFloor;
            _legY = Apart(Random.Range(low, _maxY), low, _maxY, _spacing, this);
            _legLeft = Random.Range(LegMinSec, LegMaxSec);
        }
    }
}
