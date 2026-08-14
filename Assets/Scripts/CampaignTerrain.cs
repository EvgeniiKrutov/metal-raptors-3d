using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public abstract class CampaignTerrain : MonoBehaviour
    {
        public const float ChunkLength = 512f;
        protected const int Res = 257;
        protected const float XStep = ChunkLength / (Res - 1);
        protected const float Depth = ProceduralTerrain.Depth;
        protected const float ZStep = Depth / (Res - 1);
        const double BuildBudgetMs = 3.0;

        protected int _seed;
        float _keepBehind, _keepAhead;
        Material _terrainMat;

        class Chunk
        {
            public GameObject root;
            public Terrain terrain;
            public TerrainData data;
            public List<Mesh> meshes;
        }

        readonly SortedDictionary<int, Chunk> _chunks = new SortedDictionary<int, Chunk>();
        readonly List<int> _removeScratch = new List<int>();
        bool _building;

        protected abstract float HeightScale { get; }
        protected abstract float TerrainY { get; }
        protected abstract float MinHeight { get; }
        protected abstract float MaxHeight { get; }
        protected virtual float DetailDistance => ProceduralTerrain.GrassViewDistance;

        protected abstract void Prepare(Daytime daytime, Weather weather,
            float cameraDistance, float playPlaneZ);
        protected abstract IEnumerator FillHeights(float[,] metres, int index);
        protected abstract void PaintChunk(TerrainData data, int index);
        protected abstract IEnumerable<object> Decorate(TerrainData data, int index);
        protected abstract void AddChunkMeshes(int index, Transform root, float[] cutLine,
            List<Mesh> owned);

        public virtual bool InCrater(float worldX, float z) => false;

        public static CampaignTerrain Begin(TerrainKind kind, int seed, Daytime daytime,
            Weather weather, float cameraDistance, float playPlaneZ, float startCamX)
        {
            bool coast = kind == TerrainKind.Flanders;
            var go = new GameObject(coast ? "Flanders Coast" : "Campaign Land");
            CampaignTerrain land = coast
                ? (CampaignTerrain)go.AddComponent<FlandersTerrain>()
                : go.AddComponent<VerdunTerrain>();

            land._seed = seed;
            land._terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            land.Prepare(daytime, weather, cameraDistance, playPlaneZ);

            float keep = ProceduralTerrain.FogEndDistance(cameraDistance, playPlaneZ);
            land._keepBehind = keep + ChunkLength * 0.5f;
            land._keepAhead = keep + ChunkLength * 1.5f;

            foreach (int i in land.MissingChunks(startCamX))
            {
                var steps = land.BuildChunk(i);
                while (steps.MoveNext()) { }
            }
            return land;
        }

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

        IEnumerator BuildChunk(int index)
        {
            var metres = new float[Res, Res];

            var fill = FillHeights(metres, index);
            while (fill.MoveNext()) yield return null;

            float low = MinHeight, high = MaxHeight;
            for (int iz = 0; iz < Res; iz++)
                for (int ix = 0; ix < Res; ix++)
                    metres[iz, ix] = Mathf.Clamp(metres[iz, ix], low, high);
            yield return null;

            var cutLine = new float[Res];
            for (int ix = 0; ix < Res; ix++) cutLine[ix] = metres[0, ix];

            float scale = HeightScale;
            float baseY = TerrainY;
            for (int iz = 0; iz < Res; iz++)
                for (int ix = 0; ix < Res; ix++)
                    metres[iz, ix] = (metres[iz, ix] - baseY) / scale;
            yield return null;

            var data = ProceduralTerrain.NewTerrainData();
            data.heightmapResolution = Res;
            data.size = new Vector3(ChunkLength, scale, Depth);
            yield return null;
            data.SetHeights(0, 0, metres);
            yield return null;
            PaintChunk(data, index);
            yield return null;

            foreach (var step in Decorate(data, index))
                yield return step;

            AssembleChunk(index, data, cutLine);
        }

        void AssembleChunk(int index, TerrainData data, float[] cutLine)
        {
            float x0 = index * ChunkLength;
            var root = new GameObject($"Chunk {index}");
            root.transform.SetParent(transform, false);

            var tGo = Terrain.CreateTerrainGameObject(data);
            tGo.name = "Terrain";
            tGo.layer = ProceduralTerrain.GroundLayer;
            tGo.transform.SetParent(root.transform);
            tGo.transform.position = new Vector3(x0, TerrainY, 0f);
            var terrain = tGo.GetComponent<Terrain>();
            terrain.materialTemplate = _terrainMat;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = Depth * 4f;
            terrain.groupingID = 1;
            terrain.allowAutoConnect = true;
            terrain.detailObjectDistance = DetailDistance;
            terrain.detailObjectDensity = 1f;

            var owned = new List<Mesh>();
            AddChunkMeshes(index, root.transform, cutLine, owned);

            _chunks[index] = new Chunk { root = root, terrain = terrain, data = data, meshes = owned };

            LinkNeighbors(index - 1);
            LinkNeighbors(index);
            LinkNeighbors(index + 1);
        }

        protected static void AddMesh(Transform parent, string name, Mesh mesh, Material material,
            Vector3 position, List<Mesh> owned)
        {
            if (mesh == null) return;

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            owned.Add(mesh);
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
            DisposeChunk(chunk);
            LinkNeighbors(index - 1);
            LinkNeighbors(index + 1);
        }

        static void DisposeChunk(Chunk chunk)
        {
            Destroy(chunk.data);
            foreach (var mesh in chunk.meshes) Destroy(mesh);
            chunk.meshes.Clear();
        }

        void OnDestroy()
        {
            foreach (var kv in _chunks) DisposeChunk(kv.Value);
            _chunks.Clear();
        }

        protected static float WorldX(int chunkIndex, int ix) =>
            ((long)chunkIndex * (Res - 1) + ix) * XStep;

        protected static int CountForDensity(System.Random rng, float expected)
        {
            int count = (int)expected;
            if (rng.NextDouble() < expected - count) count++;
            return count;
        }

        protected static float Offset(System.Random rng) => (float)(rng.NextDouble() * 1000.0 + 100.0);

        protected static float Range(System.Random rng, float min, float max) =>
            Mathf.Lerp(min, max, (float)rng.NextDouble());

        protected static int Hash(int seed, int cell, int salt)
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
