using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class VosgesLayout
    {
        const float U = PlaneModelConfig.UnitsPerMeter;

        public const float VillageBackZ = 1290f;
        const float VillageRefZ = 800f;
        const float VillageScreens = 2f;
        const float VillageFrontGap = 5f * U;
        const float VillageJitter = 0.12f;
        const float VillagePadMargin = 3f * U;
        const float VillagePadFalloff = 90f;
        const int VillageYawSteps = 72;
        const float BasinWidth = 600f;

        public const float MillZMax = 1270f;
        const float MillRefZ = 700f;
        const float MillFrontGap = 90f;
        const float MillJitter = 0.3f;
        const float MillVillageGap = 16f * U;
        const float MillPadMargin = 2f * U;
        const float MillPadFalloff = 70f;
        const float MillFacingSpread = 40f;
        const float MillSink = 0.15f * U;
        const int MillTries = 8;

        public const float HerdZMin = 120f, HerdZMax = 300f;
        const float HerdRadiusMin = 9f * U, HerdRadiusMax = 16f * U;
        const float HerdDepthRatio = 0.75f;
        const float CowGap = 3.6f * U;
        const int HerdCountMin = 7, HerdCountMax = 8;
        const float HerdsPerScreen = 2f;
        const float HerdVillageGap = 10f * U, HerdMillGap = 8f * U;
        const float CowSizeJitter = 0.06f;
        const float CowMaxTilt = 18f;
        const float SlopeProbe = 1f * U;
        const int HerdTries = 10, CowTries = 30;

        public const float GroveZMax = 1290f;
        const float GroveRefZ = 500f;
        public const int GroveBands = 3;
        static readonly float[] BandFrom = { 20f, 380f, 840f };
        static readonly float[] BandTo = { 380f, 840f, GroveZMax };
        static readonly float[] BandRate = { 1.17f, 2.2f, 2.2f };
        static readonly int[] BandTreesMin = { 18, 20, 20 };
        static readonly int[] BandTreesMax = { 24, 26, 26 };
        static readonly int[] BandLoneMin = { 2, 7, 7 };
        static readonly int[] BandLoneMax = { 3, 12, 12 };
        const float GroveRadiusXMin = 22f * U, GroveRadiusXMax = 34f * U;
        const float GroveRadiusZMin = 12f * U, GroveRadiusZMax = 20f * U;
        const float TreeGap = 4.2f * U;
        public const float ForeBackZ = 380f;
        const float TreeClear = 2.2f * U;
        const float TreeSizeJitter = 0.12f;
        const float TreeZMin = 20f;
        const float TreeFoot = 0.5f * U;
        const float TreeSlopeSinkMax = 2f * U;
        const int GroveTries = 8, TreeTries = 12;

        public const float HouseZMax = VillageBackZ;
        const float HouseRefZ = 700f;
        const int HouseCountMin = 3, HouseCountMax = 4;
        const float HouseYawSpread = 40f;
        const float HousePadMargin = 1.5f * U;
        const float HousePadFalloff = 60f;
        const float HouseVillageGap = 25f * U;
        const float HouseMillGap = 21f * U;
        const float HouseHerdGap = 10f * U;
        const float HouseGap = 20f * U;
        const float HouseTreeClear = 3f * U;
        const float HouseBareMargin = 0.3f * U;
        const float HouseSink = 0.1f * U;
        const int HouseTries = 24, HouseFlatPicks = 4;

        public const float MaxPadReach = 1100f;

        public struct VillageSpot
        {
            public int model;
            public float x, z;
            public Quaternion rotation;
            public Quaternion inverse;
            public Vector3 origin;
            public float halfX, halfZ;
            public float padY;
            public int seed;
        }

        public struct MillSpot
        {
            public float x, z;
            public Quaternion rotation;
            public Vector3 origin;
            public float radius;
            public float padY;
            public float phase;
        }

        public struct HouseSpot
        {
            public int model;
            public float x, z;
            public Quaternion rotation;
            public Quaternion inverse;
            public Vector3 origin;
            public float halfX, halfZ, radius;
            public float padY;
        }

        public struct CowSpot
        {
            public Vector3 position;
            public Vector3 up;
            public float yaw;
            public bool brown;
            public float scale;
        }

        public struct HerdSpot
        {
            public float x, z, radiusX, radiusZ;
            public CowSpot[] cows;
        }

        public struct TreeSpot
        {
            public Vector3 position;
            public float yaw;
            public int model;
            public bool fore;
            public float scale;
        }

        public struct GroveSpot
        {
            public bool grove;
            public float x, z, radiusX, radiusZ;
            public TreeSpot[] trees;
        }

        public struct Pad
        {
            public float x, z;
            public Quaternion inverse;
            public float halfX, halfZ;
            public bool round;
            public float falloff;
            public float y;

            public float Reach => (round ? halfX : Mathf.Sqrt(halfX * halfX + halfZ * halfZ)) + falloff;

            public float Weight(float px, float pz)
            {
                float outside;
                if (round)
                {
                    float dx = px - x, dz = pz - z;
                    outside = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - halfX);
                }
                else
                {
                    Vector3 local = inverse * new Vector3(px - x, 0f, pz - z);
                    float ox = Mathf.Max(0f, Mathf.Abs(local.x) - halfX);
                    float oz = Mathf.Max(0f, Mathf.Abs(local.z) - halfZ);
                    outside = Mathf.Sqrt(ox * ox + oz * oz);
                }

                if (outside <= 0f) return 1f;
                if (outside >= falloff) return 0f;
                return 1f - Mathf.SmoothStep(0f, 1f, outside / falloff);
            }
        }

        readonly int _seed;
        readonly System.Func<float, float, float> _valleyHeight;
        readonly System.Func<float, float, float> _baseHeight;
        readonly float _camZ;
        readonly float _playPlaneZ;

        public readonly float VillageCell, MillCell, HerdCell, HouseCell;
        readonly float[] _groveCells = new float[GroveBands];

        readonly Dictionary<int, VillageSpot?> _villages = new Dictionary<int, VillageSpot?>();
        readonly Dictionary<int, MillSpot?> _mills = new Dictionary<int, MillSpot?>();
        readonly Dictionary<int, Vector2?> _gunHills = new Dictionary<int, Vector2?>();
        readonly Dictionary<int, HouseSpot[]> _houses = new Dictionary<int, HouseSpot[]>();
        readonly Dictionary<int, HerdSpot?> _herds = new Dictionary<int, HerdSpot?>();
        readonly Dictionary<int, GroveSpot[]>[] _groves = new Dictionary<int, GroveSpot[]>[GroveBands];
        readonly List<Pad> _padScratch = new List<Pad>();
        readonly List<float> _yawScratch = new List<float>();

        public float PlayPlaneZ => _playPlaneZ;

        public VosgesLayout(int seed, System.Func<float, float, float> valleyHeight,
            System.Func<float, float, float> baseHeight, float cameraDistance, float playPlaneZ)
        {
            _seed = seed;
            _valleyHeight = valleyHeight;
            _baseHeight = baseHeight;
            _playPlaneZ = playPlaneZ;
            _camZ = playPlaneZ - cameraDistance;

            VosgesVillage.Measure();
            VosgesWindmill.Measure();
            VosgesHouse.Measure();

            VillageCell = VillageScreens * ScreenWidth(VillageRefZ);
            MillCell = ScreenWidth(MillRefZ);
            HerdCell = ScreenWidth((HerdZMin + HerdZMax) * 0.5f) / HerdsPerScreen;
            HouseCell = ScreenWidth(HouseRefZ);

            float groveScreen = ScreenWidth(GroveRefZ);
            for (int band = 0; band < GroveBands; band++)
            {
                _groveCells[band] = GroveBands * groveScreen / BandRate[band];
                _groves[band] = new Dictionary<int, GroveSpot[]>();
            }
        }

        float ScreenWidth(float z) =>
            2f * (z - _camZ) * Mathf.Tan(LevelCamera.BaseFieldOfView * 0.5f * Mathf.Deg2Rad)
            * LevelCamera.ReferenceAspect;

        public float GroveCell(int band) => _groveCells[Mathf.Clamp(band, 0, GroveBands - 1)];

        public float BandFarZ(int band) => BandTo[Mathf.Clamp(band, 0, GroveBands - 1)];

        public float BaseHeight(float x, float z) => _baseHeight(x, z);

        public bool GunHills { get; set; }

        public bool GunHillNear(float x, out Vector2 spot)
        {
            spot = default;
            if (!GunHills) return false;

            float best = float.MaxValue;
            bool found = false;
            int middle = Mathf.FloorToInt(x / VillageCell);
            for (int cell = middle - 1; cell <= middle + 1; cell++)
            {
                if (!Village(cell, out var v) || !GunHill(cell, out Vector2 at)) continue;
                float gap = Mathf.Abs(v.x - x);
                if (gap >= best) continue;
                best = gap;
                spot = at;
                found = true;
            }
            return found;
        }

        bool GunHill(int cell, out Vector2 spot)
        {
            if (!_gunHills.TryGetValue(cell, out var cached))
            {
                cached = null;
                if (ValleyVillages.Site(this, cell, out VillageSite site))
                {
                    float z = (_playPlaneZ + site.CentreZ) * 0.5f;
                    float x = site.right + ValleyBattery.HillGap;
                    for (int i = 0; i < GunHillTries && InVillage(x, z, ValleyBattery.HillReach); i++)
                        x += GunHillStep;
                    cached = new Vector2(x, z);
                }
                _gunHills[cell] = cached;
            }

            spot = cached ?? default;
            return cached.HasValue;
        }

        float GunHillHalfX => ValleyBattery.HillHalfX + ValleyBattery.HillFalloff;

        float GunHillHalfZ => ValleyBattery.HillHalfZ + ValleyBattery.HillFalloff;

        static float ViewSpread =>
            Mathf.Tan(LevelCamera.BaseFieldOfView * 0.5f * Mathf.Deg2Rad) * LevelCamera.ReferenceAspect;

        bool InGunClear(float x, float z, float margin)
        {
            if (!GunHills) return false;

            int middle = Mathf.FloorToInt(x / VillageCell);
            for (int cell = middle - 1; cell <= middle + 1; cell++)
            {
                if (!GunHill(cell, out Vector2 hill)) continue;
                float half = GunHillHalfX + GunZoneScreens * ScreenWidth(hill.y)
                             + Mathf.Max(0f, z - hill.y) * ViewSpread;
                if (Mathf.Abs(x - hill.x) <= half + margin) return true;
            }
            return false;
        }

        bool InGunHill(float x, float z, float marginX, float marginZ)
        {
            if (!GunHills) return false;

            int middle = Mathf.FloorToInt(x / VillageCell);
            for (int cell = middle - 1; cell <= middle + 1; cell++)
            {
                if (!GunHill(cell, out Vector2 hill)) continue;
                if (Mathf.Abs(x - hill.x) <= GunHillHalfX + marginX
                    && Mathf.Abs(z - hill.y) <= GunHillHalfZ + marginZ) return true;
            }
            return false;
        }

        bool InGunView(float x, float z, float marginX, float marginZ)
        {
            if (!GunHills) return false;
            if (InGunHill(x, z, marginX, marginZ)) return true;

            int middle = Mathf.FloorToInt(x / VillageCell);
            for (int cell = middle - 1; cell <= middle + 1; cell++)
            {
                if (!GunHill(cell, out Vector2 hill)) continue;
                if (z - marginZ > hill.y) continue;
                if (Mathf.Abs(x - hill.x) <= ValleyBattery.HillHalfX + GunFrontPad + marginX) return true;
            }
            return false;
        }

        public float Ground(float x, float z) => Ground(x, z, true);

        float Ground(float x, float z, bool houses)
        {
            float h = _baseHeight(x, z);
            PadsForRange(x - MaxPadReach, x + MaxPadReach, _padScratch, houses);
            foreach (var pad in _padScratch)
            {
                float w = pad.Weight(x, z);
                if (w > 0f) h = Mathf.Lerp(h, pad.y, w);
            }
            return h;
        }

        Vector3 GroundNormal(float x, float z, bool houses)
        {
            float dx = Ground(x + SlopeProbe, z, houses) - Ground(x - SlopeProbe, z, houses);
            float dz = Ground(x, z + SlopeProbe, houses) - Ground(x, z - SlopeProbe, houses);
            return new Vector3(-dx, 2f * SlopeProbe, -dz).normalized;
        }

        public float HillMask(float x, float z)
        {
            int cell = Mathf.FloorToInt(x / VillageCell);
            float nearest = float.MaxValue;
            for (int c = cell - 1; c <= cell + 1; c++)
            {
                if (!Village(c, out var v)) continue;
                Vector3 local = v.inverse * new Vector3(x - v.x, 0f, z - v.z);
                float ox = Mathf.Max(0f, Mathf.Abs(local.x) - v.halfX - VillagePadMargin);
                float oz = Mathf.Max(0f, Mathf.Abs(local.z) - v.halfZ - VillagePadMargin);
                nearest = Mathf.Min(nearest, ox * ox + oz * oz);
            }

            if (nearest == float.MaxValue) return 1f;
            return Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(VillagePadFalloff, VillagePadFalloff + BasinWidth, Mathf.Sqrt(nearest)));
        }

        public void PadsForRange(float xMin, float xMax, List<Pad> list, bool houses = true)
        {
            int h0 = Mathf.FloorToInt(xMin / HouseCell) - 1;
            int h1 = Mathf.FloorToInt(xMax / HouseCell) + 1;
            if (houses)
                for (int cell = h0; cell <= h1; cell++) Houses(cell);

            list.Clear();

            int v0 = Mathf.FloorToInt((xMin - MaxPadReach) / VillageCell);
            int v1 = Mathf.FloorToInt((xMax + MaxPadReach) / VillageCell);
            for (int cell = v0; cell <= v1; cell++)
            {
                if (!Village(cell, out var v)) continue;
                var pad = new Pad
                {
                    x = v.x, z = v.z, inverse = v.inverse,
                    halfX = v.halfX + VillagePadMargin, halfZ = v.halfZ + VillagePadMargin,
                    falloff = VillagePadFalloff, y = v.padY,
                };
                if (v.x + pad.Reach < xMin || v.x - pad.Reach > xMax) continue;
                list.Add(pad);
            }

            int m0 = Mathf.FloorToInt(xMin / MillCell) - 1;
            int m1 = Mathf.FloorToInt(xMax / MillCell) + 1;
            for (int cell = m0; cell <= m1; cell++)
            {
                if (!Mill(cell, out var m)) continue;
                var pad = new Pad
                {
                    x = m.x, z = m.z, inverse = Quaternion.identity,
                    halfX = m.radius + MillPadMargin, halfZ = m.radius + MillPadMargin,
                    round = true, falloff = MillPadFalloff, y = m.padY,
                };
                if (m.x + pad.Reach < xMin || m.x - pad.Reach > xMax) continue;
                list.Add(pad);
            }

            if (!houses) return;

            for (int cell = h0; cell <= h1; cell++)
                foreach (var h in Houses(cell))
                {
                    var pad = new Pad
                    {
                        x = h.x, z = h.z, inverse = h.inverse,
                        halfX = h.halfX + HousePadMargin, halfZ = h.halfZ + HousePadMargin,
                        falloff = HousePadFalloff, y = h.padY,
                    };
                    if (h.x + pad.Reach < xMin || h.x - pad.Reach > xMax) continue;
                    list.Add(pad);
                }
        }

        public bool Bare(float x, float z) =>
            InMill(x, z, MillPadMargin) || VillageCovered(x, z) || InHouse(x, z, HouseBareMargin);

        bool VillageCovered(float x, float z)
        {
            int cell = Mathf.FloorToInt(x / VillageCell);
            for (int c = cell - 1; c <= cell + 1; c++)
            {
                if (!Village(c, out var v)) continue;
                Vector3 rect = v.inverse * new Vector3(x - v.x, 0f, z - v.z);
                if (Mathf.Abs(rect.x) > v.halfX || Mathf.Abs(rect.z) > v.halfZ) continue;

                Vector3 local = v.inverse * new Vector3(x - v.origin.x, 0f, z - v.origin.z) / U;
                return VosgesVillage.Covered(v.model, local);
            }
            return false;
        }

        public bool Occupied(float x, float z, float margin) =>
            InVillage(x, z, margin) || InMill(x, z, margin) || InHerd(x, z, margin) || InGrove(x, z, margin)
            || InHouse(x, z, margin);

        public bool InHouse(float x, float z, float margin)
        {
            int cell = Mathf.FloorToInt(x / HouseCell);
            for (int c = cell - 1; c <= cell + 1; c++)
                foreach (var h in Houses(c))
                {
                    Vector3 local = h.inverse * new Vector3(x - h.x, 0f, z - h.z);
                    if (Mathf.Abs(local.x) <= h.halfX + margin && Mathf.Abs(local.z) <= h.halfZ + margin)
                        return true;
                }
            return false;
        }

        public bool InVillage(float x, float z, float margin)
        {
            int cell = Mathf.FloorToInt(x / VillageCell);
            for (int c = cell - 1; c <= cell + 1; c++)
            {
                if (!Village(c, out var v)) continue;
                Vector3 local = v.inverse * new Vector3(x - v.x, 0f, z - v.z);
                if (Mathf.Abs(local.x) <= v.halfX + margin && Mathf.Abs(local.z) <= v.halfZ + margin)
                    return true;
            }
            return false;
        }

        public bool InMill(float x, float z, float margin)
        {
            int cell = Mathf.FloorToInt(x / MillCell);
            for (int c = cell - 1; c <= cell + 1; c++)
            {
                if (!Mill(c, out var m)) continue;
                float dx = x - m.x, dz = z - m.z, r = m.radius + margin;
                if (dx * dx + dz * dz <= r * r) return true;
            }
            return false;
        }

        public bool InHerd(float x, float z, float margin)
        {
            int cell = Mathf.FloorToInt(x / HerdCell);
            for (int c = cell - 1; c <= cell + 1; c++)
            {
                if (!Herd(c, out var h)) continue;
                float dx = (x - h.x) / (h.radiusX + margin);
                float dz = (z - h.z) / (h.radiusZ + margin);
                if (dx * dx + dz * dz <= 1f) return true;
            }
            return false;
        }

        public bool InGrove(float x, float z, float margin)
        {
            for (int band = 0; band < GroveBands; band++)
            {
                int cell = Mathf.FloorToInt(x / _groveCells[band]);
                for (int c = cell - 1; c <= cell + 1; c++)
                    foreach (var g in Groves(band, c))
                    {
                        if (!g.grove) continue;
                        float dx = (x - g.x) / (g.radiusX + margin);
                        float dz = (z - g.z) / (g.radiusZ + margin);
                        if (dx * dx + dz * dz <= 1f) return true;
                    }
            }
            return false;
        }

        public bool Village(int cell, out VillageSpot spot)
        {
            if (!_villages.TryGetValue(cell, out var cached))
            {
                cached = TryVillage(cell, out var made) ? made : (VillageSpot?)null;
                _villages[cell] = cached;
            }

            spot = cached ?? default;
            return cached.HasValue;
        }

        bool TryVillage(int cell, out VillageSpot spot)
        {
            spot = default;
            int model = VillageModel(cell);
            if (!VosgesVillage.Has(model)) return false;

            var rng = new System.Random(Hash(_seed, cell, 101));
            Bounds box = VosgesVillage.Footprint(model);
            Vector3 church = VosgesVillage.Church(model) - box.center;
            float ex = box.extents.x * U, ez = box.extents.z * U;

            float front = _playPlaneZ + VillageFrontGap;
            float room = (VillageBackZ - front) * 0.5f;

            float bestYaw = 0f, bestSpan = float.MaxValue;
            float start = (float)rng.NextDouble() * 360f;
            _yawScratch.Clear();
            for (int i = 0; i < VillageYawSteps; i++)
            {
                float yaw = start + i * 360f / VillageYawSteps;
                var rot = Quaternion.Euler(0f, yaw, 0f);
                if ((rot * church).z > 0f) continue;

                float span = SpanZ(rot, ex, ez);
                if (span <= room) _yawScratch.Add(yaw);
                if (span < bestSpan)
                {
                    bestSpan = span;
                    bestYaw = yaw;
                }
            }

            float chosen = _yawScratch.Count > 0 ? _yawScratch[rng.Next(_yawScratch.Count)] : bestYaw;
            var rotation = Quaternion.Euler(0f, chosen, 0f);
            float halfDepth = SpanZ(rotation, ex, ez);

            float x = (cell + 0.5f + Range(rng, -VillageJitter, VillageJitter)) * VillageCell;
            float z = front + halfDepth;

            float padY = _valleyHeight(x, z);
            Vector3 offset = rotation * new Vector3(box.center.x * U, 0f, box.center.z * U);

            spot = new VillageSpot
            {
                model = model,
                x = x,
                z = z,
                rotation = rotation,
                inverse = Quaternion.Inverse(rotation),
                origin = new Vector3(x, padY, z) - offset,
                halfX = ex,
                halfZ = ez,
                padY = padY,
                seed = Hash(_seed, cell, 103),
            };
            return true;
        }

        static float SpanZ(Quaternion rot, float ex, float ez)
        {
            Vector3 a = rot * new Vector3(ex, 0f, ez);
            Vector3 b = rot * new Vector3(ex, 0f, -ez);
            return Mathf.Max(Mathf.Abs(a.z), Mathf.Abs(b.z));
        }

        int VillageModel(int cell)
        {
            int count = VosgesVillage.Models;
            int step = cell >= 0 ? 1 : -1;
            int model = (int)((uint)Hash(_seed, 0, 107) % count);

            for (int c = step; c != cell + step; c += step)
                model = (model + 1 + (int)((uint)Hash(_seed, c, 109) % (count - 1))) % count;

            return model;
        }

        public bool Mill(int cell, out MillSpot spot)
        {
            if (!_mills.TryGetValue(cell, out var cached))
            {
                cached = TryMill(cell, out var made) ? made : (MillSpot?)null;
                _mills[cell] = cached;
            }

            spot = cached ?? default;
            return cached.HasValue;
        }

        const int GunHillTries = 40;
        const float GunHillStep = 30f;
        const float GunZoneScreens = 0.5f;
        const float GunFrontPad = 60f;

        bool TryMill(int cell, out MillSpot spot)
        {
            spot = default;
            if (!VosgesWindmill.Ready) return false;

            var rng = new System.Random(Hash(_seed, cell, 201));
            float radius = VosgesWindmill.Radius * U;
            float bestSlope = float.MaxValue;
            bool found = false;

            for (int attempt = 0; attempt < MillTries; attempt++)
            {
                float x = (cell + 0.5f + Range(rng, -MillJitter, MillJitter)) * MillCell;
                float z = Range(rng, _playPlaneZ + MillFrontGap, MillZMax);
                float facing = VosgesWindmill.FacingYaw + Range(rng, -MillFacingSpread, MillFacingSpread);
                float phase = (float)rng.NextDouble() * 360f;
                if (InVillage(x, z, radius + MillVillageGap)) continue;
                if (InGunClear(x, z, radius)) continue;

                float reach = radius + MillPadMargin;
                float slope = Mathf.Max(
                    Mathf.Abs(_baseHeight(x + reach, z) - _baseHeight(x - reach, z)),
                    Mathf.Abs(_baseHeight(x, z + reach) - _baseHeight(x, z - reach)));
                if (slope >= bestSlope) continue;

                var rotation = Quaternion.Euler(0f, facing, 0f);
                float padY = _baseHeight(x, z);
                Vector3 offset = rotation * (VosgesWindmill.Centre * U);

                bestSlope = slope;
                found = true;
                spot = new MillSpot
                {
                    x = x,
                    z = z,
                    rotation = rotation,
                    origin = new Vector3(x - offset.x, padY - MillSink, z - offset.z),
                    radius = radius,
                    padY = padY,
                    phase = phase,
                };
            }

            return found;
        }

        public HouseSpot[] Houses(int cell)
        {
            if (_houses.TryGetValue(cell, out var cached)) return cached;
            cached = MakeHouses(cell);
            _houses[cell] = cached;
            return cached;
        }

        HouseSpot[] MakeHouses(int cell)
        {
            if (!VosgesHouse.Ready) return System.Array.Empty<HouseSpot>();

            var rng = new System.Random(Hash(_seed, cell, 501));
            var models = new int[VosgesHouse.Models];
            for (int i = 0; i < models.Length; i++) models[i] = i;
            for (int i = models.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (models[i], models[j]) = (models[j], models[i]);
            }

            int count = rng.Next(HouseCountMin, HouseCountMax + 1);
            var placed = new List<HouseSpot>(count);
            for (int i = 0; i < models.Length && placed.Count < count; i++)
            {
                if (!VosgesHouse.Has(models[i])) continue;
                if (TryHouse(rng, cell, models[i], placed, out var spot)) placed.Add(spot);
            }

            return placed.ToArray();
        }

        bool TryHouse(System.Random rng, int cell, int model, List<HouseSpot> placed, out HouseSpot spot)
        {
            spot = default;
            Bounds box = VosgesHouse.Footprint(model);
            float ex = box.extents.x * U, ez = box.extents.z * U;
            float radius = Mathf.Sqrt(ex * ex + ez * ez);
            float left = cell * HouseCell + radius + HouseGap * 0.5f;
            float right = (cell + 1) * HouseCell - radius - HouseGap * 0.5f;
            float front = _playPlaneZ + VillageFrontGap;
            if (right <= left) return false;

            float bestSlope = float.MaxValue;
            int picks = 0;
            for (int attempt = 0; attempt < HouseTries && picks < HouseFlatPicks; attempt++)
            {
                float yaw = VosgesHouse.Facing(model) + Range(rng, -HouseYawSpread, HouseYawSpread);
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                float halfDepth = SpanZ(rotation, ex, ez);
                float zMin = front + halfDepth, zMax = HouseZMax - halfDepth;
                float x = Range(rng, left, right);
                float z = Range(rng, zMin, zMax);
                if (zMax <= zMin) continue;
                if (InVillage(x, z, radius + HouseVillageGap)) continue;
                if (InGunClear(x, z, radius)) continue;
                if (InMill(x, z, radius + HouseMillGap)) continue;
                if (InHerd(x, z, radius + HouseHerdGap)) continue;
                if (Crowded(placed, x, z, radius)) continue;

                picks++;
                float reach = radius + HousePadMargin;
                float slope = Mathf.Max(
                    Mathf.Abs(_baseHeight(x + reach, z) - _baseHeight(x - reach, z)),
                    Mathf.Abs(_baseHeight(x, z + reach) - _baseHeight(x, z - reach)));
                if (slope >= bestSlope) continue;

                bestSlope = slope;
                float padY = _baseHeight(x, z);
                Vector3 offset = rotation * new Vector3(box.center.x * U, 0f, box.center.z * U);
                spot = new HouseSpot
                {
                    model = model,
                    x = x,
                    z = z,
                    rotation = rotation,
                    inverse = Quaternion.Inverse(rotation),
                    origin = new Vector3(x - offset.x, padY - box.min.y * U - HouseSink, z - offset.z),
                    halfX = ex,
                    halfZ = ez,
                    radius = radius,
                    padY = padY,
                };
            }

            return picks > 0;
        }

        static bool Crowded(List<HouseSpot> houses, float x, float z, float radius)
        {
            foreach (var h in houses)
            {
                float dx = h.x - x, dz = h.z - z, gap = h.radius + radius + HouseGap;
                if (dx * dx + dz * dz < gap * gap) return true;
            }
            return false;
        }

        public bool Herd(int cell, out HerdSpot spot)
        {
            if (!_herds.TryGetValue(cell, out var cached))
            {
                cached = TryHerd(cell, out var made) ? made : (HerdSpot?)null;
                _herds[cell] = cached;
            }

            spot = cached ?? default;
            return cached.HasValue;
        }

        bool TryHerd(int cell, out HerdSpot spot)
        {
            spot = default;
            var rng = new System.Random(Hash(_seed, cell, 301));

            for (int attempt = 0; attempt < HerdTries; attempt++)
            {
                float rx = Range(rng, HerdRadiusMin, HerdRadiusMax);
                float rz = Mathf.Min(rx * HerdDepthRatio, (HerdZMax - HerdZMin) * 0.5f);
                float x = (cell + Range(rng, 0.2f, 0.8f)) * HerdCell;
                float z = Range(rng, HerdZMin + rz, HerdZMax - rz);
                float reachOut = Mathf.Max(rx, rz);
                if (InVillage(x, z, reachOut + HerdVillageGap)) continue;
                if (InMill(x, z, reachOut + HerdMillGap)) continue;
                if (InGunHill(x, z, rx, rz)) continue;

                int count = rng.Next(HerdCountMin, HerdCountMax + 1);
                var cows = new List<CowSpot>(count);
                for (int i = 0; i < count; i++)
                    for (int tries = 0; tries < CowTries; tries++)
                    {
                        float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float reach = Mathf.Sqrt((float)rng.NextDouble());
                        float cx = x + Mathf.Cos(angle) * reach * rx;
                        float cz = z + Mathf.Sin(angle) * reach * rz;
                        if (TooClose(cows, cx, cz, CowGap)) continue;

                        cows.Add(new CowSpot
                        {
                            position = new Vector3(cx, Ground(cx, cz, false), cz),
                            up = Tilt(GroundNormal(cx, cz, false), CowMaxTilt),
                            yaw = (float)rng.NextDouble() * 360f,
                            brown = rng.NextDouble() < 0.5,
                            scale = 1f + Range(rng, -CowSizeJitter, CowSizeJitter),
                        });
                        break;
                    }

                spot = new HerdSpot { x = x, z = z, radiusX = rx, radiusZ = rz, cows = cows.ToArray() };
                return true;
            }

            return false;
        }

        static Vector3 Tilt(Vector3 normal, float maxAngle)
        {
            float angle = Vector3.Angle(Vector3.up, normal);
            if (angle <= maxAngle) return normal;
            return Vector3.Slerp(Vector3.up, normal, maxAngle / angle);
        }

        static bool TooClose(List<CowSpot> cows, float x, float z, float gap)
        {
            foreach (var c in cows)
            {
                float dx = c.position.x - x, dz = c.position.z - z;
                if (dx * dx + dz * dz < gap * gap) return true;
            }
            return false;
        }

        public GroveSpot[] Groves(int band, int cell)
        {
            var cache = _groves[band];
            if (cache.TryGetValue(cell, out var cached)) return cached;
            cached = MakeGroves(band, cell);
            cache[cell] = cached;
            return cached;
        }

        GroveSpot[] MakeGroves(int band, int cell)
        {
            var result = new List<GroveSpot>(2);
            var rng = new System.Random(Hash(_seed, cell, 401 + band * 7));
            float width = _groveCells[band];
            float zLow = Mathf.Max(BandFrom[band], TreeZMin);

            for (int attempt = 0; attempt < GroveTries; attempt++)
            {
                float rx = Range(rng, GroveRadiusXMin, GroveRadiusXMax);
                float rz = Range(rng, GroveRadiusZMin, GroveRadiusZMax);
                float x = (cell + Range(rng, 0.1f, 0.9f)) * width;
                float zFrom = band == 0
                    ? Mathf.Max(BandFrom[band], TreeZMin + rz * 0.5f)
                    : Mathf.Max(BandFrom[band], ForeBackZ + rz);
                float z = Range(rng, zFrom, BandTo[band]);
                if (InVillage(x, z, Mathf.Min(rx, rz))) continue;
                if (InGunView(x, z, rx, rz)) continue;

                var trees = new List<TreeSpot>();
                int count = rng.Next(BandTreesMin[band], BandTreesMax[band] + 1);
                for (int i = 0; i < count; i++)
                    for (int tries = 0; tries < TreeTries; tries++)
                    {
                        float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float reach = Mathf.Sqrt((float)rng.NextDouble());
                        float tx = x + Mathf.Cos(angle) * reach * rx;
                        float tz = z + Mathf.Sin(angle) * reach * rz;
                        if (tz < TreeZMin || tz > GroveZMax) continue;
                        if (TooClose(trees, tx, tz, TreeGap)) continue;
                        if (Blocked(tx, tz)) continue;

                        trees.Add(Tree(rng, tx, tz));
                        break;
                    }

                result.Add(new GroveSpot
                {
                    grove = true, x = x, z = z, radiusX = rx, radiusZ = rz, trees = trees.ToArray(),
                });
                break;
            }

            var lone = new List<TreeSpot>();
            int loneCount = rng.Next(BandLoneMin[band], BandLoneMax[band] + 1);
            for (int i = 0; i < loneCount; i++)
                for (int tries = 0; tries < TreeTries; tries++)
                {
                    float tx = (cell + (float)rng.NextDouble()) * width;
                    float tz = Range(rng, zLow, BandTo[band]);
                    if (Blocked(tx, tz)) continue;
                    if (result.Count > 0 && TooClose(result[0].trees, tx, tz, TreeGap)) continue;
                    if (TooClose(lone, tx, tz, TreeGap)) continue;

                    lone.Add(Tree(rng, tx, tz));
                    break;
                }

            if (lone.Count > 0) result.Add(new GroveSpot { grove = false, trees = lone.ToArray() });
            return result.ToArray();
        }

        bool Blocked(float x, float z) =>
            InVillage(x, z, TreeClear) || InMill(x, z, TreeClear) || InHerd(x, z, TreeClear)
            || InHouse(x, z, HouseTreeClear) || InGunView(x, z, TreeClear, TreeClear);

        TreeSpot Tree(System.Random rng, float x, float z)
        {
            Vector3 normal = GroundNormal(x, z, true);
            float slope = Mathf.Sqrt(normal.x * normal.x + normal.z * normal.z) / Mathf.Max(normal.y, 0.2f);
            float sink = Mathf.Min(slope * TreeFoot, TreeSlopeSinkMax);

            return new TreeSpot
            {
                position = new Vector3(x, Ground(x, z) - sink, z),
                yaw = (float)rng.NextDouble() * 360f,
                model = rng.Next(VosgesSpruce.Models),
                fore = z < ForeBackZ,
                scale = 1f + Range(rng, -TreeSizeJitter, TreeSizeJitter),
            };
        }

        static bool TooClose(IList<TreeSpot> trees, float x, float z, float gap)
        {
            if (trees == null) return false;
            foreach (var t in trees)
            {
                float dx = t.position.x - x, dz = t.position.z - z;
                if (dx * dx + dz * dz < gap * gap) return true;
            }
            return false;
        }

        static float Range(System.Random rng, float min, float max) =>
            Mathf.Lerp(min, max, (float)rng.NextDouble());

        static int Hash(int seed, int cell, int salt)
        {
            unchecked
            {
                int h = seed;
                h = h * 486187739 + cell;
                h = h * 486187739 + salt;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
