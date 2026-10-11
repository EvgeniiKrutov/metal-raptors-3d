using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class FlandersTerrain : CampaignTerrain
    {
        public const float SeaLevel = SeaSurface.Level;
        public const float BeachZMax = ShoreZ + ShoreZJitter;

        const float BaseY = -115f;
        const float Scale = 205f;
        const float LowestPoint = -110f;
        const float HighestPoint = 86f;

        const float PlainRise = 11f;
        const float PlainBroad = 4.5f;
        const float PlainFine = 1.6f;
        const float PlainMicro = 0.9f, PlainGrit = 0.25f;
        const float MicroWave = 38f, GritWave = 26f;
        const float MicroOnBeach = 0f;
        const float ShelfSink = -14f;
        const float SeabedY = -95f;
        const float SeabedRelief = 12f;

        const float ShoreZ = 300f, ShoreZJitter = 45f;
        const float ShoreWidth = 30f, ShoreWidthJitter = 8f;
        const float ShelfRun = 60f, DeepRun = 320f;

        const float CraterCellSize = 150f;
        const float CratersPerCell = 1.6f;
        const float MaxCraterReach = 55f;
        const float CraterZMin = 12f, CraterZMax = 130f;

        const float DykeCellSize = 420f;
        const float DykeChance = 0.55f;
        const float MaxDykeReach = 150f;
        const float DykeFadeFrom = 120f, DykeFadeTo = 190f;

        const float GroyneCellSize = 320f;
        const float GroyneXMin = 0.35f, GroyneXMax = 0.65f;
        const float GroyneInsetMin = 4f, GroyneInsetMax = 14f;
        const float GroyneAngleMin = 40f, GroyneAngleMax = 52f;
        const float GroyneDrop = 6f;
        const float GroyneHalfWidth = 5f;

        const float BunkerNearGap = 2f;
        const int BunkerDepthBands = 3;
        const float BunkerFrontGap = 12f;
        const float BunkerGroyneGap = 30f;
        const float BunkerJitter = 0.06f;
        const float BunkerYawMin = 40f, BunkerYawMax = 60f;
        const float BunkerPadMargin = 3f, BunkerPadFalloff = 18f;
        const float BunkerSink = 0.6f;
        const float MaxBunkerReach = 160f;

        const int AlphaRes = 128;
        const int RowsPerStep = 16;

        const float SandInland = 250f, SandSeaward = 10f;

        const float GroundTile = ChunkLength / 6f, SandTile = ChunkLength / 8f;

        static readonly Color GroundColor = new Color(0.46f, 0.42f, 0.33f);
        static readonly Color WallColor = new Color(0.38f, 0.35f, 0.28f);
        static readonly Color SandColor = new Color(0.74f, 0.70f, 0.59f);

        float _p1, _p2, _p3, _p4, _p5, _p6, _d1, _d2, _m1, _m2, _m3, _m4;
        float _groyneSide;
        float _bunkerSpacing;

        TerrainLayer _groundLayer, _sandLayer;
        Material _wallMat;

        struct CraterSpec
        {
            public float x, z, radius, depth, rim, rimSigma, influence;
        }

        struct DykeSpec
        {
            public float x0, lean, halfWidth, rise;
        }

        struct GroyneSpec
        {
            public Vector3 land, run;
        }

        struct BunkerSpec
        {
            public int model;
            public Vector3 origin;
            public Quaternion rotation;
            public float x, z, halfX, halfZ, reach, padY;
        }

        readonly List<CraterSpec> _craters = new List<CraterSpec>();
        readonly List<DykeSpec> _dykes = new List<DykeSpec>();
        readonly List<BunkerSpec> _bunkers = new List<BunkerSpec>();
        readonly Dictionary<int, BunkerSpec> _bunkerCells = new Dictionary<int, BunkerSpec>();
        readonly Dictionary<int, GroyneSpec> _groyneCells = new Dictionary<int, GroyneSpec>();
        float[] _shoreCentre, _shoreHalf;

        protected override float HeightScale => Scale;
        protected override float TerrainY => BaseY;
        protected override float MinHeight => LowestPoint;
        protected override float MaxHeight => HighestPoint;
        protected override float DetailDistance => 0f;

        protected override void Prepare(Daytime daytime, Weather weather,
            float cameraDistance, float playPlaneZ)
        {
            var rng = new System.Random(_seed);
            _p1 = Offset(rng); _p2 = Offset(rng); _p3 = Offset(rng);
            _p4 = Offset(rng); _p5 = Offset(rng); _p6 = Offset(rng);
            _d1 = Offset(rng); _d2 = Offset(rng);
            _m1 = Offset(rng); _m2 = Offset(rng);
            _m3 = Offset(rng); _m4 = Offset(rng);
            _groyneSide = rng.NextDouble() < 0.5 ? -1f : 1f;

            _groundLayer = TerrainSurfaces.GrainLayer("Coast (grain)", GroundColor, GroundTile, _seed);
            _sandLayer = TerrainSurfaces.FlatLayer("Coast (flat sand)", SandColor, SandTile);

            _wallMat = TerrainSurfaces.FlatMaterial(WallColor);

            float beach = (ShoreZ - ShoreWidth - BunkerFrontGap + BunkerNearGap) * 0.5f
                          - (playPlaneZ - cameraDistance);
            _bunkerSpacing = 2f * beach * LevelCamera.ReferenceAspect
                             * Mathf.Tan(LevelCamera.BaseFieldOfView * 0.5f * Mathf.Deg2Rad);

            CoastSky.ApplyFog(daytime, cameraDistance, playPlaneZ);
        }

        protected override IEnumerator FillHeights(float[,] metres, int index)
        {
            float x0 = index * ChunkLength;
            CratersForRange(x0 - MaxCraterReach, x0 + ChunkLength + MaxCraterReach, _craters);
            DykesForRange(x0 - MaxDykeReach, x0 + ChunkLength + MaxDykeReach, _dykes);
            BunkersForRange(x0 - MaxBunkerReach, x0 + ChunkLength + MaxBunkerReach, _bunkers);
            CacheShore(index);
            yield return null;

            for (int iz = 0; iz < Res; iz += RowsPerStep)
            {
                FillRows(metres, index, iz, Mathf.Min(Res, iz + RowsPerStep));
                yield return null;
            }

            foreach (var c in _craters)
            {
                StampCrater(metres, index, c);
                yield return null;
            }

            foreach (var d in _dykes)
            {
                StampDyke(metres, index, d);
                yield return null;
            }

            foreach (var b in _bunkers)
            {
                StampBunkerPad(metres, index, b);
                yield return null;
            }
        }

        protected override void PaintChunk(TerrainData data, int index)
        {
            data.terrainLayers = new[] { _groundLayer, _sandLayer };
            data.alphamapResolution = AlphaRes;

            float x0 = index * ChunkLength;
            var alpha = new float[AlphaRes, AlphaRes, 2];

            for (int iz = 0; iz < AlphaRes; iz++)
            {
                float z = Depth * iz / (AlphaRes - 1);
                for (int ix = 0; ix < AlphaRes; ix++)
                {
                    float x = x0 + ChunkLength * ix / (AlphaRes - 1);
                    float centre = ShoreCentre(x);
                    float sand = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(centre - SandInland, centre - SandSeaward, z));

                    alpha[iz, ix, 0] = 1f - sand;
                    alpha[iz, ix, 1] = sand;
                }
            }

            data.SetAlphamaps(0, 0, alpha);
        }

        protected override IEnumerable<object> Decorate(TerrainData data, int index)
        {
            yield break;
        }

        protected override void AddChunkMeshes(int index, Transform root, float[] cutLine,
            List<Mesh> owned)
        {
            float x0 = index * ChunkLength;

            AddMesh(root, "Cut Wall", ProceduralTerrain.BuildCutWallMesh(cutLine, ChunkLength),
                _wallMat, new Vector3(x0 + ChunkLength / 2f, 0f, 0f), owned);

            AddGroynes(index, root);
            AddBunkers(index, root);
        }

        void AddGroynes(int index, Transform root)
        {
            if (!FlandersGroyne.Measure()) return;

            float x0 = index * ChunkLength;
            int c0 = Mathf.FloorToInt(x0 / GroyneCellSize);
            int c1 = Mathf.FloorToInt((x0 + ChunkLength) / GroyneCellSize);

            for (int cell = c0; cell <= c1; cell++)
            {
                var groyne = Groyne(cell);
                if (groyne.land.x < x0 || groyne.land.x >= x0 + ChunkLength) continue;

                FlandersGroyne.Place(root, groyne.land,
                    groyne.land + groyne.run + Vector3.down * GroyneDrop, BaseHeight);
            }
        }

        GroyneSpec Groyne(int cell)
        {
            if (_groyneCells.TryGetValue(cell, out var spec)) return spec;

            var rng = GroyneRng(cell);
            float x = GroyneX(cell, rng);
            float z = ShoreCentre(x) - ShoreHalf(x) - Range(rng, GroyneInsetMin, GroyneInsetMax);
            float angle = Range(rng, GroyneAngleMin, GroyneAngleMax) * Mathf.Deg2Rad;

            spec = new GroyneSpec
            {
                land = new Vector3(x, BaseHeight(x, z), z),
                run = new Vector3(_groyneSide * Mathf.Sin(angle), 0f, Mathf.Cos(angle))
                      * FlandersGroyne.Length,
            };
            _groyneCells[cell] = spec;
            return spec;
        }

        bool NearGroyne(float worldX, float z, float margin)
        {
            if (!FlandersGroyne.Measure()) return false;

            float clear = GroyneHalfWidth + margin;
            int reach = Mathf.CeilToInt((FlandersGroyne.Length + clear) / GroyneCellSize);
            int cell = Mathf.FloorToInt(worldX / GroyneCellSize);

            for (int c = cell - reach; c <= cell + reach; c++)
            {
                var groyne = Groyne(c);
                var run = new Vector2(groyne.run.x, groyne.run.z);
                var point = new Vector2(worldX - groyne.land.x, z - groyne.land.z);
                float t = Mathf.Clamp01(Vector2.Dot(point, run) / run.sqrMagnitude);
                if ((point - run * t).sqrMagnitude <= clear * clear) return true;
            }

            return false;
        }

        System.Random GroyneRng(int cell) => new System.Random(Hash(_seed, cell, 17));

        float GroyneX(int cell, System.Random rng) =>
            (cell + Range(rng, GroyneXMin, GroyneXMax)) * GroyneCellSize;

        void AddBunkers(int index, Transform root)
        {
            float x0 = index * ChunkLength;
            BunkersForRange(x0, x0 + ChunkLength, _bunkers);

            foreach (var b in _bunkers)
                if (b.x >= x0 && b.x < x0 + ChunkLength)
                    FlandersBunker.Place(root, b.model, b.origin, b.rotation);
        }

        void BunkersForRange(float xMin, float xMax, List<BunkerSpec> list)
        {
            list.Clear();
            int c0 = Mathf.FloorToInt(xMin / _bunkerSpacing);
            int c1 = Mathf.FloorToInt(xMax / _bunkerSpacing);

            for (int cell = c0; cell <= c1; cell++)
                if (Bunker(cell, out var spec)) list.Add(spec);
        }

        public override bool Occupied(float worldX, float z, float margin)
        {
            int cell = Mathf.FloorToInt(worldX / _bunkerSpacing);

            for (int c = cell - 1; c <= cell + 1; c++)
            {
                if (!Bunker(c, out var b)) continue;

                Vector3 local = Quaternion.Inverse(b.rotation) * new Vector3(worldX - b.x, 0f, z - b.z);
                if (Mathf.Abs(local.x) <= b.halfX + margin && Mathf.Abs(local.z) <= b.halfZ + margin)
                    return true;
            }

            return NearGroyne(worldX, z, margin);
        }

        bool Bunker(int cell, out BunkerSpec spec)
        {
            if (_bunkerCells.TryGetValue(cell, out spec)) return true;
            if (!TryBunker(cell, out spec)) return false;

            _bunkerCells[cell] = spec;
            return true;
        }

        bool TryBunker(int cell, out BunkerSpec spec)
        {
            spec = default;
            int model = BunkerModel(cell);
            if (!FlandersBunker.Has(model)) return false;

            var rng = new System.Random(Hash(_seed, cell, 23));
            float yaw = Range(rng, BunkerYawMin, BunkerYawMax) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            var rotation = Quaternion.Euler(0f, yaw, 0f);

            Bounds box = FlandersBunker.Footprint(model);
            float halfX = box.extents.x + BunkerPadMargin;
            float halfZ = box.extents.z + BunkerPadMargin;
            float sin = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad));
            float cos = Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad));
            float spanX = cos * box.extents.x + sin * box.extents.z;
            float spanZ = sin * box.extents.x + cos * box.extents.z;

            float x = (cell + 0.5f + Range(rng, -BunkerJitter, BunkerJitter)) * _bunkerSpacing;
            x = ClearOfGroynes(x, spanX + BunkerGroyneGap);

            float centre = ShoreCentre(x);
            float front = centre - ShoreHalf(x) - BunkerFrontGap - spanZ;
            float back = BunkerNearGap + spanZ;
            float depth = (DepthBand(cell) + (float)rng.NextDouble()) / BunkerDepthBands;
            float z = back < front ? Mathf.Lerp(back, front, depth) : front;

            float padY = BaseHeight(x, z);
            Vector3 offset = rotation * new Vector3(box.center.x, 0f, box.center.z);

            spec = new BunkerSpec
            {
                model = model,
                rotation = rotation,
                origin = new Vector3(x, padY - BunkerSink, z) - offset,
                x = x,
                z = z,
                halfX = halfX,
                halfZ = halfZ,
                reach = Mathf.Sqrt(halfX * halfX + halfZ * halfZ) + BunkerPadFalloff,
                padY = padY,
            };
            return true;
        }

        int BunkerModel(int cell)
        {
            int count = FlandersBunker.Models;
            int step = cell >= 0 ? 1 : -1;
            int model = (int)((uint)Hash(_seed, 0, 29) % count);

            for (int c = step; c != cell + step; c += step)
                model = (model + 1 + (int)((uint)Hash(_seed, c, 31) % (count - 1))) % count;

            return model;
        }

        int DepthBand(int cell)
        {
            int block = Mathf.FloorToInt((float)cell / BunkerDepthBands);
            var rng = new System.Random(Hash(_seed, block, 37));
            var bands = new int[BunkerDepthBands];
            for (int i = 0; i < bands.Length; i++) bands[i] = i;

            for (int i = bands.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (bands[i], bands[j]) = (bands[j], bands[i]);
            }

            return bands[cell - block * BunkerDepthBands];
        }

        float ClearOfGroynes(float x, float clear)
        {
            int g = Mathf.FloorToInt(x / GroyneCellSize);
            float best = x, bestShift = float.MaxValue;
            float left = GroyneX(g - 2, GroyneRng(g - 2));

            for (int cell = g - 1; cell <= g + 2; cell++)
            {
                float right = GroyneX(cell, GroyneRng(cell));
                float from = left + clear, to = right - clear;
                left = right;
                if (from > to) continue;

                float spot = Mathf.Clamp(x, from, to);
                float shift = Mathf.Abs(spot - x);
                if (shift >= bestShift) continue;

                bestShift = shift;
                best = spot;
            }

            return best;
        }

        void CacheShore(int chunkIndex)
        {
            if (_shoreCentre == null)
            {
                _shoreCentre = new float[Res];
                _shoreHalf = new float[Res];
            }

            for (int ix = 0; ix < Res; ix++)
            {
                float x = WorldX(chunkIndex, ix);
                _shoreCentre[ix] = ShoreCentre(x);
                _shoreHalf[ix] = ShoreHalf(x);
            }
        }

        void FillRows(float[,] heights, int chunkIndex, int izFrom, int izTo)
        {
            for (int iz = izFrom; iz < izTo; iz++)
            {
                float z = Depth * iz / (Res - 1);
                for (int ix = 0; ix < Res; ix++)
                    heights[iz, ix] = HeightFrom(WorldX(chunkIndex, ix), z,
                        _shoreCentre[ix], _shoreHalf[ix]);
            }
        }

        float ShoreCentre(float x) =>
            ShoreZ + (Mathf.PerlinNoise(x / 900f + _d1, 0.5f) - 0.5f) * 2f * ShoreZJitter;

        float ShoreHalf(float x) =>
            ShoreWidth + (Mathf.PerlinNoise(x / 520f + _d2, 0.5f) - 0.5f) * 2f * ShoreWidthJitter;

        float BaseHeight(float x, float z) =>
            HeightFrom(x, z, ShoreCentre(x), ShoreHalf(x));

        float HeightFrom(float x, float z, float centre, float half)
        {
            float plain = SeaLevel + PlainRise
                + (Mathf.PerlinNoise(x / 620f + _p1, z / 620f + _p2) - 0.5f) * 2f * PlainBroad
                + (Mathf.PerlinNoise(x / 150f + _p3, z / 150f + _p4) - 0.5f) * 2f * PlainFine;

            float seabed = SeabedY
                + (Mathf.PerlinNoise(x / 400f + _p5, z / 400f + _p6) - 0.5f) * 2f * SeabedRelief;

            float toShelf = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(centre - half, centre + half, z));
            float toDeep = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(centre + ShelfRun, centre + DeepRun, z));

            float h = Mathf.Lerp(plain, SeaLevel + ShelfSink, toShelf);
            h = Mathf.Lerp(h, seabed, toDeep);

            return h + Micro(x, z) * Mathf.Lerp(1f, MicroOnBeach, toShelf) * (1f - toDeep);
        }

        float Micro(float x, float z) =>
            (Mathf.PerlinNoise(x / MicroWave + _m1, z / MicroWave + _m2) - 0.5f) * 2f * PlainMicro
            + (Mathf.PerlinNoise(x / GritWave + _m3, z / GritWave + _m4) - 0.5f) * 2f * PlainGrit;

        static void StampCrater(float[,] heights, int chunkIndex, CraterSpec c)
        {
            float chunkX0 = chunkIndex * ChunkLength;
            if (c.x + c.influence < chunkX0 || c.x - c.influence > chunkX0 + ChunkLength) return;

            int ixMin = Mathf.Max(0, Mathf.FloorToInt((c.x - c.influence - chunkX0) / XStep));
            int ixMax = Mathf.Min(Res - 1, Mathf.CeilToInt((c.x + c.influence - chunkX0) / XStep));
            int izMin = Mathf.Max(0, Mathf.FloorToInt((c.z - c.influence) / ZStep));
            int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((c.z + c.influence) / ZStep));

            for (int iz = izMin; iz <= izMax; iz++)
            {
                float dz = Depth * iz / (Res - 1) - c.z;

                for (int ix = ixMin; ix <= ixMax; ix++)
                {
                    float dx = WorldX(chunkIndex, ix) - c.x;
                    float r = Mathf.Sqrt(dx * dx + dz * dz);
                    if (r > c.influence) continue;
                    heights[iz, ix] += ProceduralTerrain.CraterDelta(r, c.radius, c.depth, c.rim, c.rimSigma);
                }
            }
        }

        static void StampBunkerPad(float[,] heights, int chunkIndex, BunkerSpec b)
        {
            float chunkX0 = chunkIndex * ChunkLength;
            if (b.x + b.reach < chunkX0 || b.x - b.reach > chunkX0 + ChunkLength) return;

            int ixMin = Mathf.Max(0, Mathf.FloorToInt((b.x - b.reach - chunkX0) / XStep));
            int ixMax = Mathf.Min(Res - 1, Mathf.CeilToInt((b.x + b.reach - chunkX0) / XStep));
            int izMin = Mathf.Max(0, Mathf.FloorToInt((b.z - b.reach) / ZStep));
            int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((b.z + b.reach) / ZStep));
            Quaternion back = Quaternion.Inverse(b.rotation);

            for (int iz = izMin; iz <= izMax; iz++)
            {
                float dz = Depth * iz / (Res - 1) - b.z;

                for (int ix = ixMin; ix <= ixMax; ix++)
                {
                    Vector3 local = back * new Vector3(WorldX(chunkIndex, ix) - b.x, 0f, dz);
                    float outX = Mathf.Max(0f, Mathf.Abs(local.x) - b.halfX);
                    float outZ = Mathf.Max(0f, Mathf.Abs(local.z) - b.halfZ);
                    float weight = Mathf.SmoothStep(1f, 0f,
                        Mathf.Sqrt(outX * outX + outZ * outZ) / BunkerPadFalloff);
                    if (weight <= 0f) continue;
                    heights[iz, ix] = Mathf.Lerp(heights[iz, ix], b.padY, weight);
                }
            }
        }

        void StampDyke(float[,] heights, int chunkIndex, DykeSpec d)
        {
            float chunkX0 = chunkIndex * ChunkLength;
            int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt(DykeFadeTo / ZStep));

            for (int iz = 0; iz <= izMax; iz++)
            {
                float z = Depth * iz / (Res - 1);
                float fade = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(DykeFadeTo, DykeFadeFrom, z));
                if (fade <= 0f) continue;

                float cx = d.x0 + d.lean * z;
                float reach = d.halfWidth * 2.4f;
                int ixMin = Mathf.Max(0, Mathf.FloorToInt((cx - reach - chunkX0) / XStep));
                int ixMax = Mathf.Min(Res - 1, Mathf.CeilToInt((cx + reach - chunkX0) / XStep));

                for (int ix = ixMin; ix <= ixMax; ix++)
                {
                    float x = WorldX(chunkIndex, ix);
                    float dx = (x - cx) / d.halfWidth;
                    float crest = BaseHeight(x, z) + d.rise;
                    heights[iz, ix] = Mathf.Lerp(heights[iz, ix], crest,
                        Mathf.Exp(-dx * dx) * fade);
                }
            }
        }

        void CratersForRange(float xMin, float xMax, List<CraterSpec> list)
        {
            list.Clear();
            int c0 = Mathf.FloorToInt(xMin / CraterCellSize);
            int c1 = Mathf.FloorToInt(xMax / CraterCellSize);

            for (int cell = c0; cell <= c1; cell++)
            {
                var rng = new System.Random(Hash(_seed, cell, 5));
                int count = CountForDensity(rng, CratersPerCell);

                for (int i = 0; i < count; i++)
                {
                    float radius = Range(rng, 10f, 26f);
                    float depth = radius * Range(rng, 0.30f, 0.45f);
                    var crater = new CraterSpec
                    {
                        x = (cell + (float)rng.NextDouble()) * CraterCellSize,
                        z = Range(rng, CraterZMin, CraterZMax),
                        radius = radius,
                        depth = depth,
                        rim = depth * 0.28f,
                        rimSigma = radius * 0.40f,
                        influence = radius * 1.7f,
                    };

                    if (!Occupied(crater.x, crater.z, crater.influence + BunkerPadFalloff))
                        list.Add(crater);
                }
            }
        }

        void DykesForRange(float xMin, float xMax, List<DykeSpec> list)
        {
            list.Clear();
            int c0 = Mathf.FloorToInt(xMin / DykeCellSize);
            int c1 = Mathf.FloorToInt(xMax / DykeCellSize);

            for (int cell = c0; cell <= c1; cell++)
            {
                var rng = new System.Random(Hash(_seed, cell, 11));
                if (rng.NextDouble() > DykeChance) continue;

                list.Add(new DykeSpec
                {
                    x0 = (cell + (float)rng.NextDouble()) * DykeCellSize,
                    lean = Range(rng, -0.32f, 0.32f),
                    halfWidth = Range(rng, 10f, 18f),
                    rise = Range(rng, 5f, 9f),
                });
            }
        }
    }
}
