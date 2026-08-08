using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class FlandersTerrain : CampaignTerrain
    {
        public const float SeaLevel = SeaSurface.Level;

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

        const int AlphaRes = 128;
        const int RowsPerStep = 16;

        const float SandInland = 250f, SandSeaward = 10f;

        const int GrainRes = 128;
        const int BlotchCells = 6, GritCells = 12;
        const float BlotchDepth = 0.10f, GritDepth = 0.045f;
        const float GroundTile = ChunkLength / 6f, SandTile = ChunkLength / 8f;
        const float Roughness = 0f;

        static readonly Color GroundColor = new Color(0.46f, 0.42f, 0.33f);
        static readonly Color WallColor = new Color(0.38f, 0.35f, 0.28f);
        static readonly Color SandColor = new Color(0.74f, 0.70f, 0.59f);

        float _p1, _p2, _p3, _p4, _p5, _p6, _d1, _d2, _m1, _m2, _m3, _m4;

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

        readonly List<CraterSpec> _craters = new List<CraterSpec>();
        readonly List<DykeSpec> _dykes = new List<DykeSpec>();
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

            _groundLayer = GrainLayer(GroundColor, GroundTile, _seed);
            _sandLayer = FlatLayer(SandColor, SandTile);

            _wallMat = FlatMaterial(WallColor);

            CoastSky.ApplyFog(daytime, cameraDistance, playPlaneZ);
        }

        protected override IEnumerator FillHeights(float[,] metres, int index)
        {
            float x0 = index * ChunkLength;
            CratersForRange(x0 - MaxCraterReach, x0 + ChunkLength + MaxCraterReach, _craters);
            DykesForRange(x0 - MaxDykeReach, x0 + ChunkLength + MaxDykeReach, _dykes);
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
                    list.Add(new CraterSpec
                    {
                        x = (cell + (float)rng.NextDouble()) * CraterCellSize,
                        z = Range(rng, CraterZMin, CraterZMax),
                        radius = radius,
                        depth = depth,
                        rim = depth * 0.28f,
                        rimSigma = radius * 0.40f,
                        influence = radius * 1.7f,
                    });
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

        static TerrainLayer GrainLayer(Color color, float tile, int seed)
        {
            var tex = new Texture2D(GrainRes, GrainRes, TextureFormat.RGBA32, true)
            {
                name = "Coast (grain)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };

            float[,] blotch = TileNoise(GrainRes, BlotchCells, seed);
            float[,] grit = TileNoise(GrainRes, GritCells, seed + 31);
            var pixels = new Color32[GrainRes * GrainRes];

            for (int j = 0; j < GrainRes; j++)
            {
                for (int i = 0; i < GrainRes; i++)
                {
                    float shade = 1f
                        + (blotch[j, i] - 0.5f) * 2f * BlotchDepth
                        + (grit[j, i] - 0.5f) * 2f * GritDepth;

                    pixels[j * GrainRes + i] = new Color32(
                        (byte)(Mathf.Clamp01(color.r * shade) * 255f),
                        (byte)(Mathf.Clamp01(color.g * shade) * 255f),
                        (byte)(Mathf.Clamp01(color.b * shade) * 255f), 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true);

            return NewLayer(tex, tile);
        }

        static TerrainLayer FlatLayer(Color color, float tile)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Coast (flat sand)",
                wrapMode = TextureWrapMode.Repeat,
            };
            tex.SetPixel(0, 0, new Color(color.r, color.g, color.b, 0f));
            tex.Apply(false);

            return NewLayer(tex, tile);
        }

        static TerrainLayer NewLayer(Texture2D tex, float tile) =>
            new TerrainLayer
            {
                diffuseTexture = tex,
                tileSize = new Vector2(tile, tile),
                tileOffset = Vector2.zero,
                metallic = 0f,
                specular = Color.black,
                smoothness = Roughness,
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly,
            };

        static float[,] TileNoise(int size, int cells, int seed)
        {
            var lattice = new float[cells, cells];
            var rng = new System.Random(seed);
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                    lattice[j, i] = (float)rng.NextDouble();

            var field = new float[size, size];
            float scale = (float)cells / size;

            for (int j = 0; j < size; j++)
            {
                float fy = j * scale;
                int y0 = Mathf.FloorToInt(fy) % cells, y1 = (y0 + 1) % cells;
                float ty = fy - Mathf.Floor(fy);
                ty = ty * ty * (3f - 2f * ty);

                for (int i = 0; i < size; i++)
                {
                    float fx = i * scale;
                    int x0 = Mathf.FloorToInt(fx) % cells, x1 = (x0 + 1) % cells;
                    float tx = fx - Mathf.Floor(fx);
                    tx = tx * tx * (3f - 2f * tx);

                    float top = Mathf.Lerp(lattice[y0, x0], lattice[y0, x1], tx);
                    float bottom = Mathf.Lerp(lattice[y1, x0], lattice[y1, x1], tx);
                    field[j, i] = Mathf.Lerp(top, bottom, ty);
                }
            }
            return field;
        }

        static Material FlatMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return mat;
        }
    }
}
