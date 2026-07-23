using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    /// <summary>
    /// Endless streamed version of the Verdun-style land for the campaign levels: fixed-length
    /// terrain chunks are generated at runtime ahead of the camera and destroyed once they fall
    /// behind the visible haze. Chunks are seamless by construction and generation is
    /// time-sliced so the flight never hitches — see docs/campaign.md for the full design.
    /// </summary>
    public class CampaignTerrain : MonoBehaviour
    {
        public const float ChunkLength = 512f;
        const int Res = 257;                              // per-chunk heightmap; X step = 2 m exactly,
                                                          // matching the fixed Verdun level's fidelity
        const float XStep = ChunkLength / (Res - 1);
        const float Depth = ProceduralTerrain.Depth;
        const float ZStep = Depth / (Res - 1);
        const int GrassDetailRes = 512;
        const float CellSize = 128f;                      // crater-hash cell width along X
        const float MaxCraterReach = 150f;                // widest crater influence (80 m * 1.7)
        const double BuildBudgetMs = 3.0;                 // main-thread slice per frame while streaming
        const int RowsPerStep = 16;                       // heightmap rows per slice checkpoint

        int _seed;
        float _keepBehind, _keepAhead;

        // Seeded offsets standing in for a Perlin seed, fixed for the level's lifetime.
        float _r1, _r2, _r3, _ox1, _oz1, _ox2, _oz2;

        // Assets shared by every chunk, created once.
        TerrainLayer _landLayer;
        Material _terrainMat;
        Material _wallMat;
        DetailPrototype _grassPrototype;

        class Chunk
        {
            public GameObject root;
            public Terrain terrain;
            public TerrainData data;
            public Mesh wallMesh;
        }

        struct CraterSpec
        {
            public float x, z, radius, depth, rim, rimSigma, influence, bareRadius;
        }

        readonly SortedDictionary<int, Chunk> _chunks = new SortedDictionary<int, Chunk>();
        readonly List<int> _removeScratch = new List<int>();
        bool _building; // set before StartCoroutine so a build finishing synchronously can't wedge it

        /// <summary>
        /// Creates the streamer, applies the daytime fog, and synchronously builds the opening
        /// window of chunks around <paramref name="startCamX"/> (a short scene-load beat instead
        /// of land popping in at the spawn). <paramref name="weather"/> is the future modulation
        /// seam; <see cref="Weather.Calm"/> changes nothing.
        /// </summary>
        public static CampaignTerrain Begin(int seed, Daytime daytime, Weather weather,
            float cameraDistance, float playPlaneZ, float startCamX)
        {
            var streamer = new GameObject("Campaign Land").AddComponent<CampaignTerrain>();
            streamer._seed = seed;

            var rng = new System.Random(seed);
            streamer._r1 = Offset(rng);
            streamer._r2 = Offset(rng);
            streamer._r3 = Offset(rng);
            streamer._ox1 = Offset(rng);
            streamer._oz1 = Offset(rng);
            streamer._ox2 = Offset(rng);
            streamer._oz2 = Offset(rng);

            streamer._landLayer = ProceduralTerrain.CreateLandLayer();
            streamer._terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            streamer._wallMat = ProceduralTerrain.CutWallMaterial();
            streamer._grassPrototype = ProceduralTerrain.CreateGrassPrototype(
                ProceduralTerrain.GrassBladesTexture(new System.Random(seed)));

            ProceduralTerrain.ApplyFog(daytime, cameraDistance, playPlaneZ);

            // Past the fog's saturation distance the land is pure haze (matching the skybox's
            // horizon band), so a chunk beyond it can vanish or not exist yet without ever
            // being seen. The ahead margin adds build lead time.
            float fogEnd = ProceduralTerrain.FogEndDistance(cameraDistance, playPlaneZ);
            streamer._keepBehind = fogEnd + ChunkLength * 0.5f;
            streamer._keepAhead = fogEnd + ChunkLength * 1.5f;

            foreach (int i in streamer.MissingChunks(startCamX))
            {
                var steps = streamer.BuildChunk(i);
                while (steps.MoveNext()) { }
            }
            return streamer;
        }

        /// <summary>
        /// Keeps the chunk window centred on the camera: drops chunks behind the haze, starts
        /// (at most one at a time) time-sliced builds for missing ones ahead. Call every frame
        /// with the camera's X.
        /// </summary>
        public void UpdateStreaming(float camX)
        {
            int first = FirstChunk(camX);
            int last = LastChunk(camX);

            _removeScratch.Clear();
            foreach (var kv in _chunks)
                if (kv.Key < first || kv.Key > last) _removeScratch.Add(kv.Key);
            foreach (int i in _removeScratch) RemoveChunk(i);

            if (!_building)
                foreach (int i in MissingChunks(camX))
                {
                    _building = true;
                    StartCoroutine(BuildChunkSliced(i));
                    break;
                }
        }

        int FirstChunk(float camX) => Mathf.FloorToInt((camX - _keepBehind) / ChunkLength);
        int LastChunk(float camX) => Mathf.FloorToInt((camX + _keepAhead) / ChunkLength);

        IEnumerable<int> MissingChunks(float camX)
        {
            for (int i = FirstChunk(camX); i <= LastChunk(camX); i++)
                if (!_chunks.ContainsKey(i)) yield return i;
        }

        /// <summary>Runs one chunk build spread over frames: each frame advances the build
        /// iterator until the time budget is spent, then yields.</summary>
        IEnumerator BuildChunkSliced(int index)
        {
            var steps = BuildChunk(index);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (steps.MoveNext())
            {
                if (timer.Elapsed.TotalMilliseconds < BuildBudgetMs) continue;
                yield return null;
                timer.Restart();
            }
            _building = false;
        }

        // ---------------------------------------------------------------- chunk build

        /// <summary>
        /// The whole build of chunk <paramref name="index"/> as checkpointed steps (each
        /// MoveNext is one affordable slice of work). Seams need no stitching: heights are a
        /// continuous function of world X sampled at globally indexed columns, and craters come
        /// from hashed world cells, so both chunks sharing an edge compute bit-identical values
        /// for it.
        /// </summary>
        IEnumerator BuildChunk(int index)
        {
            float x0 = index * ChunkLength;
            var craters = CratersForRange(x0 - MaxCraterReach, x0 + ChunkLength + MaxCraterReach);

            var heights = new float[Res, Res];
            for (int iz = 0; iz < Res; iz += RowsPerStep)
            {
                FillRows(heights, index, iz, Mathf.Min(Res, iz + RowsPerStep));
                yield return null;
            }

            foreach (var c in craters)
            {
                StampCrater(heights, index, c);
                yield return null;
            }

            for (int iz = 0; iz < Res; iz++)
                for (int ix = 0; ix < Res; ix++)
                    heights[iz, ix] = Mathf.Clamp(heights[iz, ix],
                        ProceduralTerrain.MinHeight, ProceduralTerrain.MaxHeight)
                        / ProceduralTerrain.HeightScale;
            yield return null;

            var cutLine = new float[Res];
            for (int ix = 0; ix < Res; ix++)
                cutLine[ix] = heights[0, ix] * ProceduralTerrain.HeightScale;

            var data = new TerrainData();
            data.heightmapResolution = Res;
            data.size = new Vector3(ChunkLength, ProceduralTerrain.HeightScale, Depth);
            yield return null;
            data.SetHeights(0, 0, heights);
            yield return null;
            ProceduralTerrain.PaintTerrain(data, _landLayer);
            ProceduralTerrain.SetupGrassDetail(data, _grassPrototype, GrassDetailRes);
            yield return null;

            foreach (var step in PlantGrass(data, index, craters))
                yield return step;

            AssembleChunk(index, data, cutLine);
        }

        /// <summary>
        /// The base height field for rows [<paramref name="izFrom"/>, <paramref name="izTo"/>):
        /// the fixed level's shape (front strip flattening, depth drift, roughness), but with
        /// the ridge line as world-space Perlin octaves — the whole-cycle sines only tile a
        /// finite width, while Perlin is continuous forever. Columns are sampled at global
        /// indices so a seam column's world X (and height) is bit-identical in both chunks.
        /// </summary>
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

        static float WorldX(int chunkIndex, int ix) => ((long)chunkIndex * (Res - 1) + ix) * XStep;

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
                // Same front-strip flattening as the base field, so craters near the play line
                // cut into it as a constant profile.
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

        // ---------------------------------------------------------------- craters

        /// <summary>
        /// Every crater whose centre lies in [<paramref name="xMin"/>, <paramref name="xMax"/>],
        /// generated deterministically from hashed fixed-width world cells: any chunk that asks
        /// about a cell gets the same craters, which is what keeps overlapping stamps identical
        /// on both sides of a seam. Densities and shapes match the fixed level's.
        /// </summary>
        List<CraterSpec> CratersForRange(float xMin, float xMax)
        {
            var list = new List<CraterSpec>();
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
                    // Same deep-biased depth roll as the fixed level's mine pits.
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
            return list;
        }

        /// <summary>Rounds an expected count to an integer with the fraction as spawn chance,
        /// so fractional densities per cell still average out right across the land.</summary>
        static int CountForDensity(System.Random rng, float expected)
        {
            int count = (int)expected;
            if (rng.NextDouble() < expected - count) count++;
            return count;
        }

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

        static float Offset(System.Random rng) => (float)(rng.NextDouble() * 1000.0 + 100.0);

        // ---------------------------------------------------------------- grass

        /// <summary>
        /// Plants the chunk's grass on a deterministic jittered grid (one tuft per
        /// grass-spacing cell, random offset inside it): near-even spacing like the fixed
        /// level's Poisson sward, but cheap and sliceable row by row, which Bridson sampling is
        /// not. Craters (bowl + rim) and steep walls stay bare, as before.
        /// </summary>
        IEnumerable<object> PlantGrass(TerrainData data, int index, List<CraterSpec> craters)
        {
            // Cell counts are rounded so the grid divides the chunk exactly: a floored count
            // left a tuftless remainder strip at the chunk's end, which read as a bare stitch
            // at every seam.
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

        // ---------------------------------------------------------------- chunk lifecycle

        void AssembleChunk(int index, TerrainData data, float[] cutLine)
        {
            float x0 = index * ChunkLength;
            var root = new GameObject($"Chunk {index}");
            root.transform.SetParent(transform, false);

            var tGo = Terrain.CreateTerrainGameObject(data);
            tGo.name = "Terrain";
            tGo.transform.SetParent(root.transform);
            tGo.transform.position = new Vector3(x0, 0f, 0f);
            var terrain = tGo.GetComponent<Terrain>();
            terrain.materialTemplate = _terrainMat;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = Depth * 4f;
            terrain.drawInstanced = true;
            terrain.groupingID = 1;
            terrain.allowAutoConnect = true;
            terrain.detailObjectDistance = ProceduralTerrain.GrassViewDistance;
            terrain.detailObjectDensity = 1f;

            var wallMesh = ProceduralTerrain.BuildCutWallMesh(cutLine, ChunkLength);
            var wGo = new GameObject("Cut Wall", typeof(MeshFilter), typeof(MeshRenderer));
            wGo.transform.SetParent(root.transform);
            wGo.transform.position = new Vector3(x0 + ChunkLength / 2f, 0f, 0f);
            wGo.GetComponent<MeshFilter>().sharedMesh = wallMesh;
            var mr = wGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _wallMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            _chunks[index] = new Chunk { root = root, terrain = terrain, data = data, wallMesh = wallMesh };

            // Explicit neighbour links (on top of auto-connect) so terrain LOD picks matching
            // tessellation across the seam and never opens a crack.
            LinkNeighbors(index - 1);
            LinkNeighbors(index);
            LinkNeighbors(index + 1);
        }

        void LinkNeighbors(int index)
        {
            if (!_chunks.TryGetValue(index, out var chunk)) return;
            _chunks.TryGetValue(index - 1, out var left);
            _chunks.TryGetValue(index + 1, out var right);
            chunk.terrain.SetNeighbors(left?.terrain, null, right?.terrain, null);
        }

        void RemoveChunk(int index)
        {
            if (!_chunks.TryGetValue(index, out var chunk)) return;
            _chunks.Remove(index);
            Destroy(chunk.root);
            Destroy(chunk.data);     // TerrainData is an asset; the GameObject won't free it
            Destroy(chunk.wallMesh);
            LinkNeighbors(index - 1);
            LinkNeighbors(index + 1);
        }

        void OnDestroy()
        {
            foreach (var kv in _chunks)
            {
                Destroy(kv.Value.data);
                Destroy(kv.Value.wallMesh);
            }
            _chunks.Clear();
        }
    }
}
