using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public static class ProceduralTerrain
    {
        public const float Depth = 800f;
        internal const float HeightScale = 90f;
        public const float BaseLevel = 30f;
        internal const float MinHeight = 4f;
        public const float MaxHeight = 85f;
        internal const float FrontStrip = 130f;
        const int Res = 1025;

        internal const float CratersPerMetre = 0.017f;

        internal const float MinesPerMetre = 0.0035f;
        internal const float MineDepthShallow = 0.20f;
        internal const float MineDepthDeep = 0.62f;

        public const float CutRevealY = -80f;
        internal const float WallBottomY = -120f;
        internal const float WallSeamLift = 3f;

        public const string GrassTemplateResource = "GrassTerrainTemplate";
        public const int GrassTextureSeed = 1917;

        const int GrassDetailRes = 1024;
        internal const int GrassDetailPatch = 32;
        internal const float GrassSpacing = 4.5f;
        internal const int GrassPoissonTries = 20;
        internal const float GrassMaxSlopeDeg = 30f;
        internal const float CraterBareRadii = 1.35f;
        internal const float GrassViewDistance = 800f;

        static readonly Color LandColor = new Color(0.44f, 0.36f, 0.26f);
        static readonly Color DirtColor = new Color(0.36f, 0.28f, 0.20f);
        static readonly Color GrassHealthy = new Color(0.47f, 0.39f, 0.27f);
        static readonly Color GrassDry = new Color(0.40f, 0.32f, 0.22f);

        static TerrainData _grassTemplate;

        public static System.Func<float, float, bool> Build(int seed, float width,
            float cameraDistance, float playPlaneZ, Daytime daytime, Weather weather)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Battlefield Land");

            float[,] heights01 = GenerateHeights(rng, width, out float[] cutLine, out List<Vector3> craters);

            var data = NewTerrainData();
            data.heightmapResolution = Res;
            data.size = new Vector3(width, HeightScale, Depth);
            data.SetHeights(0, 0, heights01);
            PaintTerrain(data);
            PlantGrass(rng, data, width, craters);

            var terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            Mesh wallMesh = BuildCutWallMesh(cutLine, width);
            var wallMat = CutWallMaterial();

            for (int tile = -1; tile <= 1; tile++)
            {
                float x0 = -width / 2f + tile * width;

                var tGo = Terrain.CreateTerrainGameObject(data);
                tGo.name = $"Terrain (tile {tile})";
                tGo.transform.SetParent(root.transform);
                tGo.transform.position = new Vector3(x0, 0f, 0f);
                var terrain = tGo.GetComponent<Terrain>();
                terrain.materialTemplate = terrainMat;
                terrain.heightmapPixelError = 2f;
                terrain.basemapDistance = Depth * 4f;
                terrain.groupingID = 1;
                terrain.allowAutoConnect = true;
                terrain.detailObjectDistance = GrassViewDistance;
                terrain.detailObjectDensity = 1f;

                var wGo = new GameObject($"Cut Wall (tile {tile})", typeof(MeshFilter), typeof(MeshRenderer));
                wGo.transform.SetParent(root.transform);
                wGo.transform.position = new Vector3(tile * width, 0f, 0f);
                wGo.GetComponent<MeshFilter>().sharedMesh = wallMesh;
                var mr = wGo.GetComponent<MeshRenderer>();
                mr.sharedMaterial = wallMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            ApplyFog(daytime, cameraDistance, playPlaneZ);

            return (x, z) => InCrater(
                new Vector2(Mathf.Repeat(x + width / 2f, width), z), craters, width);
        }

        public static TerrainData NewTerrainData()
        {
            if (_grassTemplate == null) _grassTemplate = Resources.Load<TerrainData>(GrassTemplateResource);

            if (_grassTemplate == null)
            {
                Debug.LogWarning($"ProceduralTerrain: Resources/{GrassTemplateResource} is missing; "
                                 + "grass will not render in a standalone player.");
                return new TerrainData();
            }

            return Object.Instantiate(_grassTemplate);
        }

        internal static float FogEndDistance(float cameraDistance, float playPlaneZ)
            => cameraDistance - playPlaneZ + Depth - 250f;

        internal static void ApplyFog(Daytime daytime, float cameraDistance, float playPlaneZ)
        {
            Color haze;
            float startOffset;
            switch (daytime)
            {
                case Daytime.Midday: haze = MiddaySky.HazeColor; startOffset = 300f; break;
                case Daytime.Evening: haze = EveningSky.HazeColor; startOffset = 120f; break;
                case Daytime.Night: haze = NightSky.HazeColor; startOffset = 250f; break;
                default: haze = MorningSky.HazeColor; startOffset = 80f; break;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = cameraDistance + startOffset;
            RenderSettings.fogEndDistance = FogEndDistance(cameraDistance, playPlaneZ);
        }

        static float[,] GenerateHeights(System.Random rng, float width,
            out float[] cutLine, out List<Vector3> craters)
        {
            int[] cycles = { 2, 3, 5, 8 };
            float[] amps = { 10f, 6f, 3.5f, 2f };
            var phases = new float[cycles.Length];
            for (int i = 0; i < phases.Length; i++)
                phases[i] = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float ox1 = Offset(rng), oz1 = Offset(rng);
            float ox2 = Offset(rng), oz2 = Offset(rng);

            var metres = new float[Res, Res];
            for (int iz = 0; iz < Res; iz++)
            {
                float z = Depth * iz / (Res - 1);
                float zEff = Mathf.Max(z, FrontStrip);
                float depthRamp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FrontStrip, 220f, zEff));

                for (int ix = 0; ix < Res; ix++)
                {
                    float x = width * ix / (Res - 1);

                    float h = BaseLevel;
                    for (int i = 0; i < cycles.Length; i++)
                        h += amps[i] * Mathf.Sin(2f * Mathf.PI * cycles[i] * x / width + phases[i]);

                    h += (TileableNoise(x, zEff, 1f / 170f, ox1, oz1, width) - 0.5f) * 2f * 10f * depthRamp;
                    h += (TileableNoise(x, zEff, 1f / 30f, ox2, oz2, width) - 0.5f) * 2f * 1.6f;

                    metres[iz, ix] = h;
                }
            }

            craters = new List<Vector3>();
            StampCraters(rng, metres, width, craters);
            StampMineCraters(rng, metres, width, craters);

            for (int iz = 0; iz < Res; iz++)
            {
                for (int ix = 0; ix < Res; ix++)
                    metres[iz, ix] = Mathf.Clamp(metres[iz, ix], MinHeight, MaxHeight) / HeightScale;
                metres[iz, Res - 1] = metres[iz, 0];
            }

            cutLine = new float[Res];
            for (int ix = 0; ix < Res; ix++)
                cutLine[ix] = metres[0, ix] * HeightScale;

            return metres;
        }

        static void StampCraters(System.Random rng, float[,] metres, float width, List<Vector3> craters)
        {
            int count = Mathf.RoundToInt(width * CratersPerMetre);
            float xStep = width / (Res - 1);
            float zStep = Depth / (Res - 1);

            for (int c = 0; c < count; c++)
            {
                float cx = (float)rng.NextDouble() * width;
                float cz = Mathf.Lerp(10f, Depth - 40f, (float)rng.NextDouble());
                float radius = Mathf.Lerp(12f, 42f, (float)rng.NextDouble());
                float depth = radius * Mathf.Lerp(0.22f, 0.30f, (float)rng.NextDouble());
                craters.Add(new Vector3(cx, cz, radius * CraterBareRadii));
                float rim = depth * 0.35f;
                float rimSigma = radius * 0.35f;
                float influence = radius * 1.8f;

                int izMin = cz - influence < FrontStrip ? 0 : Mathf.FloorToInt((cz - influence) / zStep);
                int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((cz + influence) / zStep));
                int icx = Mathf.RoundToInt(cx / xStep);
                int ixSpan = Mathf.CeilToInt(influence / xStep);

                for (int iz = izMin; iz <= izMax; iz++)
                {
                    float dz = Mathf.Max(Depth * iz / (Res - 1), FrontStrip) - cz;

                    for (int j = icx - ixSpan; j <= icx + ixSpan; j++)
                    {
                        int ix = ((j % (Res - 1)) + (Res - 1)) % (Res - 1);
                        float dx = j * xStep - cx;

                        float r = Mathf.Sqrt(dx * dx + dz * dz);
                        if (r > influence) continue;
                        metres[iz, ix] += CraterDelta(r, radius, depth, rim, rimSigma);
                    }
                }
            }
        }

        static void StampMineCraters(System.Random rng, float[,] metres, float width, List<Vector3> craters)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(width * MinesPerMetre));
            float xStep = width / (Res - 1);
            float zStep = Depth / (Res - 1);

            for (int c = 0; c < count; c++)
            {
                float cx = (float)rng.NextDouble() * width;
                float cz = Mathf.Lerp(10f, Depth - 40f, (float)rng.NextDouble());
                float radius = Mathf.Lerp(40f, 80f, (float)rng.NextDouble());
                craters.Add(new Vector3(cx, cz, radius * CraterBareRadii));

                float u = (float)rng.NextDouble();
                float depthFrac = Mathf.Lerp(MineDepthShallow, MineDepthDeep, 1f - u * u);
                float depth = radius * depthFrac;
                float rim = depth * 0.45f;
                float rimSigma = radius * 0.3f;
                float influence = radius * 1.7f;

                int izMin = cz - influence < FrontStrip ? 0 : Mathf.FloorToInt((cz - influence) / zStep);
                int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((cz + influence) / zStep));
                int icx = Mathf.RoundToInt(cx / xStep);
                int ixSpan = Mathf.CeilToInt(influence / xStep);

                for (int iz = izMin; iz <= izMax; iz++)
                {
                    float dz = Mathf.Max(Depth * iz / (Res - 1), FrontStrip) - cz;

                    for (int j = icx - ixSpan; j <= icx + ixSpan; j++)
                    {
                        int ix = ((j % (Res - 1)) + (Res - 1)) % (Res - 1);
                        float dx = j * xStep - cx;

                        float r = Mathf.Sqrt(dx * dx + dz * dz);
                        if (r > influence) continue;
                        metres[iz, ix] += CraterDelta(r, radius, depth, rim, rimSigma);
                    }
                }
            }
        }

        internal static float CraterDelta(float r, float radius, float depth, float rim, float rimSigma)
        {
            float delta = rim * Mathf.Exp(-((r - radius) / rimSigma) * ((r - radius) / rimSigma));
            if (r < radius)
            {
                float t = 1f - (r / radius) * (r / radius);
                delta -= depth * t * t;
            }
            return delta;
        }

        static float TileableNoise(float x, float z, float frequency, float ox, float oz, float width)
        {
            float a = Mathf.PerlinNoise(x * frequency + ox, z * frequency + oz);
            float b = Mathf.PerlinNoise((x - width) * frequency + ox, z * frequency + oz);
            return Mathf.Lerp(a, b, x / width);
        }

        static float Offset(System.Random rng) => (float)(rng.NextDouble() * 1000.0 + 100.0);

        static void PaintTerrain(TerrainData data) => PaintTerrain(data, CreateLandLayer());

        internal static TerrainLayer CreateLandLayer()
        {
            return new TerrainLayer
            {
                diffuseTexture = SolidTexture(LandColor),
                tileSize = new Vector2(25f, 25f),
                tileOffset = Vector2.zero,
                metallic = 0f,
                smoothness = 0f,
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly,
            };
        }

        internal static void PaintTerrain(TerrainData data, TerrainLayer layer)
        {
            data.terrainLayers = new[] { layer };

            data.alphamapResolution = 64;
            var alpha = new float[64, 64, 1];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    alpha[y, x, 0] = 1f;
            data.SetAlphamaps(0, 0, alpha);
        }

        static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Land (flat colour)" };
            tex.SetPixel(0, 0, new Color(color.r, color.g, color.b, 0f));
            tex.Apply(false);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        public static DetailPrototype CreateGrassPrototype(Texture2D bladesTexture)
        {
            return new DetailPrototype
            {
                prototypeTexture = bladesTexture,
                renderMode = DetailRenderMode.GrassBillboard,
                usePrototypeMesh = false,
                minWidth = 3f, maxWidth = 6f,
                minHeight = 3f, maxHeight = 7f,
                noiseSpread = 0.15f,
                healthyColor = GrassHealthy,
                dryColor = GrassDry,
            };
        }

        public static void SetupGrassDetail(TerrainData data, int detailRes)
        {
            if (data.detailPrototypes == null || data.detailPrototypes.Length == 0)
                data.detailPrototypes = new[]
                {
                    CreateGrassPrototype(GrassBladesTexture(new System.Random(GrassTextureSeed))),
                };

            data.SetDetailResolution(detailRes, GrassDetailPatch);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            data.wavingGrassTint = new Color(0.95f, 0.93f, 0.85f);
            data.wavingGrassStrength = 0.22f;
            data.wavingGrassAmount = 0.25f;
            data.wavingGrassSpeed = 0.3f;
        }

        static void PlantGrass(System.Random rng, TerrainData data, float width, List<Vector3> craters)
        {
            SetupGrassDetail(data, GrassDetailRes);

            var layer = new int[GrassDetailRes, GrassDetailRes];
            foreach (var p in PoissonDiskPoints(rng, width, Depth, GrassSpacing, GrassPoissonTries))
            {
                float xNorm = p.x / width, zNorm = p.y / Depth;

                if (InCrater(p, craters, width)) continue;
                if (data.GetSteepness(xNorm, zNorm) > GrassMaxSlopeDeg) continue;

                int ix = Mathf.Min(GrassDetailRes - 1, (int)(xNorm * GrassDetailRes));
                int iz = Mathf.Min(GrassDetailRes - 1, (int)(zNorm * GrassDetailRes));
                layer[iz, ix]++;
            }
            data.SetDetailLayer(0, 0, 0, layer);
        }

        static bool InCrater(Vector2 p, List<Vector3> craters, float width)
        {
            float zEff = Mathf.Max(p.y, FrontStrip);
            foreach (var c in craters)
            {
                float dx = Mathf.Abs(c.x - p.x);
                dx = Mathf.Min(dx, width - dx);
                float dz = zEff - c.y;
                if (dx * dx + dz * dz < c.z * c.z) return true;
            }
            return false;
        }

        static List<Vector2> PoissonDiskPoints(
            System.Random rng, float width, float depth, float radius, int tries)
        {
            float cell = radius / Mathf.Sqrt(2f);
            int nx = Mathf.CeilToInt(width / cell);
            int nz = Mathf.CeilToInt(depth / cell);
            var grid = new int[nz, nx];
            var points = new List<Vector2>();
            var active = new List<int>();

            int GX(float x) => Mathf.Min(nx - 1, (int)(x / cell));
            int GZ(float y) => Mathf.Min(nz - 1, (int)(y / cell));

            void Add(Vector2 p)
            {
                points.Add(p);
                active.Add(points.Count - 1);
                grid[GZ(p.y), GX(p.x)] = points.Count;
            }

            bool Fits(Vector2 p)
            {
                int gx = GX(p.x), gz = GZ(p.y);
                for (int dz = -2; dz <= 2; dz++)
                {
                    int z = gz + dz;
                    if (z < 0 || z >= nz) continue;
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int idx = grid[z, ((gx + dx) % nx + nx) % nx];
                        if (idx == 0) continue;

                        Vector2 q = points[idx - 1];
                        float ddx = Mathf.Abs(q.x - p.x);
                        ddx = Mathf.Min(ddx, width - ddx);
                        float ddz = q.y - p.y;
                        if (ddx * ddx + ddz * ddz < radius * radius) return false;
                    }
                }
                return true;
            }

            Add(new Vector2((float)rng.NextDouble() * width, (float)rng.NextDouble() * depth));

            while (active.Count > 0)
            {
                int ai = rng.Next(active.Count);
                Vector2 centre = points[active[ai]];

                bool placed = false;
                for (int t = 0; t < tries; t++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float dist = radius * (1f + (float)rng.NextDouble());
                    var p = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                    p.x = Mathf.Repeat(p.x, width);
                    if (p.x >= width) p.x = 0f;
                    if (p.y < 0f || p.y >= depth || !Fits(p)) continue;

                    Add(p);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    active[ai] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                }
            }
            return points;
        }

        public static Texture2D GrassBladesTexture(System.Random rng)
        {
            const int W = 64, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true)
            {
                name = "Grass blades (generated)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[W * H];
            var bg = new Color(0.9f, 0.9f, 0.9f, 0f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            const int Blades = 9;
            for (int b = 0; b < Blades; b++)
            {
                float baseX = Mathf.Lerp(5f, W - 5f, (float)rng.NextDouble());
                float lean = ((float)rng.NextDouble() - 0.5f) * 36f;
                float height = Mathf.Lerp(H * 0.55f, H * 0.95f, (float)rng.NextDouble());
                float shade = Mathf.Lerp(0.82f, 1f, (float)rng.NextDouble());
                var tone = new Color(shade, shade, shade);

                for (int y = 0; y < (int)height; y++)
                {
                    float t = y / height;
                    float centre = baseX + lean * t * t;
                    float half = Mathf.Lerp(2.4f, 0.5f, t);
                    Color c = tone * Mathf.Lerp(0.7f, 1f, t);
                    c.a = 1f;

                    int x0 = Mathf.FloorToInt(centre - half), x1 = Mathf.CeilToInt(centre + half);
                    for (int px = Mathf.Max(0, x0); px <= Mathf.Min(W - 1, x1); px++)
                        if (Mathf.Abs(px - centre) <= Mathf.Max(half, 0.5f))
                            pixels[y * W + px] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        internal static Mesh BuildCutWallMesh(float[] cutLine, float width)
        {
            int cols = cutLine.Length;
            var verts = new Vector3[cols * 2];
            var normals = new Vector3[cols * 2];

            for (int i = 0; i < cols; i++)
            {
                float x = -width / 2f + width * i / (cols - 1);
                float top = cutLine[i] + WallSeamLift;

                verts[i * 2] = new Vector3(x, top, 0f);
                verts[i * 2 + 1] = new Vector3(x, WallBottomY, 0f);

                normals[i * 2] = Vector3.back;
                normals[i * 2 + 1] = Vector3.back;
            }

            var tris = new int[(cols - 1) * 6];
            for (int i = 0; i < cols - 1; i++)
            {
                int t0 = i * 2, b0 = i * 2 + 1, t1 = i * 2 + 2, b1 = i * 2 + 3;
                int k = i * 6;
                tris[k] = t0; tris[k + 1] = t1; tris[k + 2] = b0;
                tris[k + 3] = t1; tris[k + 4] = b1; tris[k + 5] = b0;
            }

            var mesh = new Mesh { name = "Cut Wall (generated)" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Material CutWallMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", DirtColor);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return mat;
        }
    }
}
