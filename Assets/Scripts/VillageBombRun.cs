using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public struct BombRunSetup
    {
        public CubeController cube;
        public CompanionFlight wing;
        public VillageSite site;
        public CampaignTerrain terrain;
        public float halfViewWidth;
        public float cruise;
        public float minY;
        public float laneY;
        public float playZ;
        public float cameraDistance;
        public int seed;
        public System.Action<float> face;
        public System.Action<float> shake;
    }

    public class VillageBombRun
    {
        const float U = PlaneModelConfig.UnitsPerMeter;

        const float CloseSec = 2.5f;
        const float JoinMaxSec = 7f;
        const float JoinReach = 60f;
        const float BackShare = 0.25f;
        const float Lookahead = 240f;
        const float MaxPitch = 0.3f;

        const float RunDeg = 45f;
        const float SteepDeg = 62f;
        const float TurnRadius = 320f;
        const float MinTurnRadius = 160f;
        const float MinRunDepth = 120f;
        const float LoopRadius = 220f;
        const float TearDeg = 45f;
        const float ReturnRadius = 330f;
        const float LandRadius = 380f;
        const float MinHomeScale = 0.5f;
        const float MinHomeLine = 120f;
        const float ReturnDeg = 20f;
        const float EntryLead = 160f;
        const float ExitMargin = 60f;
        const float ArrowSec = 3f;
        const float HomeSec = 3f;
        const float SettleSec = 1.5f;
        const float BankDeg = 40f;
        const float BankRadius = 380f;
        const float BankLead = 90f;
        const float BankResponse = 2.5f;
        const float BankRate = 45f;
        const float PoseBlendSec = 0.6f;
        const float PitchProbe = 4f;
        const float PitchResponse = 6f;
        const float LaneEase = 300f;

        const float SmoothReach = 60f;
        const int SmoothSteps = 12;
        const float CurveProbe = 10f;
        const float HomeRun = 240f;
        const float AltStep = 20f;
        const int AltRadius = 3;
        const int AltPasses = 3;

        const float SweepLag = 0.8f;
        const float SweepPull = 0.6f;

        const float GroundClearance = 170f;
        const float GroundStep = 40f;
        const float GroundSlope = 0.35f;
        const float GroundEase = 300f;
        const float ClimbShare = 0.6f;

        const float ChaseShare = 0.75f;
        const float ChaseInSec = 2.5f;
        const float ChaseOutSec = 3.5f;
        const float ChaseLift = 40f;
        const float StopSoftness = 60f;
        const float ChaseDip = 100f;
        const float DipLeadSec = 0.5f;
        const float DipInSec = 2.5f;
        const float DipHoldSec = 1.2f;
        const float DipOutSec = 3f;
        const float CamRoofGap = 30f;
        const float CamRoofReach = 100f;
        const float CamGroundGap = 40f;

        const float TopClearance = 50f;
        const float PlaneReach = 50f;
        const float HopRamp = 260f;
        const float HopPad = 40f;

        const float FormationBelow = 50f;
        const float RoofClearance = 55f;
        const float RoofQuantile = 0.75f;

        const float BombGravity = 200f;
        const float DropSlack = 25f;
        const float RoofInner = 0.5f;
        const float BombScale = 1.4f;
        const float BombDrop = 8f;
        const float BombReach = 230f;
        const float CooldownMin = 0.6f, CooldownMax = 1f;
        const float FirstDropMax = 0.5f;
        const float RoofHit = 0.8f;
        const float BlastSize = 60f;
        const float PanicReach = 1600f;
        const float ShakePerBlast = 0.5f;

        const float SplashReach = 55f;
        const float SplashDelayMin = 0.3f, SplashDelayMax = 0.8f;
        const float SpreadDelayMin = 1.2f, SpreadDelayMax = 2.6f;
        const float SpreadPerUnit = 0.006f;

        const float FireScale = 1.6f;
        const float HouseSmoke = 0.175f;
        const int RoofSpots = 7, WallSpots = 3;
        const float SpotInset = 0.15f;
        const float RoofLow = 0.72f, RoofHigh = 0.95f;
        const float WallLow = 0.15f, WallHigh = 0.55f;
        const float WallFace = 0.47f;

        enum Phase { Close, Back, Approach, Fly, Done }

        enum Lead { Straight, Loop, Teardrop }

        class Target
        {
            public Vector3 roof;
            public Vector3 centre;
            public float top;
            public float radius;
            public Vector3[] spots;
            public float size;
            public bool bombed;
            public bool burning;
            public int index;
        }

        class Ignition
        {
            public Target target;
            public Vector3 from;
            public float at;
        }

        struct Hop
        {
            public float at;
            public float half;
            public float height;
        }

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

        readonly BombRunSetup _setup;
        readonly List<Target> _targets = new List<Target>();
        readonly List<Ignition> _ignitions = new List<Ignition>();
        readonly List<Transform> _planes = new List<Transform>();
        readonly List<float> _cooldowns = new List<float>();
        readonly List<Vector3> _planeVel = new List<Vector3>();
        readonly Dictionary<Transform, Vector3> _planeLast = new Dictionary<Transform, Vector3>();
        readonly List<bool> _armed = new List<bool>();
        readonly List<Segment> _path = new List<Segment>();
        readonly List<float> _floor = new List<float>();
        readonly List<float> _alt = new List<float>();
        readonly List<Hop> _hops = new List<Hop>();
        readonly float _runY;
        readonly Vector2 _centre;
        readonly float _attackRadius;
        readonly System.Random _rng;
        Phase _phase = Phase.Close;
        GameObject _prop;
        float _age;
        float _clock;
        int _falling;

        float _length;
        float _s;
        float _flyAge;
        float _attackS, _entryS, _exitS, _homeS;
        float _startY;
        float _bank;
        float _pitch;
        float _heading;
        float _endHeading;
        Vector2 _endAt;
        float _stopZ;
        float _faceSign = 1f;
        float _chaseAge;
        bool _chasing;
        bool _homeBound;
        bool _spreadQueued;
        Quaternion _startRot;
        Vector3 _camOffset;
        Vector3 _velocity;
        Vector3 _sweepPos, _sweepVel, _sweepPrev;

        public bool Done => _phase == Phase.Done;

        public bool Framing => _phase == Phase.Fly && _chasing;

        public bool UnderFire => Framing && _s <= _homeS;

        public IReadOnlyList<Transform> Formation => _planes;

        public Vector3 CameraPosition { get; private set; }

        VillageBombRun(BombRunSetup setup)
        {
            _setup = setup;
            _rng = new System.Random(setup.seed ^ 0x5EED);
            VosgesLayout.VillageSpot spot = setup.site.spot;
            var tops = new List<float>();

            Bounds[] boxes = VosgesVillage.BuildingsOf(spot.model);
            for (int i = 0; i < boxes.Length; i++)
            {
                Bounds box = boxes[i];
                Vector3 c = box.center;
                _targets.Add(new Target
                {
                    roof = ValleyVillages.World(spot, new Vector3(c.x, box.min.y + box.size.y * RoofHit, c.z)),
                    centre = ValleyVillages.World(spot, new Vector3(c.x, box.min.y, c.z)),
                    top = ValleyVillages.World(spot, new Vector3(c.x, box.max.y, c.z)).y,
                    radius = new Vector2(box.size.x, box.size.z).magnitude * 0.5f * U,
                    spots = Spots(spot, box),
                    size = Mathf.Max(box.size.x, box.size.z) * U * FireScale,
                    index = i,
                });
                tops.Add(ValleyVillages.World(spot, new Vector3(c.x, box.max.y, c.z)).y);
            }

            tops.Sort();
            float roof = tops.Count > 0
                ? tops[Mathf.Clamp(Mathf.FloorToInt(tops.Count * RoofQuantile), 0, tops.Count - 1)]
                : spot.padY;
            _runY = Mathf.Max(setup.minY, roof + FormationBelow + RoofClearance);

            Vector2 sum = Vector2.zero;
            Vector3[] corners = setup.site.corners;
            if (corners != null && corners.Length > 0)
            {
                foreach (Vector3 corner in corners) sum += new Vector2(corner.x, corner.z);
                _centre = sum / corners.Length;
            }
            else
            {
                _centre = new Vector2(setup.site.CentreX, spot.origin.z);
            }

            float steep = 1f - Mathf.Cos(SteepDeg * Mathf.Deg2Rad);
            _attackRadius = Mathf.Clamp((_centre.y - setup.playZ - MinRunDepth) / steep, MinTurnRadius, TurnRadius);
        }

        Vector3[] Spots(VosgesLayout.VillageSpot spot, Bounds box)
        {
            var spots = new Vector3[RoofSpots + WallSpots];
            Vector3 min = box.min, size = box.size;

            for (int i = 0; i < RoofSpots; i++)
            {
                var local = new Vector3(
                    min.x + size.x * Range(SpotInset, 1f - SpotInset),
                    min.y + size.y * Range(RoofLow, RoofHigh),
                    min.z + size.z * Range(SpotInset, 1f - SpotInset));
                spots[i] = ValleyVillages.World(spot, local);
            }

            for (int i = 0; i < WallSpots; i++)
            {
                float along = Range(SpotInset, 1f - SpotInset);
                float side = _rng.Next(2) == 0 ? 0.5f - WallFace : 0.5f + WallFace;
                bool onX = _rng.Next(2) == 0;
                var local = new Vector3(
                    min.x + size.x * (onX ? side : along),
                    min.y + size.y * Range(WallLow, WallHigh),
                    min.z + size.z * (onX ? along : side));
                spots[RoofSpots + i] = ValleyVillages.World(spot, local);
            }
            return spots;
        }

        public static VillageBombRun Begin(BombRunSetup setup)
        {
            if (setup.cube == null) return null;

            var run = new VillageBombRun(setup);
            run._prop = Bomb.BuildProp();
            setup.cube.FlyLevel();
            setup.cube.SetTargetHeading(setup.cube.Facing > 0f ? 0f : Mathf.PI);
            if (setup.wing != null) setup.wing.Join();
            return run;
        }

        public void Tick(float dt, Vector3 camera)
        {
            _clock += dt;
            TickIgnitions();
            if (_phase == Phase.Done || _setup.cube == null) return;

            _age += dt;
            CubeController cube = _setup.cube;
            bool turning = cube.TurningBack;
            float x = cube.transform.position.x;

            switch (_phase)
            {
                case Phase.Close:
                    Steer(cube.Facing);
                    if (_age >= CloseSec && !turning && Gathered()) Decide(x, cube.Facing);
                    break;

                case Phase.Back:
                    Steer(-1f);
                    if (x <= TearX() + SmoothReach) BeginFlight(Lead.Teardrop);
                    break;

                case Phase.Approach:
                    Steer(1f);
                    if (x >= TurnX(RunDeg * Mathf.Deg2Rad) - SmoothReach) BeginFlight(Lead.Straight);
                    break;

                case Phase.Fly:
                    Fly(dt, camera);
                    break;
            }
        }

        void Decide(float x, float facing)
        {
            if (_setup.wing != null) _setup.wing.FreeFollow(true, SettleSec);

            if (facing > 0f && x <= TurnX(SteepDeg * Mathf.Deg2Rad) - SmoothReach) _phase = Phase.Approach;
            else if (facing > 0f) BeginFlight(Lead.Loop);
            else _phase = Phase.Back;
        }

        bool Gathered() => _setup.wing == null || _age >= JoinMaxSec || _setup.wing.Joined(JoinReach);

        float RunwayX() => TurnX(RunDeg * Mathf.Deg2Rad) - _setup.halfViewWidth * BackShare;

        static float TearLine(float tear) => 2f * LoopRadius / Mathf.Tan(tear);

        float TearX()
        {
            float tear = TearDeg * Mathf.Deg2Rad;
            Vector2 at = Vector2.zero;
            float heading = Mathf.PI;
            ArcEnd(ref at, ref heading, -1f, LoopRadius, tear);
            at += Dir(heading) * TearLine(tear);
            ArcEnd(ref at, ref heading, 1f, LoopRadius, Mathf.PI + tear);
            return RunwayX() - at.x;
        }

        void Steer(float dir)
        {
            CubeController cube = _setup.cube;
            if (cube.TurningBack) return;

            float rise = Mathf.Clamp(_setup.laneY - cube.transform.position.y, -Lookahead, Lookahead);
            float pitch = Mathf.Clamp(Mathf.Atan2(rise, Lookahead), -MaxPitch, MaxPitch);
            cube.SetTargetHeading(dir > 0f ? pitch : Mathf.PI - pitch);
        }

        float TurnX(float theta)
        {
            float depth = _centre.y - _setup.playZ - _attackRadius * (1f - Mathf.Cos(theta));
            return _centre.x - depth / Mathf.Tan(theta) - _attackRadius * Mathf.Sin(theta);
        }

        float Solve(float x)
        {
            float low = RunDeg * Mathf.Deg2Rad, high = SteepDeg * Mathf.Deg2Rad;
            if (x <= TurnX(low)) return low;
            if (x >= TurnX(high)) return high;

            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * 0.5f;
                if (TurnX(mid) < x) low = mid;
                else high = mid;
            }
            return (low + high) * 0.5f;
        }

        void BeginFlight(Lead lead)
        {
            CubeController cube = _setup.cube;
            Vector3 start = cube.transform.position;
            _startY = start.y;
            _startRot = cube.transform.rotation;

            float heading = lead == Lead.Teardrop ? Mathf.PI : 0f;
            BuildPath(new Vector2(start.x, _setup.playZ), heading, lead);
            BuildFloor();
            BuildAltitude();

            cube.BeginScripted();
            if (_setup.wing != null) _setup.wing.ManualFollow(true);

            _s = 0f;
            _flyAge = 0f;
            _bank = 0f;
            _pitch = 0f;
            _heading = heading;
            _faceSign = Mathf.Cos(heading) < 0f ? -1f : 1f;
            _chasing = false;
            _phase = Phase.Fly;
        }

        void BuildPath(Vector2 start, float startHeading, Lead lead)
        {
            _path.Clear();
            _length = 0f;

            Vector2 at = start;
            float heading = startHeading;
            AddLine(ref at, ref heading, SmoothReach);
            if (lead == Lead.Loop)
            {
                AddArc(ref at, ref heading, 1f, LoopRadius, Mathf.PI);
                AddLine(ref at, ref heading, at.x - RunwayX());
                AddArc(ref at, ref heading, 1f, LoopRadius, Mathf.PI);
            }
            else if (lead == Lead.Teardrop)
            {
                float tear = TearDeg * Mathf.Deg2Rad;
                AddArc(ref at, ref heading, -1f, LoopRadius, tear);
                AddLine(ref at, ref heading, TearLine(tear));
                AddArc(ref at, ref heading, 1f, LoopRadius, Mathf.PI + tear);
            }

            float theta = RunDeg * Mathf.Deg2Rad;
            float turnAt = TurnX(theta);
            if (at.x < turnAt) AddLine(ref at, ref heading, turnAt - at.x);
            else theta = Solve(at.x);
            _attackS = _length;

            AddArc(ref at, ref heading, 1f, _attackRadius, theta);

            Vector2 dir = Dir(theta);
            float along = Vector2.Dot(_centre - at, dir);
            float span = HalfSpan(dir);
            _entryS = _length + Mathf.Max(0f, along - span - EntryLead);
            PlanHops(at, theta, _length);
            AddLine(ref at, ref heading, Mathf.Max(0f, along + span + ExitMargin));
            _exitS = _length;

            float back = ReturnDeg * Mathf.Deg2Rad;
            float side = Mathf.PI * 0.5f;
            float scale = HomeScale(at.y, heading, theta + side + back, side - back, back);
            float landRadius = LandRadius * scale;
            AddArc(ref at, ref heading, -1f, ReturnRadius * scale, theta + side + back);
            _homeS = _length;

            Vector2 probe = at;
            float probeHeading = heading;
            ArcEnd(ref probe, ref probeHeading, -1f, landRadius, side - back);
            float landDrop = probe.y - at.y;
            float line = (at.y + landDrop - _setup.playZ) / Mathf.Max(0.01f, Mathf.Cos(back));
            AddLine(ref at, ref heading, Mathf.Max(0f, line));
            AddArc(ref at, ref heading, -1f, landRadius, side - back);
            AddLine(ref at, ref heading, HomeRun);
            _endAt = at;
            _endHeading = heading;

            _stopZ = _centre.y - _setup.cameraDistance * ChaseShare;
        }

        float HomeScale(float depth, float heading, float turnBack, float turnLand, float back)
        {
            Vector2 a = Vector2.zero;
            float h = heading;
            ArcEnd(ref a, ref h, -1f, ReturnRadius, turnBack);
            Vector2 b = Vector2.zero;
            ArcEnd(ref b, ref h, -1f, LandRadius, turnLand);

            float drop = -(a.y + b.y);
            float room = depth - _setup.playZ - MinHomeLine * Mathf.Cos(back);
            if (drop <= 1f || room >= drop) return 1f;
            return Mathf.Clamp(room / drop, MinHomeScale, 1f);
        }

        void PlanHops(Vector2 from, float heading, float fromS)
        {
            _hops.Clear();

            var slots = new List<Vector3> { Vector3.zero };
            if (_setup.wing != null) _setup.wing.ArrowSlots(slots);

            Vector2 dir = Dir(heading);
            Vector2 side = Normal(heading);
            foreach (Target target in _targets)
            {
                float height = target.top + TopClearance;
                if (height <= _runY) continue;

                Vector2 gap = Flat(target.centre) - from;
                float along = Vector2.Dot(gap, dir);
                float across = Vector2.Dot(gap, side);
                foreach (Vector3 slot in slots)
                {
                    if (Mathf.Abs(across - slot.z) > target.radius + PlaneReach) continue;

                    _hops.Add(new Hop
                    {
                        at = fromS + along - slot.x,
                        half = target.radius + HopPad,
                        height = height - slot.y,
                    });
                }
            }
        }

        float HopY(float s, float y)
        {
            foreach (Hop hop in _hops)
            {
                float gap = Mathf.Abs(s - hop.at) - hop.half;
                float k = gap <= 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, gap / HopRamp);
                if (k <= 0f) continue;
                y = Mathf.Max(y, Mathf.Lerp(_runY, hop.height, k));
            }
            return y;
        }

        float HalfSpan(Vector2 dir)
        {
            float span = 0f;
            Vector3[] corners = _setup.site.corners;
            if (corners == null) return (_setup.site.right - _setup.site.left) * 0.5f;

            foreach (Vector3 corner in corners)
                span = Mathf.Max(span, Mathf.Abs(Vector2.Dot(new Vector2(corner.x, corner.z) - _centre, dir)));
            return span;
        }

        static Vector2 Dir(float heading) => new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));

        static Vector2 Normal(float heading) => new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading));

        void AddLine(ref Vector2 at, ref float heading, float length)
        {
            if (length <= 0f) return;

            _path.Add(new Segment { arc = false, start = at, heading = heading, length = length, from = _length });
            at += Dir(heading) * length;
            _length += length;
        }

        void AddArc(ref Vector2 at, ref float heading, float turn, float radius, float angle)
        {
            if (angle <= 0f) return;

            float length = radius * angle;
            _path.Add(new Segment
            {
                arc = true, start = at, heading = heading, turn = turn, radius = radius, length = length,
                from = _length,
            });
            ArcEnd(ref at, ref heading, turn, radius, angle);
            _length += length;
        }

        static void ArcEnd(ref Vector2 at, ref float heading, float turn, float radius, float angle)
        {
            Vector2 centre = at + Normal(heading) * (turn * radius);
            heading += turn * angle;
            at = centre - Normal(heading) * (turn * radius);
        }

        void Sample(float s, out Vector2 at, out float heading, out float curvature)
        {
            s = Mathf.Clamp(s, 0f, _length);
            Segment seg = _path[_path.Count - 1];
            for (int i = 0; i < _path.Count; i++)
            {
                if (s <= _path[i].from + _path[i].length)
                {
                    seg = _path[i];
                    break;
                }
            }

            float t = Mathf.Clamp(s - seg.from, 0f, seg.length);
            if (!seg.arc)
            {
                at = seg.start + Dir(seg.heading) * t;
                heading = seg.heading;
                curvature = 0f;
                return;
            }

            heading = seg.heading;
            at = seg.start;
            ArcEnd(ref at, ref heading, seg.turn, seg.radius, t / seg.radius);
            curvature = seg.turn / seg.radius;
        }

        void BuildFloor()
        {
            _floor.Clear();
            int count = Mathf.CeilToInt(_length / GroundStep) + 1;
            for (int i = 0; i < count; i++)
            {
                float s = i * GroundStep;
                float floor = float.MinValue;
                bool village = s >= _entryS && s <= _exitS;
                Sample(s, out Vector2 at, out _, out _);
                if (!village && _setup.terrain != null && _setup.terrain.SampleHeight(at.x, at.y, out float ground))
                    floor = ground + GroundClearance;
                if (s < _attackS) floor = Mathf.Max(floor, RoofFloor(at));
                _floor.Add(floor);
            }

            var ramped = new float[count];
            for (int i = 0; i < count; i++)
            {
                float best = _floor[i];
                for (int j = 0; j < count; j++)
                {
                    if (_floor[j] == float.MinValue) continue;
                    best = Mathf.Max(best, _floor[j] - GroundSlope * Mathf.Abs(i - j) * GroundStep);
                }
                ramped[i] = best;
            }

            for (int i = 0; i < count; i++) _floor[i] = ramped[i];
        }

        float RoofFloor(Vector2 at)
        {
            float floor = float.MinValue;
            foreach (Target target in _targets)
            {
                float reach = target.radius + PlaneReach;
                if ((Flat(target.centre) - at).sqrMagnitude > reach * reach) continue;
                floor = Mathf.Max(floor, target.top + TopClearance + FormationBelow);
            }
            return floor;
        }

        float Lane(float s) => _attackS > 0f
            ? Mathf.Lerp(_startY, _setup.laneY, Mathf.SmoothStep(0f, 1f, s / LaneEase))
            : _startY;

        Vector2 Point(float s)
        {
            if (s < 0f) return _path[0].start + Dir(_path[0].heading) * s;
            if (s > _length) return _endAt + Dir(_endHeading) * (s - _length);

            Sample(s, out Vector2 at, out _, out _);
            return at;
        }

        Vector2 Smooth(float s)
        {
            float step = 2f * SmoothReach / SmoothSteps;
            float from = s - SmoothReach;
            Vector2 sum = Point(from) + Point(s + SmoothReach);
            for (int i = 1; i < SmoothSteps; i++) sum += Point(from + i * step) * (i % 2 == 1 ? 4f : 2f);
            return sum * (step / 3f) / (2f * SmoothReach);
        }

        float SmoothHeading(float s)
        {
            Vector2 chord = Point(s + SmoothReach) - Point(s - SmoothReach);
            return Mathf.Atan2(chord.y, chord.x);
        }

        float Curvature(float s)
        {
            float turn = Mathf.DeltaAngle(SmoothHeading(s - CurveProbe) * Mathf.Rad2Deg,
                SmoothHeading(s + CurveProbe) * Mathf.Rad2Deg);
            return turn * Mathf.Deg2Rad / (2f * CurveProbe);
        }

        void BuildAltitude()
        {
            _alt.Clear();
            int count = Mathf.CeilToInt(_length / AltStep) + 1;
            var raw = new float[count];
            var next = new float[count];
            for (int i = 0; i < count; i++) raw[i] = RawAltitude(Mathf.Min(i * AltStep, _length));

            for (int pass = 0; pass < AltPasses; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    float sum = 0f;
                    for (int k = -AltRadius; k <= AltRadius; k++) sum += raw[Mathf.Clamp(i + k, 0, count - 1)];
                    next[i] = sum / (2 * AltRadius + 1);
                }

                float[] swap = raw;
                raw = next;
                next = swap;
            }
            _alt.AddRange(raw);
        }

        float Altitude(float s)
        {
            int count = _alt.Count;
            if (count == 0) return RawAltitude(s);
            if (count == 1) return _alt[0];

            float f = Mathf.Clamp(s / AltStep, 0f, count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), count - 2);
            float t = f - i;
            float p0 = _alt[Mathf.Max(i - 1, 0)];
            float p1 = _alt[i];
            float p2 = _alt[i + 1];
            float p3 = _alt[Mathf.Min(i + 2, count - 1)];
            return 0.5f * (2f * p1 + (p2 - p0) * t
                           + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t
                           + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
        }

        float RawAltitude(float s)
        {
            float y;
            float climbEnd = _exitS + (_homeS - _exitS) * ClimbShare;
            if (s <= _attackS) y = Lane(s);
            else if (s <= _entryS)
                y = Mathf.Lerp(Lane(_attackS), _runY,
                    Mathf.SmoothStep(0f, 1f, (s - _attackS) / Mathf.Max(1f, _entryS - _attackS)));
            else if (s <= _exitS) y = _runY;
            else if (s <= climbEnd)
                y = Mathf.Lerp(_runY, _setup.laneY, Mathf.SmoothStep(0f, 1f, (s - _exitS) / Mathf.Max(1f, climbEnd - _exitS)));
            else y = _setup.laneY;

            y = HopY(s, y);
            float floor = Floor(s);
            if (floor <= y) return y;

            float ease = Mathf.SmoothStep(0f, 1f, s / GroundEase)
                         * Mathf.SmoothStep(0f, 1f, (_length - s) / GroundEase);
            return y + (floor - y) * ease;
        }

        float Floor(float s)
        {
            if (_floor.Count == 0) return float.MinValue;

            float f = Mathf.Clamp(s / GroundStep, 0f, _floor.Count - 1);
            int i = Mathf.FloorToInt(f);
            int j = Mathf.Min(i + 1, _floor.Count - 1);
            if (_floor[i] == float.MinValue || _floor[j] == float.MinValue)
                return Mathf.Max(_floor[i], _floor[j]);
            return Mathf.Lerp(_floor[i], _floor[j], f - i);
        }

        void Fly(float dt, Vector3 camera)
        {
            CubeController cube = _setup.cube;
            float cruise = _setup.cruise;
            _flyAge += dt;
            _s = Mathf.Min(_length, _s + cruise * dt);

            Vector2 at = Smooth(_s);
            float heading = SmoothHeading(_s);
            float curvature = Curvature(_s + BankLead);
            float y = Altitude(_s);
            float rise = (Altitude(_s + PitchProbe) - Altitude(_s - PitchProbe)) / (2f * PitchProbe);
            float pitch = Mathf.Atan(rise) * Mathf.Rad2Deg;
            _pitch = _flyAge <= dt ? pitch : Mathf.Lerp(_pitch, pitch, 1f - Mathf.Exp(-PitchResponse * dt));

            float bank = BankDeg * DuelPlane.BankShape(curvature * BankRadius);
            float eased = Mathf.Lerp(_bank, bank, 1f - Mathf.Exp(-BankResponse * dt));
            _bank = Mathf.MoveTowards(_bank, eased, BankRate * dt);
            _heading = heading;

            Quaternion rot = Quaternion.Euler(0f, -heading * Mathf.Rad2Deg, 0f)
                             * Quaternion.Euler(0f, 0f, _pitch)
                             * Quaternion.Euler(_bank, 0f, 0f);
            float blend = Mathf.SmoothStep(0f, 1f, _flyAge / PoseBlendSec);
            rot = Quaternion.Slerp(_startRot, rot, blend);

            var lead = new Vector3(at.x, y, at.y);
            cube.Pose(lead, rot);
            _velocity = new Vector3(Mathf.Cos(heading), rise, Mathf.Sin(heading)) * cruise;

            _planes.Clear();
            _planes.Add(cube.transform);
            if (_setup.wing != null)
            {
                _setup.wing.Follow(dt);
                _setup.wing.Planes(_planes);
            }
            TrackPlanes(dt);

            if (!_chasing) FaceAlong(heading);
            if (_chasing) CameraPosition = ChaseCamera(lead, dt);
            else if (_s >= _attackS) BeginChase(lead, camera);

            if (_s >= Mathf.Max(_attackS, _entryS - BombReach) && _s <= _exitS) Drop(dt);
            if (!_spreadQueued && _s > _exitS && _falling <= 0) QueueSpread();
            if (!_homeBound && _s >= _homeS)
            {
                _homeBound = true;
                if (_setup.wing != null) _setup.wing.Unarrow(HomeSec);
            }

            if (_s >= _length) Land();
        }

        void FaceAlong(float heading)
        {
            float sign = Mathf.Cos(heading) < 0f ? -1f : 1f;
            if (sign == _faceSign) return;

            _faceSign = sign;
            _setup.face?.Invoke(sign);
        }

        void BeginChase(Vector3 lead, Vector3 camera)
        {
            _chasing = true;
            _chaseAge = 0f;
            _camOffset = camera - ChaseTarget(lead, 0f, 0f);
            _sweepPos = lead;
            _sweepPrev = lead;
            _sweepVel = _velocity;
            CameraPosition = camera;
            if (_setup.wing != null) _setup.wing.Arrow(ArrowSec);
        }

        Vector3 ChaseCamera(Vector3 lead, float dt)
        {
            _chaseAge += dt;
            float inK = Mathf.SmoothStep(0f, 1f, _chaseAge / ChaseInSec);
            float outK = Mathf.SmoothStep(0f, 1f, (_length - _s) / Mathf.Max(1f, _setup.cruise * ChaseOutSec));
            float chase = inK * outK;

            Vector3 centre = Vector3.zero;
            int n = 0;
            foreach (Transform plane in _planes)
            {
                if (plane == null) continue;
                centre += plane.position;
                n++;
            }
            centre = n > 0 ? centre / n : lead;

            Vector3 exact = Vector3.Lerp(lead, centre, chase);
            Sweep(exact, dt);
            Vector3 focus = Vector3.Lerp(exact, _sweepPos, chase);
            return ChaseTarget(focus, chase, Dip()) + _camOffset * (1f - inK);
        }

        float Dip()
        {
            float cruise = Mathf.Max(1f, _setup.cruise);
            float from = Mathf.Max(_attackS, _entryS - BombReach) - cruise * DipLeadSec;
            float back = _exitS + cruise * DipHoldSec;
            float down = Mathf.SmoothStep(0f, 1f, (_s - from) / (cruise * DipInSec));
            float up = Mathf.SmoothStep(0f, 1f, (_s - back) / (cruise * DipOutSec));
            return down * (1f - up);
        }

        float CameraFloor(Vector3 cam)
        {
            float y = cam.y;
            var at = new Vector2(cam.x, cam.z);
            foreach (Target target in _targets)
            {
                float need = target.top + CamRoofGap;
                if (need <= y) continue;

                float gap = (Flat(target.centre) - at).magnitude - target.radius;
                float k = 1f - Mathf.SmoothStep(0f, 1f, gap / CamRoofReach);
                if (k > 0f) y = Mathf.Max(y, Mathf.Lerp(cam.y, need, k));
            }

            if (_setup.terrain != null && _setup.terrain.SampleHeight(cam.x, cam.z, out float ground))
                y = Mathf.Max(y, ground + CamGroundGap);
            return y;
        }

        void Sweep(Vector3 exact, float dt)
        {
            if (dt <= 0f) return;

            Vector3 velocity = (exact - _sweepPrev) / dt;
            _sweepPrev = exact;
            _sweepVel = Vector3.Lerp(_sweepVel, velocity, 1f - Mathf.Exp(-dt / SweepLag));
            _sweepPos = Vector3.Lerp(_sweepPos + _sweepVel * dt, exact, 1f - Mathf.Exp(-SweepPull * dt));
        }

        Vector3 ChaseTarget(Vector3 focus, float chase, float dip)
        {
            float distance = _setup.cameraDistance * Mathf.Lerp(1f, ChaseShare, chase);
            float z = SoftMin(focus.z - distance, _stopZ, StopSoftness);
            var cam = new Vector3(focus.x, focus.y + (ChaseLift - ChaseDip * dip) * chase, z);
            cam.y = CameraFloor(cam);
            return cam;
        }

        static float SoftMin(float a, float b, float k)
        {
            float gap = (b - a) / k;
            if (gap > 20f) return a;
            if (gap < -20f) return b;
            return b - k * Mathf.Log(1f + Mathf.Exp(gap));
        }

        void Land()
        {
            CubeController cube = _setup.cube;
            Vector2 at = Smooth(_length);
            cube.EndScripted(new Vector3(at.x, Altitude(_length), _setup.playZ), Mathf.PI);
            cube.FlyLevel();
            cube.SetTargetHeading(Mathf.PI);
            if (_setup.wing != null)
            {
                _setup.wing.ManualFollow(false);
                _setup.wing.FreeFollow(false, SettleSec);
            }
            _setup.face?.Invoke(-1f);
            if (!_spreadQueued) QueueSpread();
            Finish();
        }

        void Drop(float dt)
        {
            while (_cooldowns.Count < _planes.Count)
            {
                _cooldowns.Add(0f);
                _armed.Add(false);
            }

            for (int i = 0; i < _planes.Count; i++)
            {
                Transform plane = _planes[i];
                if (plane == null) continue;

                _cooldowns[i] -= dt;
                if (_cooldowns[i] > 0f) continue;

                Vector3 velocity = i < _planeVel.Count ? _planeVel[i] : _velocity;
                Vector3 from = plane.position - plane.up * BombDrop;
                Target target = Pick(from, velocity, out Vector3 aim);
                if (target == null) continue;

                if (!_armed[i])
                {
                    _armed[i] = true;
                    _cooldowns[i] = Range(0f, FirstDropMax);
                    continue;
                }

                target.bombed = true;
                _falling++;
                RaidBomb.Fall(_prop, from, velocity, aim, BombGravity, BombScale, point => Hit(target, point));
                _cooldowns[i] = Range(CooldownMin, CooldownMax);
            }
        }

        void TrackPlanes(float dt)
        {
            _planeVel.Clear();
            foreach (Transform plane in _planes)
            {
                if (plane == null)
                {
                    _planeVel.Add(_velocity);
                    continue;
                }

                Vector3 now = plane.position;
                bool known = _planeLast.TryGetValue(plane, out Vector3 last);
                _planeVel.Add(known && dt > 0f ? (now - last) / dt : _velocity);
                _planeLast[plane] = now;
            }
        }

        Target Pick(Vector3 from, Vector3 velocity, out Vector3 aim)
        {
            Target best = null;
            aim = from;
            float nearest = float.MaxValue;
            var flat = new Vector2(velocity.x, velocity.z);
            foreach (Target target in _targets)
            {
                if (target.bombed) continue;

                float drop = from.y - target.roof.y;
                if (drop <= 1f) continue;

                float fall = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * BombGravity * drop)) / BombGravity;
                Vector2 roof = Flat(target.roof);
                Vector2 miss = Flat(from) + flat * fall - roof;
                float reach = target.radius + DropSlack;
                float gap = miss.sqrMagnitude;
                if (gap > reach * reach || gap >= nearest) continue;

                nearest = gap;
                best = target;
                Vector2 spot = roof + Vector2.ClampMagnitude(miss, target.radius * RoofInner);
                aim = new Vector3(spot.x, target.roof.y, spot.y);
            }
            return best;
        }

        void Hit(Target target, Vector3 point)
        {
            _falling--;

            Camera cam = Camera.main;
            Explosion.Spawn(point, BlastSize);
            GroundBlast.Spawn(point, BlastSize, cam != null ? cam.transform.position : point);
            VillagePeople.PanicNear(point, PanicReach);
            _setup.shake?.Invoke(ShakePerBlast);

            Ignite(target, point);

            foreach (Target other in _targets)
            {
                if (other.bombed || other == target) continue;
                if (Flat(other.roof - point).sqrMagnitude > SplashReach * SplashReach) continue;

                other.bombed = true;
                Schedule(other, point, Range(SplashDelayMin, SplashDelayMax));
            }
        }

        void QueueSpread()
        {
            _spreadQueued = true;

            foreach (Target target in _targets)
            {
                if (target.bombed) continue;

                Target near = null;
                float best = float.MaxValue;
                foreach (Target other in _targets)
                {
                    if (!other.bombed || other == target) continue;
                    float gap = Flat(other.roof - target.roof).sqrMagnitude;
                    if (gap >= best) continue;
                    best = gap;
                    near = other;
                }

                target.bombed = true;
                float distance = near != null ? Mathf.Sqrt(best) : 0f;
                Vector3 from = near != null ? near.roof : target.roof;
                Schedule(target, from, Range(SpreadDelayMin, SpreadDelayMax) + distance * SpreadPerUnit);
            }
        }

        void Schedule(Target target, Vector3 from, float delay)
        {
            _ignitions.Add(new Ignition { target = target, from = from, at = _clock + delay });
        }

        void TickIgnitions()
        {
            for (int i = _ignitions.Count - 1; i >= 0; i--)
            {
                Ignition ignition = _ignitions[i];
                if (_clock < ignition.at) continue;

                _ignitions.RemoveAt(i);
                Ignite(ignition.target, ignition.from);
            }
        }

        void Ignite(Target target, Vector3 from)
        {
            if (target.burning) return;
            target.burning = true;
            target.bombed = true;

            var spots = new List<Vector3>(target.spots);
            spots.Sort((a, b) => (a - from).sqrMagnitude.CompareTo((b - from).sqrMagnitude));
            PhosphorusFire.House(target.centre, spots, target.size, _setup.seed + target.index * 31, HouseSmoke);
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        void Finish()
        {
            _phase = Phase.Done;
            if (_prop != null) Object.Destroy(_prop);
            _prop = null;
        }

        public void Abort()
        {
            if (_phase == Phase.Fly && _setup.cube != null)
            {
                Vector2 at = Smooth(_s);
                _setup.cube.EndScripted(new Vector3(at.x, Altitude(_s), _setup.playZ),
                    Mathf.Cos(_heading) < 0f ? Mathf.PI : 0f);
                if (_setup.wing != null) _setup.wing.ManualFollow(false);
            }
            if (_setup.wing != null) _setup.wing.FreeFollow(false, SettleSec);
            if (_prop != null) Object.Destroy(_prop);
            _prop = null;
            _phase = Phase.Done;
        }
    }
}
