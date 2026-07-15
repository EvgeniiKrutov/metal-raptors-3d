using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    /// <summary>
    /// Runtime-generated battlefield land for the 2.5D levels: a plain-brown Unity Terrain of
    /// rolling hills pocked by shell craters, receding ~500 m toward the horizon (+Z) where a
    /// pale foggy-morning mist (linear fog) swallows it. Along the front edge (z = 0, just in
    /// front of the play line) a flat-coloured "cut wall" mesh shows the dirt cross-section
    /// below the ground line, Hill Climb style, so a low camera reveals the inside of the earth.
    ///
    /// The height field tiles seamlessly along X (sine octaves with whole cycle counts,
    /// blended Perlin, wrap-aware crater stamps), and one extra terrain + wall copy sits on
    /// each side so the land runs well past the play area's soft side boundaries — the view
    /// (which follows the cube, and the cube is steered back before it reaches the edge) never
    /// runs off the end of the world.
    ///
    /// Surfaces are plain flat colours (no generated textures); heightmap, materials and fog
    /// are built in code from one seed — no assets to import, same land every run.
    /// </summary>
    public static class ProceduralTerrain
    {
        // ---- Land shape (metres) ----
        public const float Depth = 520f;       // how far the land runs toward the horizon (+Z)
        const float HeightScale = 90f;         // terrain-data vertical range (heights are 0..1 of this)
        const float BaseLevel = 30f;           // mean ground height the hills undulate around
        const float MinHeight = 4f;            // never carve below this (keeps dirt above the wall's UVs)
        const float MaxHeight = 85f;           // never build above this
        const float FrontStrip = 130f;         // near z-band kept height-constant: the play plane sits
                                               // back from z=0 (see LevelController.PlayPlaneZ). Keep this
                                               // past the cube's far Z so it flies/lands over a flat front
                                               // terrace — never behind an intervening ridge — and so the
                                               // cut wall's raised top hides against flat dirt (below)
        const int Res = 1025;                  // heightmap resolution (square)

        // ---- Craters ----
        const float CratersPerMetre = 0.017f;  // ~34 craters across 2000 m ("moderate")

        // ---- Mine craters ----
        // Big pits blown from a gallery driven under the line. Placed at random across the field,
        // with depth varying widely per-crater: most are deep, but a random minority stay shallow.
        const float MinesPerMetre = 0.0035f;   // ~3 per 800 m
        const float MineDepthShallow = 0.20f;  // depth-to-radius ratio for the shallowest mines
        const float MineDepthDeep = 0.62f;     // ...and for the deepest

        // ---- What the camera may reveal ----
        public const float CutRevealY = -80f;  // lowest world Y the camera should ever show
        const float WallBottomY = -120f;       // the cut wall reaches safely below that
        const float WallSeamLift = 3f;         // the cut wall's top overshoots the terrain surface by this
                                               // much, so it always covers the terrain's front edge and
                                               // leaves no see-through seam despite terrain LOD; the extra
                                               // sliver is hidden against the flat dirt terrace behind it

        // ---- Palette (plain flat colours — no generated textures) ----
        // Pale, bright mist so the sky and fog read like a foggy morning rather than war haze.
        public static readonly Color HazeColor = new Color(0.82f, 0.83f, 0.85f); // fog + sky
        static readonly Color LandColor = new Color(0.44f, 0.36f, 0.26f);        // flat surface earth
        static readonly Color DirtColor = new Color(0.36f, 0.28f, 0.20f);        // flat cross-section dirt

        /// <summary>
        /// Builds the whole land into the active scene. <paramref name="width"/> is the level's
        /// playable width (the terrain spans x = -width/2 .. +width/2, plus one copy per side).
        /// <paramref name="cameraDistance"/> is how far back on -Z the gameplay camera sits from the
        /// play line, so the fog can start just behind it. <paramref name="playPlaneZ"/> is how far
        /// the play line sits into the land (+Z): the terrain's front edge is that much closer to the
        /// camera, so the fog's far end is anchored to the real far-edge distance.
        /// </summary>
        public static void Build(int seed, float width, float cameraDistance, float playPlaneZ)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Battlefield Land");

            float[,] heights01 = GenerateHeights(rng, width, out float[] cutLine);

            var data = new TerrainData();
            data.heightmapResolution = Res;
            data.size = new Vector3(width, HeightScale, Depth); // set after resolution (resolution resets size)
            data.SetHeights(0, 0, heights01);
            PaintTerrain(data);

            var terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            Mesh wallMesh = BuildCutWallMesh(cutLine, width);
            var wallMat = CutWallMaterial();

            // Main land at tile 0, one visual copy per side so the land runs past the play
            // area's soft side boundaries and the view never shows the end of the world.
            for (int tile = -1; tile <= 1; tile++)
            {
                float x0 = -width / 2f + tile * width;

                var tGo = Terrain.CreateTerrainGameObject(data);
                tGo.name = $"Terrain (tile {tile})";
                tGo.transform.SetParent(root.transform);
                tGo.transform.position = new Vector3(x0, 0f, 0f);
                var terrain = tGo.GetComponent<Terrain>();
                terrain.materialTemplate = terrainMat;
                terrain.heightmapPixelError = 2f;   // tight LOD so the silhouette matches the cut wall
                terrain.basemapDistance = Depth * 4f;
                terrain.drawInstanced = true;
                terrain.groupingID = 1;
                terrain.allowAutoConnect = true;

                var wGo = new GameObject($"Cut Wall (tile {tile})", typeof(MeshFilter), typeof(MeshRenderer));
                wGo.transform.SetParent(root.transform);
                wGo.transform.position = new Vector3(tile * width, 0f, 0f);
                wGo.GetComponent<MeshFilter>().sharedMesh = wallMesh;
                var mr = wGo.GetComponent<MeshRenderer>();
                mr.sharedMaterial = wallMat;
                mr.shadowCastingMode = ShadowCastingMode.Off; // a cross-section shouldn't shade the land
            }

            // Pale foggy-morning mist: clear at the play line, total by the terrain's far edge.
            // The far edge is at (front-edge distance) + Depth = (cameraDistance - playPlaneZ) + Depth.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = HazeColor;
            RenderSettings.fogStartDistance = cameraDistance + 80f;
            RenderSettings.fogEndDistance = cameraDistance - playPlaneZ + Depth - 60f;
            RenderSettings.ambientMode = AmbientMode.Flat;   // soft, even morning light — no harsh sky bounce
            RenderSettings.ambientLight = new Color(0.68f, 0.68f, 0.70f);
        }

        // ---------------------------------------------------------------- height field

        /// <summary>
        /// Fills the normalized heightmap and returns the front-edge surface line (metres, one
        /// entry per X sample) for the cut wall. heights[iz, ix]: iz runs along Z, ix along X.
        /// </summary>
        static float[,] GenerateHeights(System.Random rng, float width, out float[] cutLine)
        {
            // Rolling base: sine octaves with whole numbers of cycles across the width, so the
            // profile tiles exactly. Random phases make each seed a different ridge line.
            int[] cycles = { 2, 3, 5, 8 };
            float[] amps = { 10f, 6f, 3.5f, 2f };
            var phases = new float[cycles.Length];
            for (int i = 0; i < phases.Length; i++)
                phases[i] = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            // Unity's Perlin has no seed; random offsets stand in for one.
            float ox1 = Offset(rng), oz1 = Offset(rng);
            float ox2 = Offset(rng), oz2 = Offset(rng);

            var metres = new float[Res, Res];
            for (int iz = 0; iz < Res; iz++)
            {
                float z = Depth * iz / (Res - 1);
                // Inside the front strip the land is constant along Z, so the collision surface,
                // the visible silhouette and the cut wall's top are all the same line.
                float zEff = Mathf.Max(z, FrontStrip);
                float depthRamp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FrontStrip, 220f, zEff));

                for (int ix = 0; ix < Res; ix++)
                {
                    float x = width * ix / (Res - 1);

                    float h = BaseLevel;
                    for (int i = 0; i < cycles.Length; i++)
                        h += amps[i] * Mathf.Sin(2f * Mathf.PI * cycles[i] * x / width + phases[i]);

                    // Hills further from the play line drift away from the front profile...
                    h += (TileableNoise(x, zEff, 1f / 170f, ox1, oz1, width) - 0.5f) * 2f * 10f * depthRamp;
                    // ...and a little roughness everywhere keeps the land from looking drawn with a ruler.
                    h += (TileableNoise(x, zEff, 1f / 30f, ox2, oz2, width) - 0.5f) * 2f * 1.6f;

                    metres[iz, ix] = h;
                }
            }

            StampCraters(rng, metres, width);
            StampMineCraters(rng, metres, width);

            // Clamp, normalize, and force the tiling column to be bit-identical.
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

        /// <summary>Shell craters: a smooth bowl with a raised rim, wrap-aware along X.</summary>
        static void StampCraters(System.Random rng, float[,] metres, float width)
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
                float rim = depth * 0.35f;
                float rimSigma = radius * 0.35f;
                float influence = radius * 1.8f; // past this the rim's gaussian is negligible

                int izMin = cz - influence < FrontStrip ? 0 : Mathf.FloorToInt((cz - influence) / zStep);
                int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((cz + influence) / zStep));
                int icx = Mathf.RoundToInt(cx / xStep);
                int ixSpan = Mathf.CeilToInt(influence / xStep);

                for (int iz = izMin; iz <= izMax; iz++)
                {
                    // Same front-strip flattening as the base field, so craters near the play
                    // line cut into it as a constant profile, not a sliver.
                    float dz = Mathf.Max(Depth * iz / (Res - 1), FrontStrip) - cz;

                    for (int j = icx - ixSpan; j <= icx + ixSpan; j++)
                    {
                        // Wrap into [0, Res-1); the duplicate last column is re-synced later.
                        int ix = ((j % (Res - 1)) + (Res - 1)) % (Res - 1);
                        float dx = j * xStep - cx;

                        float r = Mathf.Sqrt(dx * dx + dz * dz);
                        if (r > influence) continue;

                        float delta = rim * Mathf.Exp(-((r - radius) / rimSigma) * ((r - radius) / rimSigma));
                        if (r < radius)
                        {
                            float t = 1f - (r / radius) * (r / radius);
                            delta -= depth * t * t;
                        }
                        metres[iz, ix] += delta;
                    }
                }
            }
        }

        /// <summary>
        /// Mine craters: big pits scattered at random across the field. Same wrap-aware bowl+rim
        /// maths as <see cref="StampCraters"/>, but larger and with a widely varying per-crater
        /// depth (<see cref="MineDepthShallow"/>..<see cref="MineDepthDeep"/>), biased so most are
        /// deep while a random minority stay shallow.
        /// </summary>
        static void StampMineCraters(System.Random rng, float[,] metres, float width)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(width * MinesPerMetre));
            float xStep = width / (Res - 1);
            float zStep = Depth / (Res - 1);

            for (int c = 0; c < count; c++)
            {
                float cx = (float)rng.NextDouble() * width;
                float cz = Mathf.Lerp(10f, Depth - 40f, (float)rng.NextDouble());
                float radius = Mathf.Lerp(40f, 80f, (float)rng.NextDouble());

                // Depth varies a lot between mines. Square the roll so most land toward the deep
                // end, but a random handful stay shallow — 1 - u^2 keeps u near 0 (shallow) rare-ish
                // yet always possible.
                float u = (float)rng.NextDouble();
                float depthFrac = Mathf.Lerp(MineDepthShallow, MineDepthDeep, 1f - u * u);
                float depth = radius * depthFrac;
                float rim = depth * 0.45f;                // taller lip on the deeper pits
                float rimSigma = radius * 0.3f;
                float influence = radius * 1.7f;

                int izMin = cz - influence < FrontStrip ? 0 : Mathf.FloorToInt((cz - influence) / zStep);
                int izMax = Mathf.Min(Res - 1, Mathf.CeilToInt((cz + influence) / zStep));
                int icx = Mathf.RoundToInt(cx / xStep);
                int ixSpan = Mathf.CeilToInt(influence / xStep);

                for (int iz = izMin; iz <= izMax; iz++)
                {
                    // Same front-strip flattening as the base field.
                    float dz = Mathf.Max(Depth * iz / (Res - 1), FrontStrip) - cz;

                    for (int j = icx - ixSpan; j <= icx + ixSpan; j++)
                    {
                        int ix = ((j % (Res - 1)) + (Res - 1)) % (Res - 1);
                        float dx = j * xStep - cx;

                        float r = Mathf.Sqrt(dx * dx + dz * dz);
                        if (r > influence) continue;

                        float delta = rim * Mathf.Exp(-((r - radius) / rimSigma) * ((r - radius) / rimSigma));
                        if (r < radius)
                        {
                            float t = 1f - (r / radius) * (r / radius);
                            delta -= depth * t * t;
                        }
                        metres[iz, ix] += delta;
                    }
                }
            }
        }

        /// <summary>2D Perlin made periodic along X (period = width) by cross-fading two reads.</summary>
        static float TileableNoise(float x, float z, float frequency, float ox, float oz, float width)
        {
            float a = Mathf.PerlinNoise(x * frequency + ox, z * frequency + oz);
            float b = Mathf.PerlinNoise((x - width) * frequency + ox, z * frequency + oz);
            return Mathf.Lerp(a, b, x / width);
        }

        static float Offset(System.Random rng) => (float)(rng.NextDouble() * 1000.0 + 100.0);

        // ---------------------------------------------------------------- surface paint

        static void PaintTerrain(TerrainData data)
        {
            var layer = new TerrainLayer
            {
                diffuseTexture = SolidTexture(LandColor),
                tileSize = new Vector2(25f, 25f),
                tileOffset = Vector2.zero,
                metallic = 0f,
                smoothness = 0f,
            };
            data.terrainLayers = new[] { layer };

            // One layer everywhere; a tiny alphamap is enough.
            data.alphamapResolution = 64;
            var alpha = new float[64, 64, 1];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    alpha[y, x, 0] = 1f;
            data.SetAlphamaps(0, 0, alpha);
        }

        /// <summary>A 1x1 flat-colour texture, so the land paints as one plain colour (no noise).</summary>
        static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Land (flat colour)" };
            tex.SetPixel(0, 0, color);
            tex.Apply(false);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        // ---------------------------------------------------------------- cut wall

        /// <summary>
        /// A vertical ribbon along the front edge (z = 0): the top sits <see cref="WallSeamLift"/>
        /// above the terrain's surface line so it always overlaps the terrain's front edge (no
        /// see-through seam), the bottom is flat at <see cref="WallBottomY"/>, normals face the
        /// camera (-Z). Built in x = -width/2..width/2 local space so copies just shift along X.
        /// </summary>
        static Mesh BuildCutWallMesh(float[] cutLine, float width)
        {
            int cols = cutLine.Length;
            var verts = new Vector3[cols * 2];
            var normals = new Vector3[cols * 2];

            for (int i = 0; i < cols; i++)
            {
                float x = -width / 2f + width * i / (cols - 1);
                float top = cutLine[i] + WallSeamLift;   // overshoot the surface to cover the join

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
                tris[k] = t0; tris[k + 1] = t1; tris[k + 2] = b0; // clockwise seen from -Z
                tris[k + 3] = t1; tris[k + 4] = b1; tris[k + 5] = b0;
            }

            var mesh = new Mesh { name = "Cut Wall (generated)" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material CutWallMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", DirtColor);
            mat.SetFloat("_Smoothness", 0f);
            return mat;
        }
    }
}
