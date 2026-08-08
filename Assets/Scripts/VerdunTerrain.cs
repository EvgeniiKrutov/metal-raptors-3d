using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class VerdunTerrain : CampaignTerrain
    {
        const int GrassDetailRes = 512;
        const float CellSize = 128f;
        const float MaxCraterReach = 150f;
        const int RowsPerStep = 16;

        float _r1, _r2, _r3, _ox1, _oz1, _ox2, _oz2;

        TerrainLayer _landLayer;
        Material _wallMat;

        struct CraterSpec
        {
            public float x, z, radius, depth, rim, rimSigma, influence, bareRadius;
        }

        readonly List<CraterSpec> _craterScratch = new List<CraterSpec>();
        List<CraterSpec> _chunkCraters;

        protected override float HeightScale => ProceduralTerrain.HeightScale;
        protected override float TerrainY => 0f;
        protected override float MinHeight => ProceduralTerrain.MinHeight;
        protected override float MaxHeight => ProceduralTerrain.MaxHeight;

        protected override void Prepare(Daytime daytime, Weather weather,
            float cameraDistance, float playPlaneZ)
        {
            var rng = new System.Random(_seed);
            _r1 = Offset(rng);
            _r2 = Offset(rng);
            _r3 = Offset(rng);
            _ox1 = Offset(rng);
            _oz1 = Offset(rng);
            _ox2 = Offset(rng);
            _oz2 = Offset(rng);

            _landLayer = ProceduralTerrain.CreateLandLayer();
            _wallMat = ProceduralTerrain.CutWallMaterial();

            ProceduralTerrain.ApplyFog(daytime, cameraDistance, playPlaneZ);
        }

        public override bool InCrater(float worldX, float z)
        {
            CratersForRange(worldX - MaxCraterReach, worldX + MaxCraterReach, _craterScratch);
            return InCrater(worldX, z, _craterScratch);
        }

        protected override IEnumerator FillHeights(float[,] metres, int index)
        {
            float x0 = index * ChunkLength;
            _chunkCraters = CratersForRange(x0 - MaxCraterReach, x0 + ChunkLength + MaxCraterReach);

            for (int iz = 0; iz < Res; iz += RowsPerStep)
            {
                FillRows(metres, index, iz, Mathf.Min(Res, iz + RowsPerStep));
                yield return null;
            }

            foreach (var c in _chunkCraters)
            {
                StampCrater(metres, index, c);
                yield return null;
            }
        }

        protected override void PaintChunk(TerrainData data, int index)
        {
            ProceduralTerrain.PaintTerrain(data, _landLayer);
            ProceduralTerrain.SetupGrassDetail(data, GrassDetailRes);
        }

        protected override IEnumerable<object> Decorate(TerrainData data, int index)
        {
            foreach (var step in PlantGrass(data, index, _chunkCraters))
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
                    Mathf.InverseLerp(ProceduralTerrain.FrontStrip, 220f, zEff));

                for (int ix = 0; ix < Res; ix++)
                {
                    float x = WorldX(chunkIndex, ix);

                    float h = ProceduralTerrain.BaseLevel;
                    h += (Mathf.PerlinNoise(x / 950f + _r1, 0.5f) - 0.5f) * 2f * 10f;
                    h += (Mathf.PerlinNoise(x / 430f + _r2, 0.5f) - 0.5f) * 2f * 6f;
                    h += (Mathf.PerlinNoise(x / 175f + _r3, 0.5f) - 0.5f) * 2f * 3.5f;

                    h += (Mathf.PerlinNoise(x / 170f + _ox1, zEff / 170f + _oz1) - 0.5f) * 2f * 10f * depthRamp;
                    h += (Mathf.PerlinNoise(x / 30f + _ox2, zEff / 30f + _oz2) - 0.5f) * 2f * 1.6f;

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

        List<CraterSpec> CratersForRange(float xMin, float xMax)
        {
            var list = new List<CraterSpec>();
            CratersForRange(xMin, xMax, list);
            return list;
        }

        void CratersForRange(float xMin, float xMax, List<CraterSpec> list)
        {
            list.Clear();
            int c0 = Mathf.FloorToInt(xMin / CellSize);
            int c1 = Mathf.FloorToInt(xMax / CellSize);

            for (int cell = c0; cell <= c1; cell++)
            {
                var shellRng = new System.Random(Hash(_seed, cell, 1));
                int shells = CountForDensity(shellRng, ProceduralTerrain.CratersPerMetre * CellSize);
                for (int i = 0; i < shells; i++)
                {
                    float cx = (cell + (float)shellRng.NextDouble()) * CellSize;
                    float cz = Mathf.Lerp(10f, Depth - 40f, (float)shellRng.NextDouble());
                    float radius = Mathf.Lerp(12f, 42f, (float)shellRng.NextDouble());
                    float depth = radius * Mathf.Lerp(0.22f, 0.30f, (float)shellRng.NextDouble());
                    list.Add(new CraterSpec
                    {
                        x = cx, z = cz, radius = radius, depth = depth,
                        rim = depth * 0.35f, rimSigma = radius * 0.35f,
                        influence = radius * 1.8f,
                        bareRadius = radius * ProceduralTerrain.CraterBareRadii,
                    });
                }

                var mineRng = new System.Random(Hash(_seed, cell, 2));
                int mines = CountForDensity(mineRng, ProceduralTerrain.MinesPerMetre * CellSize);
                for (int i = 0; i < mines; i++)
                {
                    float cx = (cell + (float)mineRng.NextDouble()) * CellSize;
                    float cz = Mathf.Lerp(10f, Depth - 40f, (float)mineRng.NextDouble());
                    float radius = Mathf.Lerp(40f, 80f, (float)mineRng.NextDouble());
                    float u = (float)mineRng.NextDouble();
                    float depth = radius * Mathf.Lerp(ProceduralTerrain.MineDepthShallow,
                        ProceduralTerrain.MineDepthDeep, 1f - u * u);
                    list.Add(new CraterSpec
                    {
                        x = cx, z = cz, radius = radius, depth = depth,
                        rim = depth * 0.45f, rimSigma = radius * 0.3f,
                        influence = radius * 1.7f,
                        bareRadius = radius * ProceduralTerrain.CraterBareRadii,
                    });
                }
            }
        }

        IEnumerable<object> PlantGrass(TerrainData data, int index, List<CraterSpec> craters)
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

                    if (InCrater(x0 + lx, lz, craters)) continue;
                    float xNorm = lx / ChunkLength, zNorm = lz / Depth;
                    if (data.GetSteepness(xNorm, zNorm) > ProceduralTerrain.GrassMaxSlopeDeg) continue;

                    int ix = Mathf.Min(GrassDetailRes - 1, (int)(xNorm * GrassDetailRes));
                    int iz = Mathf.Min(GrassDetailRes - 1, (int)(zNorm * GrassDetailRes));
                    layer[iz, ix]++;
                }
                if (row % 40 == 39) yield return null;
            }

            data.SetDetailLayer(0, 0, 0, layer);
        }

        static bool InCrater(float worldX, float z, List<CraterSpec> craters)
        {
            float zEff = Mathf.Max(z, ProceduralTerrain.FrontStrip);
            foreach (var c in craters)
            {
                float dx = c.x - worldX;
                float dz = zEff - c.z;
                if (dx * dx + dz * dz < c.bareRadius * c.bareRadius) return true;
            }
            return false;
        }
    }
}
