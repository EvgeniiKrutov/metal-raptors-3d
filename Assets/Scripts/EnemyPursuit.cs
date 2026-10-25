using UnityEngine;

namespace MetalRaptors
{
    public class EnemyPursuit : MonoBehaviour
    {
        const float GapNear = 170f;
        const float GapFar = 330f;
        const float GapHard = 120f;
        const float GapHz = 0.07f;
        const float ArriveSlack = 90f;

        const float WeaveMin = 70f;
        const float WeaveMax = 110f;
        const float WeaveHzMin = 0.14f;
        const float WeaveHzMax = 0.22f;
        const float SideBias = 60f;
        const float OffsetResponse = 1.5f;

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

        EnemyController _enemy;
        Rigidbody _target;
        float _minY;
        float _maxY;

        Vector2 _pos;
        float _heading;
        float _turnRate;
        float _vx;
        float _cruise;
        float _offset;
        float _seed;

        float _weave;
        float _weaveHz;
        float _bias;
        bool _attacking;
        float _phaseLeft;

        public static EnemyPursuit Begin(EnemyController enemy, Rigidbody target, float minY,
            float maxY)
        {
            if (enemy == null) return null;

            var pursuit = enemy.gameObject.AddComponent<EnemyPursuit>();
            pursuit._enemy = enemy;
            pursuit._target = target;
            pursuit._minY = minY;
            pursuit._maxY = Mathf.Max(minY, maxY);
            pursuit._pos = enemy.transform.position;
            pursuit._seed = Random.Range(0f, 100f);
            pursuit._cruise = Mathf.Max(MinCruise, target != null ? target.linearVelocity.x : 0f);
            pursuit._vx = pursuit._cruise + CatchUpMax;
            pursuit._offset = target != null ? pursuit._pos.y - target.position.y : 0f;
            pursuit.Stalk();

            enemy.BeginScript();
            enemy.Exposed = true;
            enemy.Pursuing = true;
            enemy.ScriptTo(pursuit._pos, 0f);
            return pursuit;
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

            Vector2 player = _target != null ? (Vector2)_target.position : _pos;
            if (_target != null)
                _cruise = Mathf.Lerp(_cruise, Mathf.Max(MinCruise, _target.linearVelocity.x),
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
                float gap = Mathf.Lerp(GapNear, GapFar, Mathf.PerlinNoise(_seed, Time.time * GapHz));
                float slotX = player.x - gap;
                float along = slotX - _pos.x;

                _phaseLeft -= dt;
                if (_phaseLeft <= 0f)
                {
                    if (_attacking) Stalk();
                    else if (along < ArriveSlack) Attack();
                }

                float sway = Mathf.Sin((Time.time + _seed) * _weaveHz * 2f * Mathf.PI) * _weave;
                _offset = Mathf.Lerp(_offset, sway + _bias, 1f - Mathf.Exp(-OffsetResponse * dt));
                float slotY = Mathf.Clamp(player.y + _offset, _minY, _maxY);

                aim = _attacking
                    ? Mathf.Atan2(player.y - _pos.y, player.x - _pos.x)
                    : Mathf.Atan2(slotY - _pos.y, slotX + Lookahead - _pos.x);

                want = _cruise + Mathf.Clamp(along * CatchUp, -FallBackMax, CatchUpMax);
                float room = player.x - GapHard - _pos.x;
                if (room < 0f) want = Mathf.Min(want, _cruise + room * OvertakeBrake);
            }

            float climb = MaxClimbDeg * Mathf.Deg2Rad;
            Turn(Mathf.Clamp(aim, -climb, climb), dt);

            _vx = Mathf.Lerp(_vx, want, 1f - Mathf.Exp(-SpeedResponse * dt));
            float speed = _vx / Mathf.Max(MinTilt, Mathf.Cos(_heading));
            _pos += new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading)) * (speed * dt);

            if (_pos.y < _minY || _pos.y > _maxY)
            {
                _pos.y = Mathf.Clamp(_pos.y, _minY, _maxY);
                _heading = Mathf.MoveTowards(_heading, 0f, _enemy.MaxTurnRate * dt);
            }

            _enemy.ScriptSteer(new Vector3(_pos.x, _pos.y, 0f), _heading, dt);
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
            _weave = Random.Range(WeaveMin, WeaveMax);
            _weaveHz = Random.Range(WeaveHzMin, WeaveHzMax);
            _bias = Random.Range(-SideBias, SideBias);
        }

        void Attack()
        {
            _attacking = true;
            _phaseLeft = Random.Range(AttackMinSec, AttackMaxSec);
        }
    }
}
