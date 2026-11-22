using UnityEngine;

namespace MetalRaptors
{
    public class SkyDuel
    {
        class Gun
        {
            public int left;
            public float timer;
            public float rest;
        }

        const float LaneShare = 0.05f;

        const float NearShare = 0.1f;
        const float FarShare = 1.2f;
        const float ConeDeg = 35f;
        const float AimDeg = 25f;
        const float SpreadDeg = 1f;
        const int BurstMin = 5;
        const int BurstMax = 9;
        const float RestMin = 0.35f;
        const float RestMax = 0.7f;

        const float SpanMin = 0.35f;
        const float SpanMax = 0.65f;
        const float LoopScaleMin = 0.88f;
        const float LoopScaleMax = 1.12f;
        const float LeadMax = 0.8f;
        const float OffsetMax = 0.1f;

        const float FollowGain = 0.5f;
        const float FollowShare = 0.2f;
        const float FollowAccelShare = 0.1f;
        const float BlendSec = 1f;

        readonly DuelPlane _friend;
        readonly Vector3 _lane;
        readonly float _slotShare;
        readonly float _offset;
        readonly Gun _allyGun = new Gun();
        readonly Gun _enemyGun = new Gun();

        SkyPath _ally;
        SkyPath _enemy;
        DuelPlane _foe;
        bool _closing;

        bool _locked;
        float _center;
        float _centerVel;
        float _side;
        Vector3 _blendPos;
        Vector3 _blendVel;
        Quaternion _blendRot;
        float _blendAge;

        Vector3 _foePos;
        Vector3 _foeFwd = Vector3.left;
        float _foeHeading = -1f;

        public SkyDuel(DuelPlane friend, SkyFrame frame, Vector3 lane, float slotShare, float facing, bool settled)
        {
            _friend = friend;
            _lane = lane;
            _slotShare = slotShare;
            _offset = Random.Range(-OffsetMax, OffsetMax);
            _ally = SkyPath.Enter(friend, frame, FriendLane(frame), facing, settled);
            if (_ally != null) _ally.LoopScale = Random.Range(LoopScaleMin, LoopScaleMax);
        }

        public bool Alive => _ally != null && _ally.Alive;

        public DuelPlane Friend => _friend;

        public DuelPlane Foe => _foe;

        Vector3 FriendLane(SkyFrame frame) => _lane + Vector3.up * (LaneShare * frame.halfHeight);

        Vector3 FoeLane(SkyFrame frame) => _lane - Vector3.up * (LaneShare * frame.halfHeight);

        public void Engage(DuelPlane foe, SkyFrame frame, SkyPath flying)
        {
            if (foe == null || !Alive) return;

            Unlock();
            _foe = foe;
            _enemy = flying != null && flying.Plane == foe && flying.Alive
                ? flying
                : SkyPath.Arrive(foe, frame, FoeLane(frame), -1f, Random.Range(0f, LeadMax));
            _enemy.LoopScale = Random.Range(LoopScaleMin, LoopScaleMax);
            _enemy.ClearZone();
            _ally.RequestRun();
            if (!Closing(_ally.Position.x, _ally.Heading, _enemy.Position.x, _enemy.Heading))
                _ally.Want(-_enemy.Heading);
            _closing = Closing(_ally.Position.x, _ally.Heading, _enemy.Position.x, _enemy.Heading);
        }

        public SkyPath ReleaseFoe(SkyFrame frame)
        {
            SkyPath path = _enemy;
            if (_locked && _foe != null && _foe.Scripted)
                path = SkyPath.Enter(_foe, frame, FoeLane(frame), _foeHeading, false);

            _enemy = null;
            _foe = null;
            Unlock();

            if (path != null)
            {
                path.MayTurn = true;
                path.ClearZone();
            }
            return path;
        }

        public void Release()
        {
            if (_friend != null) _friend.EndScript();
            _ally = null;
        }

        public void Tick(float dt, SkyFrame frame, float facing)
        {
            if (!Alive) return;

            if (_foe != null && !_foe.Scripted)
            {
                _foe = null;
                _enemy = null;
                Unlock();
            }

            if (_foe == null)
            {
                _ally.MayTurn = true;
                _ally.RequestHold(Slot(frame, facing), facing);
                _ally.Tick(dt, frame);
                return;
            }

            if (_locked) TickLocked(dt, frame);
            else TickFree(dt, frame);

            Vector3 a = _ally.Position;
            bool closing = Closing(a.x, _ally.Heading, _foePos.x, _foeHeading);
            float range = Mathf.Abs(_foePos.x - a.x);
            float width = frame.halfWidth;
            bool window = closing && range > NearShare * width && range < FarShare * width;

            bool allyAim = window && Vector3.Angle(_ally.Forward, _foePos - a) <= ConeDeg;
            bool enemyAim = window && Vector3.Angle(_foeFwd, a - _foePos) <= ConeDeg;
            Shoot(_allyGun, _friend, _foe, allyAim, dt);
            Shoot(_enemyGun, _foe, _friend, enemyAim, dt);
        }

        void TickFree(float dt, SkyFrame frame)
        {
            bool closing = Closing(_ally.Position.x, _ally.Heading, _enemy.Position.x, _enemy.Heading);
            if (_closing && !closing && _ally.Heading != _enemy.Heading)
            {
                Lock();
                TickLocked(dt, frame);
                return;
            }

            _closing = closing;
            _ally.MayTurn = !closing;
            _enemy.MayTurn = !closing;
            _ally.Tick(dt, frame);
            _enemy.Tick(dt, frame);

            _foePos = _enemy.Position;
            _foeFwd = _enemy.Forward;
            _foeHeading = _enemy.Heading;
        }

        void Lock()
        {
            _locked = true;
            _center = (_ally.Position.x + _enemy.Position.x) * 0.5f;
            _centerVel = 0f;
            _ally.Lock(_center);
            _ally.MayTurn = true;
            _side = _ally.Position.x - _center;
            NewLobe();

            _blendPos = _foe.transform.position;
            _blendVel = _foe.Motion;
            _blendRot = _foe.Visual;
            _blendAge = 0f;
            _enemy = null;
        }

        void Unlock()
        {
            _locked = false;
            _closing = false;
            if (_ally == null) return;
            _ally.Unlock();
            _ally.ClearZone();
        }

        void TickLocked(float dt, SkyFrame frame)
        {
            Follow(dt, frame);
            _ally.Origin = _center;
            _ally.MayTurn = true;
            _ally.Tick(dt, frame);

            float side = _ally.Position.x - _center;
            if (side * _side < 0f) NewLobe();
            if (side != 0f) _side = side;

            Mirror(dt, frame);
        }

        void Follow(float dt, SkyFrame frame)
        {
            float pace = SkyPath.Pace(frame);
            float target = frame.camX + _offset * frame.halfWidth;
            float want = Mathf.Clamp((target - _center) * FollowGain, -FollowShare * pace, FollowShare * pace);
            _centerVel = Mathf.MoveTowards(_centerVel, want, FollowAccelShare * pace * dt);
            _center += _centerVel * dt;
        }

        void NewLobe()
        {
            _ally.TurnZone(0f, Random.Range(SpanMin, SpanMax));
            _ally.LoopScale = Random.Range(LoopScaleMin, LoopScaleMax);
        }

        void Mirror(float dt, SkyFrame frame)
        {
            Vector3 p = _ally.Position;
            Vector3 f = _ally.Forward;
            Vector3 u = _ally.Up;
            float gap = 2f * LaneShare * frame.halfHeight;

            var pos = new Vector3(2f * _center - p.x, p.y - gap, p.z);
            var fwd = new Vector3(-f.x, f.y, f.z);
            var up = new Vector3(-u.x, u.y, u.z);
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(fwd, up), up);

            _foeFwd = fwd;
            _foeHeading = -_ally.Heading;

            if (_blendAge < BlendSec)
            {
                _blendAge += dt;
                float k = Mathf.SmoothStep(0f, 1f, _blendAge / BlendSec);
                pos = Vector3.Lerp(_blendPos + _blendVel * _blendAge, pos, k);
                rot = Quaternion.Slerp(_blendRot, rot, k);
            }

            _foePos = pos;
            _foe.ScriptPose(pos, rot);
        }

        Vector3 Slot(SkyFrame frame, float facing) => new Vector3(
            _slotShare * frame.halfWidth * (facing < 0f ? -1f : 1f),
            _lane.y + LaneShare * frame.halfHeight,
            _lane.z);

        static bool Closing(float ax, float aHeading, float bx, float bHeading) =>
            aHeading != bHeading && (bx - ax) * aHeading > 0f;

        static void Shoot(Gun gun, DuelPlane shooter, DuelPlane mark, bool allowed, float dt)
        {
            if (shooter == null || mark == null) return;

            if (gun.left > 0)
            {
                if (!allowed)
                {
                    gun.left = 0;
                    gun.rest = Random.Range(RestMin, RestMax);
                    return;
                }

                gun.timer -= dt;
                if (gun.timer > 0f) return;

                gun.timer = shooter.ShotSpacing;
                gun.left--;
                Fire(shooter, mark);
                if (gun.left == 0) gun.rest = Random.Range(RestMin, RestMax);
                return;
            }

            gun.rest -= dt;
            if (gun.rest > 0f || !allowed) return;

            gun.left = Random.Range(BurstMin, BurstMax + 1);
            gun.timer = 0f;
        }

        static void Fire(DuelPlane shooter, DuelPlane mark)
        {
            Vector3 nose = shooter.transform.right;
            Vector3 line = mark.transform.position - shooter.transform.position;
            Vector3 dir = line.sqrMagnitude > 1f
                ? Vector3.RotateTowards(nose, line.normalized, AimDeg * Mathf.Deg2Rad, 0f)
                : nose;
            shooter.Fire(dir + Random.insideUnitSphere * Mathf.Tan(SpreadDeg * Mathf.Deg2Rad));
        }
    }
}
