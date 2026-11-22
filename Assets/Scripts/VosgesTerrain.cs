using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class VosgesTerrain : CampaignTerrain
    {
        public const float ValleyZMax = 520f;
        public const float LandDepth = 1520f;
        const float NearGrassFrom = 260f, NearGrassTo = 340f;
        const float NearGrassDistance = 1100f;

        const int RowsPerStep = 16;

        const float RollBroad = 7f, RollMid = 4f, RollFine = 2f;
        const float RollPatch = 6f, RollGrain = 1.2f;
        const float BackRise = 30f, BackRiseFrom = 400f;

        const float U = PlaneModelConfig.UnitsPerMeter;
        const float HillHeight = 12f * U, BumpHeight = 2.4f * U;
        const float HillRampTo = 500f;
        const float HillFadeFrom = 1150f, HillFadeTo = 1460f;
        const float HillWaveX = 900f, HillWaveZ = 650f, HillDetail = 0.45f, HillDetailWeight = 0.25f;
        const float HillLow = 0.25f, HillHigh = 0.85f;
        const float TerrainTop = 300f;

        const float CraterCell = 128f;
        const float ShellsPerCell = 0.6f, MinesPerCell = 0.13f;
        const float CraterZMin = 20f, CraterZMax = 620f;
        const float MaxCraterReach = 150f;

        const int AlphaRes = 256;
        const int GrassDetailRes = 512;
        const float GrassScarLimit = 0.35f;
        const float RegrowWeight = 0.1f;
        const int RegrowSalt = 11;

        const float MeadowTile = ChunkLength / 6f;
        const float EarthTile = ChunkLength / 8f;

        static readonly Color MeadowColor = new Color(0.34f, 0.50f, 0.24f);
        static readonly Color EarthColor = new Color(0.40f, 0.32f, 0.23f);
        static readonly Color WallColor = new Color(0.36f, 0.30f, 0.22f);
        static readonly Color GrassHealthy = new Color(0.46f, 0.72f, 0.28f);
        static readonly Color GrassDry = new Color(0.56f, 0.74f, 0.32f);

        float _r1, _r2, _r3, _ox1, _oz1, _ox2, _oz2, _hx1, _hz1, _hx2, _hz2, _patchOffset;

        TerrainLayer _meadowLayer, _earthLayer;
        Material _wallMat;

        struct CraterSpec
        {
            public float x, z, radius, depth, rim, rimSigma, influence, bareRadius;
        }

        readonly List<CraterSpec> _craters = new List<CraterSpec>();
        readonly List<CraterSpec> _craterScratch = new List<CraterSpec>();
        readonly List<VosgesLayout.Pad> _pads = new List<VosgesLayout.Pad>();
        readonly List<VosgesLayout.Pad> _mounds = new List<VosgesLayout.Pad>();
        readonly Dictionary<int, int> _stamped = new Dictionary<int, int>();
        readonly Dictionary<int, List<VosgesLayout.Pad>> _regrow = new Dictionary<int, List<VosgesLayout.Pad>>();

        Mesh _farGrass;
        int _farGrassIndex = int.MinValue;

        public VosgesLayout Layout { get; private set; }

        public static bool SkipCraters;

        bool Arena => _arena != null;

        protected override float DetailDistance => NearGrassDistance;

        protected override float HeightScale => TerrainTop;
        protected override float TerrainY => 0f;
        protected override float MinHeight => ProceduralTerrain.MinHeight;
        protected override float MaxHeight => TerrainTop;

        protected override void Prepare(Daytime daytime, Weather weather,
            float cameraDistance, float playPlaneZ)
        {
            var rng = new System.Random(_seed);
            _r1 = Offset(rng); _r2 = Offset(rng); _r3 = Offset(rng);
            _ox1 = Offset(rng); _oz1 = Offset(rng);
            _ox2 = Offset(rng); _oz2 = Offset(rng);
            _hx1 = Offset(rng); _hz1 = Offset(rng);
            _hx2 = Offset(rng); _hz2 = Offset(rng);
            _patchOffset = Offset(rng);

            _meadowLayer = TerrainSurfaces.GrainLayer("Vosges (meadow)", MeadowColor, MeadowTile, _seed);
            _earthLayer = TerrainSurfaces.FlatLayer("Vosges (earth)", EarthColor, EarthTile);
            _wallMat = TerrainSurfaces.FlatMaterial(WallColor);

            Layout = Arena ? null : new VosgesLayout(_seed, ValleyHeight, BaseHeight, cameraDistance, playPlaneZ);
            if (Layout != null) Layout.GunHills = GunHills;

            VosgesSky.ApplyFog(daytime, cameraDistance, playPlaneZ);
        }

        public override bool Occupied(float worldX, float z, float margin) =>
            Arena ? _arena.Occupied(worldX, z, margin) : Layout != null && Layout.Occupied(worldX, z, margin);

        bool Bare(float x, float z) => Arena ? _arena.Covered(x, z) : Layout.Bare(x, z);

        public override bool InCrater(float worldX, float z)
        {
            CratersForRange(worldX - MaxCraterReach, worldX + MaxCraterReach, _craterScratch);

            float zEff = Mathf.Max(z, ProceduralTerrain.FrontStrip);
            foreach (var c in _craterScratch)
            {
                float dx = c.x - worldX, dz = zEff - c.z;
                if (dx * dx + dz * dz < c.bareRadius * c.bareRadius) return true;
            }
            return false;
        }

        protected override IEnumerator FillHeights(float[,] metres, int index)
        {
            float x0 = index * ChunkLength;
            CratersForRange(x0 - MaxCraterReach, x0 + ChunkLength + MaxCraterReach, _craters);
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

            if (Arena) yield break;

            Layout.PadsForRange(x0 - VosgesLayout.MaxPadReach, x0 + ChunkLength + VosgesLayout.MaxPadReach, _pads);
            foreach (var pad in _pads)
            {
                StampPad(metres, index, pad);
                yield return null;
            }

            int stamped = _mounds.Count;
            for (int i = 0; i < stamped; i++) StampPad(metres, index, _mounds[i]);
            _stamped[index] = stamped;
        }

        public void Raise(VosgesLayout.Pad mound)
        {
            _mounds.Add(mound);

            int first = Mathf.FloorToInt((mound.x - mound.Reach) / ChunkLength);
            int last = Mathf.FloorToInt((mound.x + mound.Reach) / ChunkLength);
            for (int index = first; index <= last; index++)
                if (ChunkData(index, out TerrainData data)) CatchUp(index, data);
        }

        protected override void ChunkReady(int index, TerrainData data) => CatchUp(index, data);

        void CatchUp(int index, TerrainData data)
        {
            int from = _stamped.TryGetValue(index, out int done) ? done : 0;
            for (int i = from; i < _mounds.Count; i++)
                if (Restamp(data, index, _mounds[i])) Regrow(index, data, _mounds[i]);
            _stamped[index] = _mounds.Count;
        }

        void Regrow(int index, TerrainData data, VosgesLayout.Pad pad)
        {
            if (_regrow.TryGetValue(index, out var pads))
            {
                pads.Add(pad);
                return;
            }
            _regrow[index] = new List<VosgesLayout.Pad> { pad };
            StartCoroutine(Regrow(index, data));
        }

        IEnumerator Regrow(int index, TerrainData data)
        {
            yield return null;
            if (!_regrow.TryGetValue(index, out var pads)) yield break;
            _regrow.Remove(index);
            if (!ChunkLive(index, data)) yield break;

            float x0 = index * ChunkLength;
            float xFrom = float.MaxValue, xTo = float.MinValue, zFrom = float.MaxValue, zTo = float.MinValue;
            foreach (var pad in pads)
            {
                float reach = pad.Reach;
                xFrom = Mathf.Min(xFrom, pad.x - reach - x0);
                xTo = Mathf.Max(xTo, pad.x + reach - x0);
                zFrom = Mathf.Min(zFrom, pad.z - reach);
                zTo = Mathf.Max(zTo, pad.z + reach);
            }

            if (zFrom < NearGrassTo)
            {
                foreach (var step in PlantGrass(data, index))
                {
                    yield return step;
                    if (!ChunkLive(index, data)) yield break;
                }
            }

            Mesh grass = null;
            foreach (var step in VosgesFarGrass.Build(data, x0, ChunkLength, Depth, xFrom, xTo, zFrom, zTo,
                         (x, z) => Unraised(pads, x, z) || FarGrassSkip(x, z), Hash(_seed, index, RegrowSalt),
                         GraphicsOptions.FarGrassDensity, _patchOffset, mesh => grass = mesh, OnMound))
            {
                yield return step;
                if (!ChunkLive(index, data)) yield break;
            }

            if (!AttachMesh(index, data, "Mound Grass", grass, VosgesFarGrass.SharedMaterial(GrassHealthy, GrassDry),
                    new Vector3(x0, TerrainY, 0f)) && grass != null)
                Destroy(grass);
        }

        bool OnMound(float x, float z)
        {
            foreach (var pad in _mounds)
                if (pad.Weight(x, z) > 0f) return true;
            return false;
        }

        static bool Unraised(List<VosgesLayout.Pad> pads, float x, float z)
        {
            foreach (var pad in pads)
                if (pad.Weight(x, z) >= RegrowWeight) return false;
            return true;
        }

        bool Restamp(TerrainData data, int index, VosgesLayout.Pad pad)
        {
            float chunkX0 = index * ChunkLength;
            float reach = pad.Reach;
            if (pad.x + reach < chunkX0 || pad.x - reach > chunkX0 + ChunkLength) return false;

            int ixMin = Mathf.Max(0, Mathf.FloorToInt((pad.x - reach - chunkX0) / XStep));
            int ixMax = Mathf.Min(Res - 1, Mathf.CeilToInt((pad.x + reach - chunkX0) / XStep));
            int izMin = Mathf.Max(1, Mathf.FloorToInt((pad.z - reach) / ZStep));
            int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((pad.z + reach) / ZStep));
            if (ixMin > ixMax || izMin > izMax) return false;

            float[,] patch = data.GetHeights(ixMin, izMin, ixMax - ixMin + 1, izMax - izMin + 1);
            float scale = HeightScale, baseY = TerrainY;
            for (int iz = izMin; iz <= izMax; iz++)
            {
                float z = Depth * iz / (Res - 1);
                for (int ix = ixMin; ix <= ixMax; ix++)
                {
                    float w = pad.Weight(WorldX(index, ix), z);
                    if (w <= 0f) continue;

                    float metres = patch[iz - izMin, ix - ixMin] * scale + baseY;
                    metres = Mathf.Clamp(Mathf.Lerp(metres, pad.y, w), MinHeight, MaxHeight);
                    patch[iz - izMin, ix - ixMin] = (metres - baseY) / scale;
                }
            }
            data.SetHeights(ixMin, izMin, patch);
            return true;
        }

        protected override void PaintChunk(TerrainData data, int index)
        {
            data.terrainLayers = new[] { _meadowLayer, _earthLayer };
            data.alphamapResolution = AlphaRes;

            float x0 = index * ChunkLength;
            var alpha = new float[AlphaRes, AlphaRes, 2];

            for (int iz = 0; iz < AlphaRes; iz++)
            {
                float z = Depth * iz / (AlphaRes - 1);

                for (int ix = 0; ix < AlphaRes; ix++)
                {
                    float x = x0 + ChunkLength * ix / (AlphaRes - 1);
                    float earth = Scar(x, z);

                    alpha[iz, ix, 0] = 1f - earth;
                    alpha[iz, ix, 1] = earth;
                }
            }

            data.SetAlphamaps(0, 0, alpha);
            VosgesFarGrass.SetupNear(data, GrassDetailRes, GrassHealthy, GrassDry);
        }

        protected override IEnumerable<object> Decorate(TerrainData data, int index)
        {
            foreach (var step in PlantGrass(data, index))
                yield return step;

            _farGrass = null;
            _farGrassIndex = index;
            float x0 = index * ChunkLength;
            foreach (var step in VosgesFarGrass.Build(data, x0, ChunkLength, Depth, FarGrassSkip,
                         Hash(_seed, index, 5), GraphicsOptions.FarGrassDensity, _patchOffset, mesh => _farGrass = mesh,
                         OnMound))
                yield return step;
        }

        bool FarGrassSkip(float x, float z) => Scar(x, z) > GrassScarLimit || Bare(x, z);

        protected override void AddChunkMeshes(int index, Transform root, float[] cutLine,
            List<Mesh> owned)
        {
            AddMesh(root, "Cut Wall", ProceduralTerrain.BuildCutWallMesh(cutLine, ChunkLength),
                _wallMat, new Vector3(index * ChunkLength + ChunkLength / 2f, 0f, 0f), owned);

            if (_farGrass != null && _farGrassIndex == index)
                AddMesh(root, "Far Grass", _farGrass, VosgesFarGrass.SharedMaterial(GrassHealthy, GrassDry),
                    new Vector3(index * ChunkLength, TerrainY, 0f), owned);
            _farGrass = null;
        }

        void FillRows(float[,] heights, int chunkIndex, int izFrom, int izTo)
        {
            for (int iz = izFrom; iz < izTo; iz++)
            {
                float z = Depth * iz / (Res - 1);

                for (int ix = 0; ix < Res; ix++)
                    heights[iz, ix] = BaseHeight(WorldX(chunkIndex, ix), z);
            }
        }

        float BaseHeight(float x, float z)
        {
            float mask = Arena ? _arena.HillMask(x, z) : Layout != null ? Layout.HillMask(x, z) : 1f;
            float h = ValleyHeight(x, z);
            if (mask > 0f) h += Hills(x, z) * mask;
            return Mathf.Clamp(h, MinHeight, MaxHeight);
        }

        float Hills(float x, float z)
        {
            float zEff = Mathf.Max(z, ProceduralTerrain.FrontStrip);
            float amp = Mathf.Lerp(BumpHeight, HillHeight, Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(ProceduralTerrain.FrontStrip, HillRampTo, zEff)));
            amp *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HillFadeFrom, HillFadeTo, z));
            if (amp <= 0f) return 0f;

            float n = Mathf.PerlinNoise(x / HillWaveX + _hx1, zEff / HillWaveZ + _hz1) * (1f - HillDetailWeight)
                      + Mathf.PerlinNoise(x / (HillWaveX * HillDetail) + _hx2, zEff / (HillWaveZ * HillDetail) + _hz2)
                      * HillDetailWeight;
            return amp * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HillLow, HillHigh, n));
        }

        float ValleyHeight(float x, float z)
        {
            if (Arena) return ProceduralTerrain.BaseLevel;

            float zEff = Mathf.Max(z, ProceduralTerrain.FrontStrip);
            float depthRamp = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(ProceduralTerrain.FrontStrip, 240f, zEff));
            float back = BackRise * Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(BackRiseFrom, Depth, z));

            float h = ProceduralTerrain.BaseLevel + back;
            h += (Mathf.PerlinNoise(x / 1100f + _r1, 0.5f) - 0.5f) * 2f * RollBroad;
            h += (Mathf.PerlinNoise(x / 470f + _r2, 0.5f) - 0.5f) * 2f * RollMid;
            h += (Mathf.PerlinNoise(x / 190f + _r3, 0.5f) - 0.5f) * 2f * RollFine;
            h += (Mathf.PerlinNoise(x / 210f + _ox1, zEff / 210f + _oz1) - 0.5f)
                 * 2f * RollPatch * depthRamp;
            h += (Mathf.PerlinNoise(x / 34f + _ox2, zEff / 34f + _oz2) - 0.5f) * 2f * RollGrain;
            return Mathf.Clamp(h, MinHeight, MaxHeight);
        }

        static void StampPad(float[,] heights, int chunkIndex, VosgesLayout.Pad pad)
        {
            float chunkX0 = chunkIndex * ChunkLength;
            float reach = pad.Reach;
            if (pad.x + reach < chunkX0 || pad.x - reach > chunkX0 + ChunkLength) return;

            int ixMin = Mathf.Max(0, Mathf.FloorToInt((pad.x - reach - chunkX0) / XStep));
            int ixMax = Mathf.Min(Res - 1, Mathf.CeilToInt((pad.x + reach - chunkX0) / XStep));
            int izMin = Mathf.Max(0, Mathf.FloorToInt((pad.z - reach) / ZStep));
            int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((pad.z + reach) / ZStep));

            for (int iz = izMin; iz <= izMax; iz++)
            {
                float z = Depth * iz / (Res - 1);
                for (int ix = ixMin; ix <= ixMax; ix++)
                {
                    float w = pad.Weight(WorldX(chunkIndex, ix), z);
                    if (w > 0f) heights[iz, ix] = Mathf.Lerp(heights[iz, ix], pad.y, w);
                }
            }
        }

        static void StampCrater(float[,] heights, int chunkIndex, CraterSpec c)
        {
            float chunkX0 = chunkIndex * ChunkLength;
            if (c.x + c.influence < chunkX0 || c.x - c.influence > chunkX0 + ChunkLength) return;

            int ixMin = Mathf.Max(0, Mathf.FloorToInt((c.x - c.influence - chunkX0) / XStep));
            int ixMax = Mathf.Min(Res - 1, Mathf.CeilToInt((c.x + c.influence - chunkX0) / XStep));
            int izMin = c.z - c.influence < ProceduralTerrain.FrontStrip
                ? 0 : Mathf.FloorToInt((c.z - c.influence) / ZStep);
            int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((c.z + c.influence) / ZStep));

            for (int iz = izMin; iz <= izMax; iz++)
            {
                float dz = Mathf.Max(Depth * iz / (Res - 1), ProceduralTerrain.FrontStrip) - c.z;

                for (int ix = ixMin; ix <= ixMax; ix++)
                {
                    float dx = WorldX(chunkIndex, ix) - c.x;
                    float r = Mathf.Sqrt(dx * dx + dz * dz);
                    if (r > c.influence) continue;
                    heights[iz, ix] += ProceduralTerrain.CraterDelta(r, c.radius, c.depth, c.rim, c.rimSigma);
                }
            }
        }

        void CratersForRange(float xMin, float xMax, List<CraterSpec> list)
        {
            list.Clear();
            if (Arena || SkipCraters) return;
            int c0 = Mathf.FloorToInt(xMin / CraterCell);
            int c1 = Mathf.FloorToInt(xMax / CraterCell);

            for (int cell = c0; cell <= c1; cell++)
            {
                var shells = new System.Random(Hash(_seed, cell, 1));
                int shellCount = CountForDensity(shells, ShellsPerCell);
                for (int i = 0; i < shellCount; i++)
                {
                    float radius = Range(shells, 10f, 34f);
                    list.Add(NewCrater(shells, cell, radius,
                        radius * Range(shells, 0.22f, 0.32f), 0.35f, 0.35f, 1.8f));
                }

                var mines = new System.Random(Hash(_seed, cell, 2));
                int mineCount = CountForDensity(mines, MinesPerCell);
                for (int i = 0; i < mineCount; i++)
                {
                    float radius = Range(mines, 38f, 72f);
                    float u = (float)mines.NextDouble();
                    float depth = radius * Mathf.Lerp(ProceduralTerrain.MineDepthShallow,
                        ProceduralTerrain.MineDepthDeep, 1f - u * u);
                    list.Add(NewCrater(mines, cell, radius, depth, 0.45f, 0.3f, 1.7f));
                }
            }

            for (int i = list.Count - 1; i >= 0; i--)
                if (Layout.Occupied(list[i].x, list[i].z, list[i].influence)) list.RemoveAt(i);
        }

        static CraterSpec NewCrater(System.Random rng, int cell, float radius, float depth,
            float rimFactor, float sigmaFactor, float influenceFactor)
        {
            return new CraterSpec
            {
                x = (cell + (float)rng.NextDouble()) * CraterCell,
                z = Range(rng, CraterZMin, CraterZMax),
                radius = radius,
                depth = depth,
                rim = depth * rimFactor,
                rimSigma = radius * sigmaFactor,
                influence = radius * influenceFactor,
                bareRadius = radius * ProceduralTerrain.CraterBareRadii,
            };
        }

        float Scar(float x, float z)
        {
            float scar = 0f;
            float zEff = Mathf.Max(z, ProceduralTerrain.FrontStrip);
            foreach (var c in _craters)
            {
                float dx = x - c.x, dz = zEff - c.z;
                float rr = dx * dx + dz * dz;
                if (rr > c.bareRadius * c.bareRadius) continue;

                scar = Mathf.Max(scar, 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(c.radius, c.bareRadius, Mathf.Sqrt(rr))));
            }

            return Mathf.Clamp01(scar);
        }

        IEnumerable<object> PlantGrass(TerrainData data, int index)
        {
            int cols = Mathf.Max(1, Mathf.RoundToInt(ChunkLength / ProceduralTerrain.GrassSpacing));
            int rows = Mathf.Max(1, Mathf.RoundToInt(Depth / ProceduralTerrain.GrassSpacing));
            float cellX = ChunkLength / cols;
            float cellZ = Depth / rows;
            int grassRows = Mathf.Min(rows, Mathf.CeilToInt(NearGrassTo / cellZ));
            float x0 = index * ChunkLength;

            var rng = new System.Random(Hash(_seed, index, 3));
            var layers = new int[VosgesFarGrass.Kinds][,];
            for (int k = 0; k < layers.Length; k++) layers[k] = new int[GrassDetailRes, GrassDetailRes];

            for (int row = 0; row < grassRows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    float lx = Mathf.Min((col + (float)rng.NextDouble()) * cellX, ChunkLength);
                    float lz = Mathf.Min((row + (float)rng.NextDouble()) * cellZ, Depth);
                    float xNorm = lx / ChunkLength, zNorm = lz / Depth;
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(NearGrassFrom, NearGrassTo, lz));
                    if (fade > 0f && rng.NextDouble() < fade) continue;

                    if (data.GetSteepness(xNorm, zNorm) > ProceduralTerrain.GrassMaxSlopeDeg
                        && !OnMound(x0 + lx, lz)) continue;
                    if (Scar(x0 + lx, lz) > GrassScarLimit) continue;
                    if (Bare(x0 + lx, lz)) continue;

                    int ix = Mathf.Min(GrassDetailRes - 1, (int)(xNorm * GrassDetailRes));
                    int iz = Mathf.Min(GrassDetailRes - 1, (int)(zNorm * GrassDetailRes));
                    int kind = VosgesFarGrass.Kind(VosgesFarGrass.Dryness(x0 + lx, lz, _patchOffset), rng);
                    layers[kind][iz, ix]++;
                }
                if (row % 40 == 39) yield return null;
            }

            for (int k = 0; k < layers.Length; k++) data.SetDetailLayer(0, 0, k, layers[k]);
        }
    }
}
