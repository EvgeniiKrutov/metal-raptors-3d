using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class BossStage
    {
        public Vector3 camPos;
        public float halfW;
        public float halfH;
        public float floorY;
        public float ceilingY;
        public float playerSpeed;
        public Transform player;
        public Transform hud;
        public HudCurtain curtain;
        public SoundSystem sound;
        public CampaignEnemies enemies;
        public PlaneModelConfig escort;
        public float scrollSpeed;
        public System.Func<bool> over;
        public System.Func<Vector2, float, EnemyController> respawn;
        public System.Action<float> pin;
    }

    public class RavenMoveset : MonoBehaviour
    {
        const float GapSec = 1f;
        const float WarnSec = 1f;
        const float Offscreen = 140f;
        const float LanePad = 40f;

        const float LeaveSpeed = 364f;
        const float LeaveAccel = 420f;
        const float LeaveTurnDeg = 140f;

        const float TrailSec = 7f;
        const float TrailHoldX = -0.72f;
        const float TrailEnterSpeed = 182f;
        const float TrailClimbFactor = 0.55f;
        const float TrailResponse = 3f;
        const float TrailFireSec = 0.15f;
        const float TrailBulletSpeed = 220f;
        const float TrailDamage = 6f;

        const float OvertakeMaxSec = 5f;
        const float OvertakeStartSpeed = 105f;
        const float OvertakeTopSpeed = 224f;
        const float OvertakeAccel = 154f;
        const float OvertakeBrake = 182f;
        const float OvertakeLane = 70f;
        const float OvertakeShiftRange = 260f;
        const float OvertakePassGap = 40f;

        const float StallSec = 2f;

        const float SmokeStartSpeed = 224f;
        const float SmokeTopSpeed = 392f;
        const float SmokeAccel = 490f;
        const float SmokeBrake = 364f;
        const float SmokeHoldPad = 80f;
        const float SmokeWeaveSec = 8f;
        const float SmokeClimb = 110f;
        const float SmokeSettle = 4f;
        const float SmokeDamage = 20f;

        const float WallEdgePad = 6f;
        const float WallPinGap = 10f;
        const float WallSeam = 2f;
        const float WallEnterSec = 1.3f;
        const float WallHoldSec = 1f;
        const float WallSqueezeSec = 4f;
        const float WallFireSec = 0.08f;
        const float WallBulletSpeed = 400f;
        const float WallDamage = 8f;

        enum Move { Trail, Smoke, Wall }

        static readonly Move[] Moves = { Move.Trail, Move.Smoke, Move.Wall };

        readonly List<Move> _bag = new List<Move>();
        Move? _last;

        EnemyController _boss;
        EnemyController _upper;
        EnemyController _lower;
        BossStage _stage;
        SmokeRibbon _smoke;

        Vector2 _pos;
        float _heading;
        float _speed;
        float _climb;

        public static RavenMoveset Begin(GameObject owner, EnemyController boss, BossStage stage)
        {
            if (boss == null || stage == null) return null;

            var moves = owner.AddComponent<RavenMoveset>();
            moves._boss = boss;
            moves._stage = stage;
            moves.StartCoroutine(moves.Run());
            return moves;
        }

        public void Stop()
        {
            StopAllCoroutines();
            Unpin();
            FadeSmoke();
            Throttle(false);

            if (_upper != null) _upper.EndScript();
            if (_lower != null) _lower.EndScript();
            _upper = null;
            _lower = null;

            if (_boss == null) return;
            _boss.Sputter(false);
            _boss.EndScript();
        }

        bool Live => _boss != null && _boss.IsAlive && (_stage.over == null || !_stage.over());

        float Left => _stage.camPos.x - _stage.halfW;
        float Right => _stage.camPos.x + _stage.halfW;
        float Bottom => _stage.camPos.y - _stage.halfH;

        Vector2 Player => _stage.player != null
            ? (Vector2)_stage.player.position
            : (Vector2)_stage.camPos;

        float ViewY(float y) => (y - Bottom) / (2f * _stage.halfH);

        static Vector2 Dir(float heading) => new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));

        IEnumerator Run()
        {
            Grab();
            _boss.Exposed = true;
            yield return Leave();

            while (Live)
            {
                yield return Pause(GapSec);
                if (!Live) yield break;

                switch (Next())
                {
                    case Move.Trail:
                        yield return Trail();
                        break;

                    case Move.Smoke:
                        yield return Smoke();
                        break;

                    default:
                        yield return Wall();
                        break;
                }
            }
        }

        Move Next()
        {
            if (_bag.Count == 0) Refill();

            Move move = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            _last = move;
            return move;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void Refill()
        {
            _bag.AddRange(Moves);
            Shuffle(_bag);

            int top = _bag.Count - 1;
            if (_last.HasValue && top > 0 && _bag[top] == _last.Value)
                (_bag[0], _bag[top]) = (_bag[top], _bag[0]);
        }

        IEnumerator Pause(float seconds)
        {
            for (float t = 0f; t < seconds && Live; t += Time.deltaTime) yield return null;
        }

        void Warn(ScreenEdge edge, System.Func<float> along)
        {
            if (_stage.hud == null) return;

            DangerSign sign = DangerSign.Show(_stage.hud, edge, WarnSec, along);
            if (_stage.curtain != null) _stage.curtain.Adopt(sign.gameObject);
        }

        void Grab()
        {
            Rigidbody body = _boss.Body;
            _speed = body != null && !body.isKinematic
                ? ((Vector2)body.linearVelocity).magnitude
                : 0f;

            _boss.BeginScript();
            _pos = _boss.transform.position;
            _heading = _boss.Heading;
        }

        void Place() => _boss.ScriptSteer(_pos, _heading, Time.deltaTime);

        void Arrive(Vector2 at, float heading)
        {
            EnemyController fresh = _stage.respawn != null ? _stage.respawn(at, heading) : null;
            if (fresh != null) _boss = fresh;

            _boss.Exposed = true;
            _pos = at;
            _heading = heading;
            _boss.ScriptTo(at, heading);
        }

        void Throttle(bool on)
        {
            if (_boss != null) _boss.Throttling = on;
        }

        void Pin(float x) => _stage.pin?.Invoke(x);

        void Unpin() => _stage.pin?.Invoke(float.NegativeInfinity);

        static bool Ready(ref float timer, float interval, float dt)
        {
            timer -= dt;
            if (timer > 0f) return false;

            timer += interval;
            return true;
        }

        IEnumerator Leave()
        {
            Throttle(true);
            while (Live && _pos.x < Right + Offscreen)
            {
                float dt = Time.deltaTime;
                _speed = Mathf.MoveTowards(_speed, LeaveSpeed, LeaveAccel * dt);
                _heading = Mathf.MoveTowardsAngle(_heading * Mathf.Rad2Deg, 0f, LeaveTurnDeg * dt)
                           * Mathf.Deg2Rad;
                _pos += Dir(_heading) * (_speed * dt);
                _pos.y = Mathf.Clamp(_pos.y, _stage.floorY, _stage.ceilingY);
                Place();
                yield return null;
            }
            Throttle(false);
        }

        void Follow(float y, float maxSpeed, float dt)
        {
            float want = Mathf.Clamp((y - _pos.y) * TrailResponse, -maxSpeed, maxSpeed);
            _climb = Mathf.MoveTowards(_climb, want, maxSpeed * TrailResponse * dt);
            _pos.y = Mathf.Clamp(_pos.y + _climb * dt, _stage.floorY, _stage.ceilingY);
        }

        IEnumerator Trail()
        {
            Warn(ScreenEdge.Left, () => ViewY(Player.y));
            yield return Pause(WarnSec);
            if (!Live) yield break;

            _climb = 0f;
            Arrive(new Vector2(Left - Offscreen, Player.y), 0f);

            float holdX = _stage.camPos.x + _stage.halfW * TrailHoldX;
            float climb = _stage.playerSpeed * TrailClimbFactor;
            float fire = TrailFireSec;

            for (float t = 0f; t < TrailSec && Live; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                _pos.x = Mathf.MoveTowards(_pos.x, holdX, TrailEnterSpeed * dt);
                Follow(Player.y, climb, dt);
                Place();

                if (_pos.x > Left && Ready(ref fire, TrailFireSec, dt))
                    _boss.Fire(TrailBulletSpeed, TrailDamage);
                yield return null;
            }

            float stopX = _stage.camPos.x;
            float midY = Mathf.Clamp(_stage.camPos.y, _stage.floorY, _stage.ceilingY)
                         - (_boss.BodyLow + _boss.BodyHigh) * 0.5f;
            float speed = OvertakeStartSpeed;
            float laneY = 0f;
            bool shifted = false;
            bool passed = false;

            Throttle(true);
            for (float t = 0f; t < OvertakeMaxSec && Live; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                Vector2 player = Player;

                float left = Mathf.Max(0f, stopX - _pos.x);
                float cap = Mathf.Min(OvertakeTopSpeed, Mathf.Sqrt(2f * OvertakeBrake * left));
                speed = Mathf.MoveTowards(speed, cap,
                    (cap > speed ? OvertakeAccel : OvertakeBrake) * dt);
                _pos.x = Mathf.Min(stopX, _pos.x + speed * dt);

                passed |= _pos.x > player.x + OvertakePassGap;
                if (!passed && !shifted && _pos.x > player.x - OvertakeShiftRange)
                {
                    shifted = true;
                    float side = player.y + OvertakeLane < _stage.ceilingY - LanePad ? 1f : -1f;
                    laneY = player.y + side * OvertakeLane;
                }

                bool arrived = left <= 1f;
                float targetY = passed || arrived ? midY : shifted ? laneY : player.y;
                Follow(targetY, climb, dt);
                Place();

                if (!shifted && !passed && Ready(ref fire, TrailFireSec, dt))
                    _boss.Fire(TrailBulletSpeed, TrailDamage);
                if (arrived && Mathf.Abs(_pos.y - targetY) < 2f) break;
                yield return null;
            }
            Throttle(false);
            if (!Live) yield break;

            _boss.Sputter(true);
            if (_stage.sound != null) _stage.sound.PlayStutter();

            yield return Pause(StallSec);

            _boss.Sputter(false);
            _speed = 0f;
            yield return Leave();
        }

        IEnumerator Smoke()
        {
            float low = _stage.floorY;
            float high = _stage.ceilingY;

            Warn(ScreenEdge.Left, () => ViewY(Mathf.Clamp(Player.y, low, high)));
            yield return Pause(WarnSec);
            if (!Live) yield break;

            _climb = 0f;
            Arrive(new Vector2(Left - Offscreen, Mathf.Clamp(Player.y, low, high)), 0f);
            _smoke = SmokeRibbon.Lay(_stage.player, _stage.scrollSpeed, Left,
                _boss.transform.position.z, SmokeDamage);

            float holdX = Right - SmokeHoldPad;
            float speed = SmokeStartSpeed;
            Throttle(true);
            while (Live)
            {
                float dt = Time.deltaTime;
                float left = Mathf.Max(0f, holdX - _pos.x);
                if (left <= 1f) break;

                float cap = Mathf.Min(SmokeTopSpeed, Mathf.Sqrt(2f * SmokeBrake * left));
                speed = Mathf.MoveTowards(speed, cap, (cap > speed ? SmokeAccel : SmokeBrake) * dt);
                _pos.x = Mathf.Min(holdX, _pos.x + speed * dt);
                PlaceSmoking();
                yield return null;
            }
            Throttle(false);

            float target = Random.value < 0.5f ? high : low;

            for (float t = 0f; t < SmokeWeaveSec && Live; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                if (Mathf.Abs(target - _pos.y) < SmokeSettle) target = target == high ? low : high;

                Follow(target, SmokeClimb, dt);
                _heading = Mathf.Atan2(_climb, Mathf.Max(1f, _stage.scrollSpeed));
                PlaceSmoking();
                yield return null;
            }

            FadeSmoke();
            if (!Live) yield break;

            _speed = 0f;
            yield return Leave();
        }

        void PlaceSmoking()
        {
            Place();
            if (_smoke != null) _smoke.Feed(_pos - Dir(_heading) * _boss.BodyBack);
        }

        void FadeSmoke()
        {
            if (_smoke != null) _smoke.Fade();
            _smoke = null;
        }

        IEnumerator Wall()
        {
            float top = _stage.ceilingY;
            float bottom = _stage.floorY;
            float mid = (top + bottom) * 0.5f;

            Warn(ScreenEdge.Left, () => ViewY(top));
            Warn(ScreenEdge.Left, () => ViewY(mid));
            Warn(ScreenEdge.Left, () => ViewY(bottom));
            yield return Pause(WarnSec);
            if (!Live) yield break;

            float startX = Left - Offscreen;

            _upper = _stage.enemies.SpawnEscort(_stage.escort, new Vector2(startX, top));
            _lower = _stage.enemies.SpawnEscort(_stage.escort, new Vector2(startX, bottom));
            Arrive(new Vector2(startX, mid), 0f);
            _boss.Exposed = false;

            float back = Mathf.Max(_boss.BodyBack, Mathf.Max(Back(_upper), Back(_lower)));
            float front = Mathf.Max(_boss.BodyFront, Mathf.Max(Front(_upper), Front(_lower)));
            float holdX = Left + WallEdgePad + back;

            float center = mid - (_boss.BodyLow + _boss.BodyHigh) * 0.5f;
            float upperEnd = _upper != null
                ? center + _boss.BodyHigh + WallSeam - _upper.BodyLow
                : top;
            float lowerEnd = _lower != null
                ? center + _boss.BodyLow - WallSeam - _lower.BodyHigh
                : bottom;

            for (float t = 0f; t < WallEnterSec && Live; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / WallEnterSec), 2f);
                float x = Mathf.Lerp(startX, holdX, k);
                PlaceWall(x, top, center, bottom);
                Pin(x + front + WallPinGap);
                yield return null;
            }

            Pin(holdX + front + WallPinGap);

            float fire = 0f;
            for (float t = 0f; t < WallHoldSec && Live; t += Time.deltaTime)
            {
                PlaceWall(holdX, top, center, bottom);
                if (Ready(ref fire, WallFireSec, Time.deltaTime)) FireWall();
                yield return null;
            }

            for (float t = 0f; t < WallSqueezeSec && Live; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / WallSqueezeSec);
                PlaceWall(holdX, Mathf.Lerp(top, upperEnd, k), center,
                    Mathf.Lerp(bottom, lowerEnd, k));
                if (Ready(ref fire, WallFireSec, Time.deltaTime)) FireWall();
                yield return null;
            }

            Unpin();

            _speed = 0f;
            float wallX = holdX;
            Throttle(true);
            while (Live && wallX < Right + Offscreen)
            {
                float dt = Time.deltaTime;
                _speed = Mathf.MoveTowards(_speed, LeaveSpeed, LeaveAccel * dt);
                wallX += _speed * dt;
                PlaceWall(wallX, upperEnd, center, lowerEnd);
                yield return null;
            }
            Throttle(false);

            _stage.enemies.Dismiss(_upper);
            _stage.enemies.Dismiss(_lower);
            _upper = null;
            _lower = null;
            _boss.Exposed = true;
        }

        static float Back(EnemyController plane) => plane != null ? plane.BodyBack : 0f;

        static float Front(EnemyController plane) => plane != null ? plane.BodyFront : 0f;

        void PlaceWall(float x, float upper, float mid, float lower)
        {
            _pos = new Vector2(x, mid);
            Place();
            if (_upper != null) _upper.ScriptTo(new Vector2(x, upper), 0f);
            if (_lower != null) _lower.ScriptTo(new Vector2(x, lower), 0f);
        }

        void FireWall()
        {
            _boss.Fire(WallBulletSpeed, WallDamage);
            if (_upper != null) _upper.Fire(WallBulletSpeed, WallDamage);
            if (_lower != null) _lower.Fire(WallBulletSpeed, WallDamage);
        }
    }
}
