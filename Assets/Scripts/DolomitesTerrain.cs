using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class DolomitesTerrain : CampaignTerrain
    {
        public const float ValleyZMax = 520f;

        const int RowsPerStep = 16;

        const float RollBroad = 7f, RollMid = 4f, RollFine = 2f;
        const float RollPatch = 6f, RollGrain = 1.2f;
        const float BackRise = 30f, BackRiseFrom = 400f;

        const float CraterCell = 128f;
        const float ShellsPerCell = 0.6f, MinesPerCell = 0.13f;
        const float CraterZMin = 20f, CraterZMax = 620f;
        const float MaxCraterReach = 150f;

        const int AlphaRes = 128;
        const int GrassDetailRes = 512;
        const float GrassScarLimit = 0.35f;

        const float MeadowTile = ChunkLength / 6f;
        const float EarthTile = ChunkLength / 8f;

        static readonly Color MeadowColor = new Color(0.34f, 0.50f, 0.24f);
        static readonly Color EarthColor = new Color(0.40f, 0.32f, 0.23f);
        static readonly Color WallColor = new Color(0.36f, 0.30f, 0.22f);
        static readonly Color GrassHealthy = new Color(0.36f, 0.60f, 0.26f);
        static readonly Color GrassDry = new Color(0.50f, 0.62f, 0.30f);

        float _r1, _r2, _r3, _ox1, _oz1, _ox2, _oz2;

        TerrainLayer _meadowLayer, _earthLayer;
        Material _wallMat;

        struct CraterSpec
        {
            public float x, z, radius, depth, rim, rimSigma, influence, bareRadius;
        }

        readonly List<CraterSpec> _craters = new List<CraterSpec>();
        readonly List<CraterSpec> _craterScratch = new List<CraterSpec>();

        protected override float HeightScale => ProceduralTerrain.HeightScale;
        protected override float TerrainY => 0f;
        protected override float MinHeight => ProceduralTerrain.MinHeight;
        protected override float MaxHeight => ProceduralTerrain.MaxHeight;

        protected override void Prepare(Daytime daytime, Weather weather,
            float cameraDistance, float playPlaneZ)
        {
            var rng = new System.Random(_seed);
            _r1 = Offset(rng); _r2 = Offset(rng); _r3 = Offset(rng);
            _ox1 = Offset(rng); _oz1 = Offset(rng);
            _ox2 = Offset(rng); _oz2 = Offset(rng);

            _meadowLayer = TerrainSurfaces.GrainLayer("Dolomites (meadow)", MeadowColor, MeadowTile, _seed);
            _earthLayer = TerrainSurfaces.FlatLayer("Dolomites (earth)", EarthColor, EarthTile);
            _wallMat = TerrainSurfaces.FlatMaterial(WallColor);

            DolomitesSky.ApplyFog(daytime, cameraDistance, playPlaneZ);
        }

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
            ProceduralTerrain.SetupGrassDetail(data, GrassDetailRes, GrassHealthy, GrassDry);
        }

        protected override IEnumerable<object> Decorate(TerrainData data, int index)
        {
            foreach (var step in PlantGrass(data, index))
                yield return step;
        }

        protected override void AddChunkMeshes(int index, Transform root, float[] cutLine,
            List<Mesh> owned)
        {
            AddMesh(root, "Cut Wall", ProceduralTerrain.BuildCutWallMesh(cutLine, ChunkLength),
                _wallMat, new Vector3(index * ChunkLength + ChunkLength / 2f, 0f, 0f), owned);
        }

        void FillRows(float[,] heights, int chunkIndex, int izFrom, int izTo)
        {
            for (int iz = izFrom; iz < izTo; iz++)
            {
                float z = Depth * iz / (Res - 1);
                float zEff = Mathf.Max(z, ProceduralTerrain.FrontStrip);
                float depthRamp = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(ProceduralTerrain.FrontStrip, 240f, zEff));
                float back = BackRise * Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(BackRiseFrom, Depth, z));

                for (int ix = 0; ix < Res; ix++)
                {
                    float x = WorldX(chunkIndex, ix);

                    float h = ProceduralTerrain.BaseLevel + back;
                    h += (Mathf.PerlinNoise(x / 1100f + _r1, 0.5f) - 0.5f) * 2f * RollBroad;
                    h += (Mathf.PerlinNoise(x / 470f + _r2, 0.5f) - 0.5f) * 2f * RollMid;
                    h += (Mathf.PerlinNoise(x / 190f + _r3, 0.5f) - 0.5f) * 2f * RollFine;
                    h += (Mathf.PerlinNoise(x / 210f + _ox1, zEff / 210f + _oz1) - 0.5f)
                         * 2f * RollPatch * depthRamp;
                    h += (Mathf.PerlinNoise(x / 34f + _ox2, zEff / 34f + _oz2) - 0.5f) * 2f * RollGrain;

                    heights[iz, ix] = h;
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
            float x0 = index * ChunkLength;

            var rng = new System.Random(Hash(_seed, index, 3));
            var layer = new int[GrassDetailRes, GrassDetailRes];

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    float lx = Mathf.Min((col + (float)rng.NextDouble()) * cellX, ChunkLength);
                    float lz = Mathf.Min((row + (float)rng.NextDouble()) * cellZ, Depth);
                    float xNorm = lx / ChunkLength, zNorm = lz / Depth;

                    if (data.GetSteepness(xNorm, zNorm) > ProceduralTerrain.GrassMaxSlopeDeg) continue;
                    if (Scar(x0 + lx, lz) > GrassScarLimit) continue;

                    int ix = Mathf.Min(GrassDetailRes - 1, (int)(xNorm * GrassDetailRes));
                    int iz = Mathf.Min(GrassDetailRes - 1, (int)(zNorm * GrassDetailRes));
                    layer[iz, ix]++;
                }
                if (row % 40 == 39) yield return null;
            }

            data.SetDetailLayer(0, 0, 0, layer);
        }
    }
}
