using UnityEngine;

namespace MetalRaptors
{
    public class PatrolLamp : MonoBehaviour
    {
        const float SpeedShare = 0.9f;
        public const float BobHeight = 14f;
        const float BobPeriod = 5f;
        const float DipDeg = 7f, NodDeg = 10f, NodPeriod = 3.6f;
        public const float WeaveSlopeDeg = 30f;
        const float WeaveTurnSec = 0.6f;

        public const float LampReach = 720f;
        const float LampInner = 8f, LampOuter = 18f;
        const float LampSource = 3f;
        const float LampGlow = 6f;
        const float LampIntensity = 0.5f;
        const float LightRangeShare = 0.8f;
        const float LightIntensity = 1.8f;
        const int LightPriority = 55;
        const float SeeHalfDeg = 6f;
        const float LostMargin = 220f;

        static readonly Color BeamColor = new Color(1f, 0.9f, 0.72f);

        EnemyController _enemy;
        Searchlight _lamp;
        NightLight2D _light;
        Camera _cam;
        float _halfViewWidth;
        System.Action<EnemyController> _lost;

        Vector3 _pos;
        float _baseY;
        float _speed;
        float _course = -1f;
        float _weave;
        float _climb;
        float _vy;
        float _low, _high;
        float _heading = Mathf.PI;
        float _angle = 180f;
        float _clock;
        float _phase;
        float _health;
        int _formation;
        bool _alert;
        bool _hit;
        bool _out;

        public bool Aware => _alert;

        public EnemyController Enemy => _enemy;

        public int Formation => _formation;

        public float Course => _course;

        public static PatrolLamp Mount(EnemyController enemy, float flySpeed, float halfViewWidth,
            System.Action<EnemyController> lost, int formation, float phase)
        {
            if (enemy == null) return null;

            var patrol = enemy.gameObject.AddComponent<PatrolLamp>();
            patrol._enemy = enemy;
            patrol._speed = Mathf.Max(1f, flySpeed) * SpeedShare;
            patrol._halfViewWidth = halfViewWidth;
            patrol._lost = lost;
            patrol._cam = Camera.main;
            patrol._pos = enemy.transform.position;
            patrol._baseY = patrol._pos.y;
            patrol._phase = phase;
            patrol._formation = formation;
            patrol._health = enemy.CurrentHealth;

            enemy.BeginScript();
            enemy.Exposed = true;
            enemy.ScriptTo(patrol._pos, patrol._heading);
            patrol.BuildLamp();
            return patrol;
        }

        public void Overtake(float speed)
        {
            _course = 1f;
            _speed = Mathf.Max(1f, speed);
            _heading = 0f;
            _angle = 0f;
            _enemy.ScriptTo(_pos, _heading);
        }

        public void Weave(float dir, float low, float high)
        {
            _weave = dir < 0f ? -1f : 1f;
            _low = low;
            _high = high;
            _climb = _speed * Mathf.Tan(WeaveSlopeDeg * Mathf.Deg2Rad);
            _vy = _weave * _climb;
            _heading = Mathf.Atan2(_vy, _course * _speed);
            _enemy.ScriptTo(_pos, _heading);
        }

        void BuildLamp()
        {
            Vector3 nose = transform.InverseTransformPoint(_enemy.NosePoint);

            _lamp = Searchlight.Mount(transform, nose);
            if (_lamp != null)
            {
                _lamp.innerAngle = LampInner;
                _lamp.outerAngle = LampOuter;
                _lamp.length = LampReach;
                _lamp.sourceRadius = LampSource;
                _lamp.glowSize = LampGlow;
                _lamp.intensity = LampIntensity;
            }

            var cone = new GameObject("Patrol Light");
            cone.transform.SetParent(transform, false);
            cone.transform.localPosition = nose;
            _light = cone.AddComponent<NightLight2D>();
            _light.type = NightLightType.Cone;
            _light.color = BeamColor;
            _light.intensity = LightIntensity;
            _light.range = LampReach * LightRangeShare;
            _light.innerAngle = LampInner;
            _light.outerAngle = LampOuter;
            _light.sourceRadius = LampSource;
            _light.localDirection = Vector3.right;
            _light.priority = LightPriority;
            _light.castShadows = true;
            _light.shadowNear = PlaneSearchlight.SelfShadowNear;
            _light.beam = _lamp;
        }

        public bool Sees(Vector3 point, float radius)
        {
            if (_alert || _out || _enemy == null || !_enemy.IsAlive) return false;

            Vector2 to = (Vector2)point - (Vector2)_enemy.NosePoint;
            float distance = to.magnitude;
            if (distance < 1f || distance > LampReach) return false;

            float off = Mathf.Abs(Mathf.DeltaAngle(_angle, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg));
            return off <= SeeHalfDeg + Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
        }

        public bool ConsumeHit()
        {
            bool hit = _hit;
            _hit = false;
            return hit;
        }

        public void Alert()
        {
            if (_alert || _enemy == null || !_enemy.IsAlive) return;

            _alert = true;
            _enemy.Exposed = false;
            _enemy.EndScript();
        }

        void Update()
        {
            if (_out) return;

            if (_enemy == null || !_enemy.IsAlive)
            {
                GoDark();
                return;
            }

            float dt = Time.deltaTime;
            if (dt > 0f && !_alert) Cruise(dt);

            if (!_alert && _enemy.CurrentHealth < _health)
            {
                _hit = true;
                Alert();
            }
            _health = _enemy.CurrentHealth;

            if (_alert) Point(_enemy.Heading);
            else if (_weave != 0f) Aim(_heading * Mathf.Rad2Deg);
            else Aim(_course < 0f ? 180f : 0f);
        }

        void Cruise(float dt)
        {
            _clock += dt;

            _pos.x += _course * _speed * dt;
            if (_weave != 0f) Swerve(dt);
            else Bob();
            _enemy.ScriptSteer(_pos, _heading, dt);

            if (_cam == null) return;
            float camX = _cam.transform.position.x;
            bool gone = _course < 0f
                ? _pos.x < camX - _halfViewWidth - LostMargin
                : _pos.x > camX + _halfViewWidth + LostMargin;
            if (gone) _lost?.Invoke(_enemy);
        }

        void Bob()
        {
            float rise = BobHeight * Mathf.Sin(_clock * Mathf.PI * 2f / BobPeriod + _phase);
            float climb = BobHeight * Mathf.PI * 2f / BobPeriod
                          * Mathf.Cos(_clock * Mathf.PI * 2f / BobPeriod + _phase);

            _pos.y = _baseY + rise;
            _heading = Mathf.Atan2(climb, _course * _speed);
        }

        void Swerve(float dt)
        {
            float brake = _climb * WeaveTurnSec * 0.25f;
            if (_weave > 0f && _pos.y >= _high - brake) _weave = -1f;
            else if (_weave < 0f && _pos.y <= _low + brake) _weave = 1f;

            _vy = Mathf.MoveTowards(_vy, _weave * _climb, 2f * _climb / WeaveTurnSec * dt);
            _pos.y += _vy * dt;
            _heading = Mathf.Atan2(_vy, _course * _speed);
        }

        void Point(float heading)
        {
            float side = Mathf.Cos(heading) >= 0f ? 1f : -1f;
            Aim(heading * Mathf.Rad2Deg - side * DipDeg
                + NodDeg * Mathf.Sin(_clock * Mathf.PI * 2f / NodPeriod));
        }

        void Aim(float angle)
        {
            _angle = angle;
            float radians = _angle * Mathf.Deg2Rad;
            if (_lamp != null) _lamp.worldDirection = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
            if (_light != null) _light.transform.rotation = Quaternion.Euler(0f, 0f, _angle);
        }

        void GoDark()
        {
            _out = true;
            if (_lamp != null) _lamp.Fail();
            if (_light != null) _light.enabled = false;
        }
    }
}
