using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public struct LastRunSetup
    {
        public DuelPlane crane;
        public ValleyBattery battery;
        public VillageSite site;
        public CampaignTerrain terrain;
        public float cruise;
        public float playZ;
        public float cameraDistance;
        public float halfViewWidth;
        public int seed;
        public Vector3 camera;
        public System.Action<float> shake;
        public System.Action<bool> muteEngine;
        public System.Action<System.Action> fadeBack;
    }

    public class CraneLastRun
    {
        const float U = PlaneModelConfig.UnitsPerMeter;

        const float SlowLine = 260f;
        const float RunInMin = 380f;
        const float PullLead = 330f;
        const float TurnRadius = 200f;
        const float MinTurnRadius = 130f;
        const float MaxTurnRadius = 420f;
        const float SwingDeg = 25f;
        const float ClimbRadius = 110f;
        const float LandRadius = 140f;
        const float MinLandRadius = 40f;
        const float HomeLine = 3600f;

        static readonly float[] SwingTries = { 25f, 35f, 15f, 45f, 10f, 55f };
        static readonly float[] DepthTries = { 0f, -60f, 60f, -120f, 120f };
        static readonly float[] SlowTries = { 0f, 150f, 300f, 450f, 600f };
        const float SwingCost = 10f, DepthCost = 60f, SlowCost = 150f;
        const float CheckStep = 20f;
        const float CheckPastClimb = 1500f;
        const float RoofMargin = 30f;

        const float SlowShare = 0.6f;
        const float TurnShare = 1f;
        const float DiveShare = 1.1f;
        const float ClimbShare = 0.85f;
        const float HomeShare = 0.9f;
        const float PaceAccel = 0.3f;

        const float SmoothReach = 60f;
        const int SmoothSteps = 12;
        const float HeadingProbe = 10f;
        const float CurveProbe = 30f;

        const float TurnClearance = 120f;
        const float RunClearance = 90f;
        const float MinClearance = 60f;
        const float AttackHeight = 130f;
        const float AttackClimb = 160f;
        const float DiveDeg = 25f;
        const float PullFlat = 40f;
        const float FireDelay = 30f;
        const float RiseShare = 0.3f;
        const float DiveFromShare = 0.5f;
        const float TossRise = 60f;
        const float TossClimbLength = 120f;
        const float LowClearance = 80f;
        const float TreeTop = 186f;
        const float TreeGap = 30f;
        const float GroveMargin = 20f;
        const float HomeEase = 300f;
        const float RoofGap = 55f;
        const float RoofReach = 60f;
        const float AltStep = 20f;
        const int AltRadius = 4;
        const int AltPasses = 2;

        const float BankDeg = 40f;
        const float ClimbBankDeg = 70f;
        const float BankRadius = 380f;
        const float BankLead = 90f;
        const float BankResponse = 2.5f;
        const float BankRate = 45f;
        const float ClimbBankRate = 90f;
        const float PitchProbe = 4f;
        const float PitchResponse = 3f;
        const float PoseBlendSec = 0.6f;

        const float FlakGapMin = 0.8f, FlakGapMax = 1.6f;
        const float BackGapMin = 0.5f, BackGapMax = 1f;
        const float BackSideMin = 60f, BackSideMax = 240f;
        const float BackLow = -30f, BackHigh = 150f;
        const float BackDepth = 60f;
        const float BackLead = 0.4f;
        const float FireSizeMin = 28f, FireSizeMax = 40f;
        const float FireVolume = 0.35f;
        const float NearShare = 0.3f;
        const float NearMin = 40f, NearMax = 80f;
        const float FarMin = 90f, FarMax = 220f;
        const float BurstLead = 0.3f;
        const float BurstGround = 80f;
        const float BurstSizeMin = 40f, BurstSizeMax = 70f;
        const float BurstSoundChance = 0.5f;

        const float MinShotGap = 0.05f;
        const float SpreadDeg = 3f;
        const float NoseConeDeg = 25f;
        const float PuffJitter = 25f;
        const float PuffSize = 14f;
        const float SparkSize = 8f;
        const int SparkEvery = 3;

        const float TossDelay = 0.35f;
        const float BombGravity = 200f;
        const float BombScale = 1.4f;
        const float BombDrop = 8f;
        const float GunHitLift = 10f;
        const float BombShake = 0.8f;
        const float PatchDelay = 0.8f;
        const float PatchRadius = 18f;
        const float MiddleSmokeDelay = 2f;

        const float BombLeadIn = 0.5f;
        const float MinFirstShot = 0.3f;
        const float GunShotSec = 3f;
        const float CraneShotSec = 3f;
        const float BurnSec = 2f;
        const float GunsHold = 1f;
        const float SparedSec = 2.5f;
        const float ShotLead = 0.5f;
        const float ShotHold = 1f;
        const float HitDelay = 0.25f;
        const float QuietLead = 0.5f;
        const float ThinShare = 0.5f;
        const float ThinStartMin = 0.15f, ThinStartMax = 0.35f;
        const float ShotFlash = 120f;
        const float ShotGlow = 90f;
        const float ShotSmoke = 80f;

        const float HitAhead = 25f;
        const float HitLift = 10f;
        const float HitSize = 70f;
        const float BlastSize = 90f;
        const float HitShake = 0.8f;

        const float FallPace = 0.8f;
        const float SinkSec = 5f;
        const float SinkGraceSec = 2f;
        const float SinkEarly = 0.6f, SinkLate = 0.4f;
        const float SinkPitchBlendSec = 0.6f;
        const float SinkRollStart = 10f, SinkRollEnd = 40f;
        const float SinkFallback = 300f;
        const float SinkTouch = 0.5f;
        const float CrashSize = 90f;
        const float CrashFire = 30f;
        const float CrashShake = 1f;
        const float LastBlowDelay = 1f;
        const float HoldSec = 3f;

        const float PanSec = 2f;
        const float ChaseShare = 0.75f;
        const float ChaseLift = 40f;
        const float ChaseInSec = 2.5f;
        const float ChaseResponse = 6f;
        const float NearKeep = 0.8f;
        const float BackResponse = 6f;
        const float CamGroundGap = 40f;

        const float GunCamMargin = 120f;
        const float GunCamMin = 120f;
        const float GunCamMax = 360f;
        const float GunCamLift = 35f;
        const float GunCamFallback = 200f;

        enum Phase { Fly, Fall, Hold, Fading, Done }

        enum Shot { Follow, Guns }

        struct Segment
        {
            public bool arc;
            public Vector2 start;
            public float heading;
            public float turn;
            public float radius;
            public float length;
            public float from;
        }

        struct Plan
        {
            public float swing;
            public float depth;
            public float slow;
        }

        struct Roof
        {
            public Vector2 centre;
            public float radius;
            public float top;
        }

        struct Pending
        {
            public float at;
            public System.Action act;
        }

        struct Puff
        {
            public float at;
            public Vector3 point;
            public bool spark;
        }

        readonly LastRunSetup _setup;
        readonly DuelPlane _crane;
        readonly List<AaGun> _guns = new List<AaGun>();
        readonly List<float> _gunIn = new List<float>();
        readonly List<Segment> _path = new List<Segment>();
        readonly List<Roof> _roofs = new List<Roof>();
        readonly List<float> _alt = new List<float>();
        readonly List<Pending> _pending = new List<Pending>();
        readonly List<Puff> _puffs = new List<Puff>();
        readonly System.Random _rng;

        Phase _phase = Phase.Fly;
        Shot _shot = Shot.Follow;
        int _cutFrame = -1;
        GameObject _prop;
        float _clock;
        float _length;
        float _slowEnd, _swingS, _attackS, _fireS, _pullS, _climbEnd;
        float _riseTo, _climbFrom, _diveStart, _diveEnd, _tossTop;
        float _cruiseY, _peakY, _homeY, _homeAt;
        float _s;
        float _speed;
        float _bank;
        float _pitch;
        float _heading;
        float _flyAge;
        Quaternion _startRot;
        float _startY;
        float _baseAttackY;
        float _attackY;
        float _hillZ;
        Vector3 _gunCam;
        Vector3 _velocity;

        float _roundIn;
        int _roundCount;

        bool _tossed;
        bool _hit;
        bool _shooterQuiet;
        bool _thin;

        float _fallAge;
        float _fallSpeed;
        float _fallPitchFrom;
        float _fallBank;
        Vector3 _fallPos;
        Vector3 _fallFrom;
        Vector3 _fallDir;
        float _fallDrop;
        float _fallHeading;
        Vector3 _crash;
        float _holdAge;

        float _chaseAt = -1f;
        Vector3 _focus;
        bool _hasFocus;
        float _camZ;
        bool _hasCamZ;
        bool _skipping;

        public bool Framing => _phase != Phase.Done;

        public bool Done => _phase == Phase.Done;

        public bool Cut => _cutFrame == Time.frameCount;

        public Vector3 CameraPosition { get; private set; }

        CraneLastRun(LastRunSetup setup)
        {
            _setup = setup;
            _crane = setup.crane;
            _rng = new System.Random(setup.seed ^ 0xC2A9E);
        }

        public static CraneLastRun Begin(LastRunSetup setup)
        {
            if (setup.crane == null || setup.battery == null || setup.terrain == null) return null;

            var guns = new List<AaGun>();
            foreach (AaGun gun in setup.battery.Hill)
                if (gun != null && gun.Grounded && !gun.Down) guns.Add(gun);
            if (guns.Count < 3) return null;
            guns.Sort((a, b) => a.X.CompareTo(b.X));

            var run = new CraneLastRun(setup);
            run._guns.AddRange(guns);
            run.Setup();
            return run;
        }

        void Setup()
        {
            _setup.battery.HoldHill(true);
            foreach (AaGun gun in _guns)
            {
                gun.Retarget(_crane.transform);
                _gunIn.Add(Range(FlakGapMin, FlakGapMax));
            }

            _hillZ = _guns[1].transform.position.z;
            float hillGround = float.MinValue;
            foreach (AaGun gun in _guns) hillGround = Mathf.Max(hillGround, gun.Foot.y);
            _baseAttackY = hillGround + AttackHeight;
            _gunCam = GunCamera(hillGround);

            CollectRoofs();

            Vector3 start = _crane.transform.position;
            _startY = start.y;
            _startRot = _crane.transform.rotation;
            _crane.BeginPosed();

            BuildPath(new Vector2(start.x, start.z));
            BuildAltitude();

            _prop = Bomb.BuildProp();
            _speed = _setup.cruise;
            _heading = Mathf.PI;
            _roundIn = 0f;
            CameraPosition = _setup.camera;
        }

        Vector3 GunCamera(float hillGround)
        {
            float left = _guns[0].X, right = _guns[_guns.Count - 1].X;
            float span = (right - left) * 0.5f + GunCamMargin;
            float distance = _setup.halfViewWidth > 1f
                ? _setup.cameraDistance * span / _setup.halfViewWidth
                : GunCamFallback;
            distance = Mathf.Clamp(distance, GunCamMin, GunCamMax);

            float x = (left + right) * 0.5f;
            float z = _hillZ - distance;
            float y = hillGround + GunCamLift;
            float ground = Ground(x, z);
            if (ground != float.MinValue) y = Mathf.Max(y, ground + CamGroundGap);
            return new Vector3(x, y, z);
        }

        void CollectRoofs()
        {
            if (_setup.site.corners == null) return;

            VosgesLayout.VillageSpot spot = _setup.site.spot;
            Bounds[] boxes = VosgesVillage.BuildingsOf(spot.model);
            foreach (Bounds box in boxes)
            {
                Vector3 c = box.center;
                Vector3 centre = ValleyVillages.World(spot, new Vector3(c.x, box.min.y, c.z));
                _roofs.Add(new Roof
                {
                    centre = new Vector2(centre.x, centre.z),
                    radius = new Vector2(box.size.x, box.size.z).magnitude * 0.5f * U,
                    top = ValleyVillages.World(spot, new Vector3(c.x, box.max.y, c.z)).y,
                });
            }
        }

        void BuildPath(Vector2 start)
        {
            var tries = new List<Plan>();
            foreach (float swing in SwingTries)
                foreach (float depth in DepthTries)
                    foreach (float slow in SlowTries)
                        tries.Add(new Plan { swing = swing, depth = depth, slow = slow });
            tries.Sort((a, b) => Cost(a).CompareTo(Cost(b)));

            foreach (Plan plan in tries)
            {
                Lay(start, plan);
                PlanHeights(false);
                if (Clear()) return;
            }

            Lay(start, tries[0]);
            PlanHeights(true);
        }

        static float Cost(Plan plan) =>
            Mathf.Abs(plan.swing - SwingDeg) / SwingCost + Mathf.Abs(plan.depth) / DepthCost
            + plan.slow / SlowCost;

        void Lay(Vector2 start, Plan plan)
        {
            float swing = plan.swing * Mathf.Deg2Rad;
            float cos = Mathf.Cos(swing);
            float runZ = _hillZ + plan.depth;
            float bend = Mathf.Clamp(
                (runZ - start.y + TurnRadius * (1f - cos)) / (1f + cos),
                MinTurnRadius, MaxTurnRadius);

            float pullX = Bombed.X - PullLead;
            float reach = (bend + TurnRadius) * Mathf.Sin(swing);
            float slow = Mathf.Max(SlowLine, start.x + reach - pullX + RunInMin) + plan.slow;

            _path.Clear();
            _length = 0f;
            Vector2 at = start;
            float heading = Mathf.PI;

            AddLine(ref at, ref heading, slow);
            _slowEnd = _length;
            AddArc(ref at, ref heading, -1f, bend, Mathf.PI + swing);
            _swingS = _length;
            AddArc(ref at, ref heading, 1f, TurnRadius, swing);
            _attackS = _length;
            AddLine(ref at, ref heading, Mathf.Max(RunInMin, pullX - at.x));
            _pullS = _length;
            AddArc(ref at, ref heading, -1f, ClimbRadius, Mathf.PI * 0.5f);
            float room = at.y - _setup.playZ;
            float land = Mathf.Clamp(room, MinLandRadius, LandRadius);
            AddLine(ref at, ref heading, room - land);
            _climbEnd = _length;
            AddArc(ref at, ref heading, -1f, land, Mathf.PI * 0.5f);
            AddLine(ref at, ref heading, HomeLine);
        }

        bool Clear()
        {
            float end = Mathf.Min(_length, _climbEnd + CheckPastClimb);
            for (float s = 0f; s <= end; s += CheckStep)
            {
                Vector2 at = Point(s);
                float y = RawAltitude(s);
                foreach (Roof roof in _roofs)
                {
                    if (roof.top + RoofGap + RoofMargin <= y) continue;
                    if ((roof.centre - at).magnitude < roof.radius + RoofReach) return false;
                }
            }
            return true;
        }

        void AddLine(ref Vector2 at, ref float heading, float length)
        {
            if (length <= 0f) return;

            _path.Add(new Segment { arc = false, start = at, heading = heading, length = length, from = _length });
            at += Dir(heading) * length;
            _length += length;
        }

        void AddArc(ref Vector2 at, ref float heading, float turn, float radius, float angle)
        {
            if (angle <= 0f || radius <= 0f) return;

            float length = radius * angle;
            _path.Add(new Segment
            {
                arc = true, start = at, heading = heading, turn = turn, radius = radius,
                length = length, from = _length,
            });
            ArcEnd(ref at, ref heading, turn, radius, angle);
            _length += length;
        }

        static void ArcEnd(ref Vector2 at, ref float heading, float turn, float radius, float angle)
        {
            Vector2 centre = at + Perp(heading, turn) * radius;
            heading += turn * angle;
            at = centre - Perp(heading, turn) * radius;
        }

        static Vector2 Dir(float heading) => new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));

        static Vector2 Perp(float heading, float turn) =>
            new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading)) * turn;

        Vector2 Point(float s)
        {
            s = Mathf.Clamp(s, 0f, _length);
            for (int i = 0; i < _path.Count; i++)
            {
                Segment seg = _path[i];
                if (s > seg.from + seg.length && i < _path.Count - 1) continue;

                float d = Mathf.Clamp(s - seg.from, 0f, seg.length);
                if (!seg.arc) return seg.start + Dir(seg.heading) * d;

                Vector2 at = seg.start;
                float heading = seg.heading;
                ArcEnd(ref at, ref heading, seg.turn, seg.radius, d / seg.radius);
                return at;
            }
            return Vector2.zero;
        }

        Vector2 Smooth(float s)
        {
            float reach = Mathf.Min(SmoothReach, Mathf.Max(0f, s), Mathf.Max(0f, _length - s));
            Vector2 sum = Vector2.zero;
            int n = 0;
            for (int k = -SmoothSteps; k <= SmoothSteps; k++)
            {
                sum += Point(s + reach * k / SmoothSteps);
                n++;
            }
            return sum / n;
        }

        float SmoothHeading(float s)
        {
            Vector2 a = Smooth(s - HeadingProbe);
            Vector2 b = Smooth(s + HeadingProbe);
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-4f)
            {
                Vector2 e = Point(Mathf.Min(_length, s + HeadingProbe)) - Point(Mathf.Max(0f, s - HeadingProbe));
                return e.sqrMagnitude > 1e-4f ? Mathf.Atan2(e.y, e.x) : _heading;
            }
            return Mathf.Atan2(d.y, d.x);
        }

        float Curvature(float s)
        {
            float a = SmoothHeading(s - CurveProbe) * Mathf.Rad2Deg;
            float b = SmoothHeading(s + CurveProbe) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(a, b) * Mathf.Deg2Rad / (2f * CurveProbe);
        }

        float Ground(float x, float z)
        {
            if (_setup.terrain != null && _setup.terrain.SampleHeight(x, z, out float y)) return y;
            VosgesLayout layout = Layout;
            return layout != null ? layout.Ground(x, z) : float.MinValue;
        }

        float RoofFloor(Vector2 at)
        {
            float y = float.MinValue;
            foreach (Roof roof in _roofs)
                if ((roof.centre - at).magnitude <= roof.radius + RoofReach)
                    y = Mathf.Max(y, roof.top + RoofGap);
            return y;
        }

        float Floor(float s, float clearance, bool roofs)
        {
            Vector2 at = Point(s);
            float ground = Ground(at.x, at.y);
            float y = ground == float.MinValue ? float.MinValue : ground + clearance;
            return roofs ? Mathf.Max(y, RoofFloor(at)) : y;
        }

        void PlanHeights(bool roofs)
        {
            _riseTo = _slowEnd + (_swingS - _slowEnd) * RiseShare;
            _diveEnd = _pullS - PullFlat;
            _tossTop = _pullS + TossClimbLength;

            _attackY = _baseAttackY;
            for (float s = _diveEnd; s <= _pullS; s += AltStep)
                _attackY = Mathf.Max(_attackY, Floor(s, RunClearance, roofs));

            _cruiseY = _startY;
            for (float s = 0f; s <= _diveEnd; s += AltStep)
                _cruiseY = Mathf.Max(_cruiseY, Floor(s, TurnClearance, roofs));
            _peakY = _cruiseY + AttackClimb;

            float drop = Mathf.Max(0f, _peakY - _attackY);
            float dive = drop / Mathf.Tan(DiveDeg * Mathf.Deg2Rad);
            float earliest = _slowEnd + (_swingS - _slowEnd) * DiveFromShare;
            _diveStart = Mathf.Max(_diveEnd - dive, earliest);
            _climbFrom = _slowEnd;
            _fireS = _diveStart + FireDelay;

            PlanHome();
        }

        void PlanHome()
        {
            VosgesLayout layout = Layout;
            _homeAt = Mathf.Min(_length, _climbEnd + HomeEase);
            _homeY = _startY;
            float end = Mathf.Min(_length, _climbEnd + CheckPastClimb);
            for (float s = _pullS; s <= end; s += AltStep)
            {
                Vector2 at = Point(s);
                float ground = Ground(at.x, at.y);
                if (ground == float.MinValue) continue;
                bool trees = layout != null && layout.InGrove(at.x, at.y, GroveMargin);
                _homeY = Mathf.Max(_homeY, ground + (trees ? TreeTop + TreeGap : LowClearance));
            }
        }

        VosgesLayout Layout => _setup.terrain is VosgesTerrain valley ? valley.Layout : null;

        float RawAltitude(float s)
        {
            if (s <= _diveStart)
            {
                float rise = Mathf.SmoothStep(0f, 1f, s / Mathf.Max(1f, _riseTo));
                float climb = Mathf.SmoothStep(0f, 1f, (s - _climbFrom) / Mathf.Max(1f, _diveStart - _climbFrom));
                return Mathf.Lerp(_startY, _cruiseY, rise) + AttackClimb * climb;
            }
            if (s <= _diveEnd)
            {
                float k = (s - _diveStart) / Mathf.Max(1f, _diveEnd - _diveStart);
                return Mathf.Lerp(_peakY, _attackY, k);
            }
            if (s <= _pullS) return _attackY;
            if (s <= _tossTop)
            {
                float k = Mathf.SmoothStep(0f, 1f, (s - _pullS) / Mathf.Max(1f, _tossTop - _pullS));
                return _attackY + TossRise * k;
            }
            if (s <= _homeAt)
            {
                float k = Mathf.SmoothStep(0f, 1f, (s - _tossTop) / Mathf.Max(1f, _homeAt - _tossTop));
                return Mathf.Lerp(_attackY + TossRise, _homeY, k);
            }
            return _homeY;
        }

        void BuildAltitude()
        {
            _alt.Clear();
            int count = Mathf.CeilToInt(_length / AltStep) + 1;
            var floor = new float[count];
            for (int i = 0; i < count; i++) floor[i] = Floor(i * AltStep, MinClearance, true);

            int reach = AltRadius * AltPasses;
            for (int i = 0; i < count; i++)
            {
                float lowest = float.MinValue;
                for (int k = -reach; k <= reach; k++)
                    lowest = Mathf.Max(lowest, floor[Mathf.Clamp(i + k, 0, count - 1)]);
                _alt.Add(Mathf.Max(RawAltitude(i * AltStep), lowest));
            }

            var scratch = new float[count];
            for (int pass = 0; pass < AltPasses; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int k = -AltRadius; k <= AltRadius; k++)
                    {
                        int j = Mathf.Clamp(i + k, 0, count - 1);
                        sum += _alt[j];
                        n++;
                    }
                    scratch[i] = sum / n;
                }
                for (int i = 0; i < count; i++) _alt[i] = scratch[i];
            }

            _alt[0] = _startY;
        }

        float Altitude(float s)
        {
            if (_alt.Count == 0) return _startY;
            float f = Mathf.Clamp(s / AltStep, 0f, _alt.Count - 1);
            int i = Mathf.FloorToInt(f);
            int j = Mathf.Min(i + 1, _alt.Count - 1);
            return Mathf.Lerp(_alt[i], _alt[j], f - i);
        }

        float PaceShare(float s)
        {
            if (s <= _slowEnd) return SlowShare;
            if (s <= _attackS) return TurnShare;
            if (s <= _pullS) return DiveShare;
            if (s <= _climbEnd) return ClimbShare;
            return HomeShare;
        }

        public void Tick(float dt)
        {
            if (_phase == Phase.Done || dt <= 0f) return;

            _clock += dt;
            RunPending();
            RunPuffs();

            if (_phase == Phase.Fading)
            {
                Frame(dt);
                return;
            }

            if (!_skipping && CanSkip() && MenuInput.ReadSkip())
            {
                Skip();
                return;
            }

            switch (_phase)
            {
                case Phase.Fly: Fly(dt); break;
                case Phase.Fall: Fall(dt); break;
                case Phase.Hold: Hold(dt); break;
            }

            if (_phase != Phase.Fading && _phase != Phase.Done)
            {
                Flak(dt);
                Frame(dt);
            }
        }

        static bool CanSkip() =>
            !GameMenu.IsOpen && !LevelBriefing.IsOpen && !DevFreeze.Active && !ScreenFade.IsBusy;

        void Fly(float dt)
        {
            if (_crane == null)
            {
                _crash = _focus;
                _phase = Phase.Hold;
                return;
            }

            _flyAge += dt;
            float target = _setup.cruise * PaceShare(_s);
            _speed = Mathf.MoveTowards(_speed, target, _setup.cruise * PaceAccel * dt);
            _s = Mathf.Min(_length, _s + _speed * dt);

            Vector2 at = Smooth(_s);
            float heading = SmoothHeading(_s);
            float y = Altitude(_s);
            float rise = (Altitude(_s + PitchProbe) - Altitude(_s - PitchProbe)) / (2f * PitchProbe);
            float pitch = Mathf.Atan(rise) * Mathf.Rad2Deg;
            _pitch = _flyAge <= dt ? pitch : Mathf.Lerp(_pitch, pitch, 1f - Mathf.Exp(-PitchResponse * dt));

            bool climbing = _s > _pullS && _s <= _climbEnd + SmoothReach;
            float limit = climbing ? ClimbBankDeg : BankDeg;
            float bank = limit * DuelPlane.BankShape(Curvature(_s + BankLead) * BankRadius);
            float eased = Mathf.Lerp(_bank, bank, 1f - Mathf.Exp(-BankResponse * dt));
            _bank = Mathf.MoveTowards(_bank, eased, (climbing ? ClimbBankRate : BankRate) * dt);
            _heading = heading;

            Quaternion rot = Pose(heading, _pitch, _bank);
            float blend = Mathf.SmoothStep(0f, 1f, _flyAge / PoseBlendSec);
            rot = Quaternion.Slerp(_startRot, rot, blend);

            var pos = new Vector3(at.x, y, at.y);
            _crane.Pose(pos, rot);
            _velocity = Forward(heading, _pitch) * _speed;

            if (_s >= _fireS && _s <= _pullS) Strafe(dt);
            if (!_tossed && _s >= _pullS + _setup.cruise * ClimbShare * TossDelay) Toss(pos);
            if (!_hit && _s >= _length) Hit();
        }

        static Quaternion Pose(float heading, float pitch, float bank) =>
            Quaternion.Euler(0f, -heading * Mathf.Rad2Deg, 0f)
            * Quaternion.Euler(0f, 0f, pitch)
            * Quaternion.Euler(bank, 0f, 0f);

        static Vector3 Forward(float heading, float pitch)
        {
            float p = pitch * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(heading) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Sin(heading) * Mathf.Cos(p));
        }

        void Strafe(float dt)
        {
            _roundIn -= dt;
            if (_roundIn > 0f) return;
            _roundIn = Mathf.Max(MinShotGap, _crane.ShotSpacing);

            float k = Mathf.Clamp01((_s - _attackS) / Mathf.Max(1f, _pullS - _attackS));
            Vector3 aim = Vector3.Lerp(_guns[0].Muzzle, _guns[1].Muzzle, k);
            Vector3 from = _crane.transform.position;
            Vector3 dir = (aim - from).normalized;
            Vector3 nose = _velocity.sqrMagnitude > 1e-4f ? _velocity.normalized : Vector3.right;
            if (Vector3.Angle(nose, dir) > NoseConeDeg) return;

            dir = (dir + Random.insideUnitSphere * Mathf.Tan(SpreadDeg * Mathf.Deg2Rad)).normalized;
            _crane.ShootAlong(dir);

            Vector2 jitter = Random.insideUnitCircle * PuffJitter;
            var point = new Vector3(aim.x + jitter.x, aim.y, aim.z + jitter.y);
            float ground = Ground(point.x, point.z);
            if (ground != float.MinValue) point.y = ground;

            _roundCount++;
            _puffs.Add(new Puff
            {
                at = _clock + Vector3.Distance(from, point) / DuelPlane.RoundSpeed,
                point = point,
                spark = _roundCount % SparkEvery == 0,
            });
        }

        void RunPuffs()
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = _puffs[i];
                if (_clock < puff.at) continue;

                _puffs.RemoveAt(i);
                if (_skipping) continue;
                MuzzleSmoke.Spawn(puff.point, Vector3.up, PuffSize);
                if (puff.spark) Sparks.Spawn(puff.point, SparkSize);
            }
        }

        void Toss(Vector3 pos)
        {
            _tossed = true;
            if (_prop == null) return;

            Vector3 from = pos + Vector3.down * BombDrop;
            Vector3 to = Bombed.Foot + Vector3.up * GunHitLift;
            RaidBomb.Fall(_prop, from, _velocity, to, BombGravity, BombScale, OnBombLanded);

            float drop = Mathf.Max(1f, from.y - to.y);
            float climb = _velocity.y;
            Schedule((climb + Mathf.Sqrt(climb * climb + 2f * BombGravity * drop)) / BombGravity);
        }

        void Schedule(float flight)
        {
            float guns = Mathf.Max(MinFirstShot, flight - BombLeadIn);
            float back = guns + GunShotSec;
            float again = back + CraneShotSec;
            float middle = again + BurnSec;
            float spared = middle + GunsHold;
            float last = spared + SparedSec;
            float shot = last + ShotLead;
            float cut = shot + ShotHold;

            AaGun mid = _guns[1];
            Later(guns, () => SetShot(Shot.Guns));
            Later(back, () => SetShot(Shot.Follow));
            Later(again, () => SetShot(Shot.Guns));
            Later(middle - QuietLead, () => _shooterQuiet = true);
            Later(middle, () => { if (mid != null) mid.Blow(); });
            Later(spared, Spared);
            Later(last, () =>
            {
                _thin = false;
                _shooterQuiet = true;
                SetShot(Shot.Guns);
            });
            Later(shot, FinalShot);
            Later(cut, () => SetShot(Shot.Follow));
            Later(cut + HitDelay, Hit);
        }

        void Spared()
        {
            _shooterQuiet = false;
            _thin = true;
            _gunIn[0] = Range(ThinStartMin, ThinStartMax);
            SetShot(Shot.Follow);
        }

        void SetShot(Shot shot)
        {
            if (_shot == shot) return;

            _shot = shot;
            _cutFrame = Time.frameCount;
            if (shot == Shot.Follow)
            {
                _hasFocus = false;
                _hasCamZ = false;
            }
        }

        void OnBombLanded(Vector3 point)
        {
            if (_skipping || _phase == Phase.Done) return;

            AaGun bombed = Bombed, middle = _guns[1];
            bombed.Blow();
            _setup.shake?.Invoke(BombShake);

            Later(PatchDelay, () => SpreadFire(bombed, middle));
            Later(MiddleSmokeDelay, () => { if (middle != null) middle.Damage(); });
        }

        void SpreadFire(AaGun from, AaGun to)
        {
            if (from == null || to == null) return;

            Vector3 mid = (from.Foot + to.Foot) * 0.5f;
            float ground = Ground(mid.x, mid.z);
            if (ground != float.MinValue) mid.y = ground;

            var host = new GameObject("Gun Fire");
            host.transform.position = mid;
            WreckFire.Begin(host.transform, mid, PatchRadius, _setup.seed + 977);
        }

        AaGun Bombed => _guns[_guns.Count - 1];

        AaGun Shooter => _guns[0];

        void FinalShot()
        {
            AaGun shooter = Shooter;
            if (shooter == null || shooter.Down || !shooter.Shoot()) return;

            Vector3 muzzle = shooter.Muzzle;
            Vector3 to = _crane != null ? _crane.transform.position : muzzle + Vector3.up;
            Vector3 dir = (to - muzzle).normalized;
            MuzzleFlash.Spawn(muzzle, dir, ShotFlash);
            MuzzleFlash.Spawn(muzzle, dir, ShotGlow);
            MuzzleSmoke.Spawn(muzzle, dir, ShotSmoke);
        }

        void Later(float seconds, System.Action act)
        {
            _pending.Add(new Pending { at = _clock + seconds, act = act });
        }

        void RunPending()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                Pending item = _pending[i];
                if (_clock < item.at) continue;

                _pending.RemoveAt(i);
                i--;
                if (!_skipping) item.act?.Invoke();
            }
        }

        void Hit()
        {
            if (_hit || _phase != Phase.Fly) return;
            _hit = true;

            if (_crane == null)
            {
                _crash = _focus;
                _phase = Phase.Hold;
                return;
            }

            Vector3 pos = _crane.transform.position;
            Vector3 nose = _velocity.sqrMagnitude > 1e-4f ? _velocity.normalized : Vector3.left;
            FlakBurst.Spawn(pos + nose * HitAhead + Vector3.up * HitLift, HitSize, CameraPosition, true);
            Explosion.Spawn(pos, BlastSize);
            _crane.Burn();
            _setup.shake?.Invoke(HitShake);
            _setup.muteEngine?.Invoke(true);

            _phase = Phase.Fall;
            _fallAge = 0f;
            _fallPos = pos;
            _fallFrom = pos;
            _fallHeading = _heading;
            _fallSpeed = _speed * FallPace;
            _fallPitchFrom = _pitch;
            _fallBank = _bank;
            _fallDir = new Vector3(Mathf.Cos(_heading), 0f, Mathf.Sin(_heading));

            Vector3 land = pos + _fallDir * (_fallSpeed * SinkSec);
            float ground = Ground(land.x, land.z);
            _fallDrop = Mathf.Max(1f, ground != float.MinValue ? pos.y - ground : SinkFallback);
        }

        static float Sink(float u) => u <= 1f
            ? SinkEarly * u * u + SinkLate * u * u * u * u
            : 1f + SinkRate(1f) * (u - 1f);

        static float SinkRate(float u)
        {
            u = Mathf.Min(u, 1f);
            return 2f * SinkEarly * u + 4f * SinkLate * u * u * u;
        }

        void Fall(float dt)
        {
            _fallAge += dt;
            float u = _fallAge / SinkSec;

            Vector3 flat = _fallFrom + _fallDir * (_fallSpeed * _fallAge);
            float y = _fallFrom.y - _fallDrop * Sink(u);
            float climb = -_fallDrop * SinkRate(u) / SinkSec;
            float want = Mathf.Atan2(climb, Mathf.Max(1f, _fallSpeed)) * Mathf.Rad2Deg;
            float pitch = Mathf.Lerp(_fallPitchFrom, want, Mathf.SmoothStep(0f, 1f, _fallAge / SinkPitchBlendSec));
            _fallBank += Mathf.Lerp(SinkRollStart, SinkRollEnd, Mathf.Clamp01(u)) * dt;
            _fallPos = new Vector3(flat.x, y, flat.z);

            if (_crane != null) _crane.Pose(_fallPos, Pose(_fallHeading, pitch, _fallBank));

            float ground = Ground(_fallPos.x, _fallPos.z);
            bool down = ground != float.MinValue && _fallPos.y <= ground + SinkTouch;
            if (down || _fallAge >= SinkSec + SinkGraceSec) Crash(down ? ground : _fallPos.y);
        }

        void Crash(float ground)
        {
            _crash = new Vector3(_fallPos.x, ground, _fallPos.z);
            bool wrecked = _crane != null && _crane.CrashAt(_crash);

            Explosion.Spawn(_crash, CrashSize);
            GroundBlast.Spawn(_crash, CrashSize, CameraPosition);
            if (!wrecked)
            {
                var host = new GameObject("Crane Wreck");
                host.transform.position = _crash;
                WreckFire.Begin(host.transform, _crash, CrashFire, _setup.seed + 1301);
            }
            _setup.shake?.Invoke(CrashShake);

            AaGun shooter = Shooter;
            Later(LastBlowDelay, () => { if (shooter != null) shooter.Blow(); });

            _phase = Phase.Hold;
            _holdAge = 0f;
        }

        void Hold(float dt)
        {
            _holdAge += dt;
            if (_holdAge < Mathf.Max(HoldSec, LastBlowDelay)) return;

            _phase = Phase.Fading;
            if (_setup.fadeBack != null) _setup.fadeBack(Finish);
            else Finish();
        }

        public void Skip()
        {
            if (_phase == Phase.Done || _phase == Phase.Fading) return;

            _skipping = true;
            _phase = Phase.Fading;
            System.Action atBlack = () =>
            {
                if (_crane != null) Object.Destroy(_crane.gameObject);
                Finish();
            };
            if (_setup.fadeBack != null) _setup.fadeBack(atBlack);
            else atBlack();
        }

        void Finish()
        {
            _phase = Phase.Done;
            _pending.Clear();
            _puffs.Clear();
            if (_prop != null) Object.Destroy(_prop);
            _prop = null;
        }

        void Flak(float dt)
        {
            if (_crane == null || _hit) return;
            if (_phase != Phase.Fly || _s < _slowEnd) return;

            Vector3 plane = _crane.transform.position;
            for (int i = 0; i < _guns.Count; i++)
            {
                AaGun gun = _guns[i];
                if (gun == null || gun.Down) continue;
                if (i == 0 && _shooterQuiet) continue;

                bool back = _tossed;
                _gunIn[i] -= dt;
                if (_gunIn[i] > 0f) continue;
                _gunIn[i] = back ? Range(BackGapMin, BackGapMax) : Range(FlakGapMin, FlakGapMax);
                if (_thin) _gunIn[i] /= ThinShare * _guns.Count;

                if (!gun.Shoot()) continue;

                Vector3 at = back ? BackBurst(plane) : plane + _velocity * BurstLead + Random.onUnitSphere
                    * (Random.value < NearShare ? Range(NearMin, NearMax) : Range(FarMin, FarMax));
                float ground = Ground(at.x, at.z);
                if (ground != float.MinValue) at.y = Mathf.Max(at.y, ground + BurstGround);

                FlakBurst.Spawn(at, Range(BurstSizeMin, BurstSizeMax), CameraPosition,
                    Random.value < BurstSoundChance);
                if (back) Explosion.Spawn(at, Range(FireSizeMin, FireSizeMax), FireVolume);
            }
        }

        Vector3 BackBurst(Vector3 plane)
        {
            float side = (Random.value < 0.5f ? -1f : 1f) * Range(BackSideMin, BackSideMax);
            return plane + _velocity * BackLead
                   + new Vector3(side, Range(BackLow, BackHigh), Range(-BackDepth, BackDepth));
        }

        void Frame(float dt)
        {
            if (_shot == Shot.Guns)
            {
                CameraPosition = _gunCam;
                return;
            }

            Vector3 plane = _phase == Phase.Hold || _crane == null ? _crash : _crane.transform.position;

            if (_chaseAt < 0f && _s >= _slowEnd) _chaseAt = _clock;

            float pan = Mathf.SmoothStep(0f, 1f, _clock / PanSec);
            var side = new Vector3(plane.x, plane.y, _setup.camera.z);
            Vector3 sideCam = Vector3.Lerp(_setup.camera, side, pan);

            if (!_hasFocus)
            {
                _focus = plane;
                _hasFocus = true;
            }
            _focus = Vector3.Lerp(_focus, plane, 1f - Mathf.Exp(-ChaseResponse * dt));

            float back = _setup.cameraDistance * ChaseShare;
            float camZ = Mathf.Min(_focus.z - back, plane.z - back * NearKeep);
            _camZ = _hasCamZ ? Mathf.Lerp(_camZ, camZ, 1f - Mathf.Exp(-BackResponse * dt)) : camZ;
            _hasCamZ = true;
            var chaseCam = new Vector3(_focus.x, _focus.y + ChaseLift, _camZ);

            float chase = _chaseAt < 0f ? 0f : Mathf.SmoothStep(0f, 1f, (_clock - _chaseAt) / ChaseInSec);
            Vector3 cam = Vector3.Lerp(sideCam, chaseCam, chase);

            float ground = Ground(cam.x, cam.z);
            if (ground != float.MinValue) cam.y = Mathf.Max(cam.y, ground + CamGroundGap);
            CameraPosition = cam;
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    }
}
