using System.Collections.Generic;
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
        public const float Depth = 800f;       // how far the land runs toward the horizon (+Z);
                                               // deep enough that the fog (below) saturates long
                                               // before the far edge, so the edge is never seen
        internal const float HeightScale = 90f; // terrain-data vertical range (heights are 0..1 of this)
        public const float BaseLevel = 30f;    // mean ground height the hills undulate around;
                                               // SkyHorizon uses it as the far edge's horizon height
        internal const float MinHeight = 4f;   // never carve below this (keeps dirt above the wall's UVs)
        public const float MaxHeight = 85f;    // never build above this (enemy AI treats this
                                               // as "the ground" so its margins hold over hills)
        internal const float FrontStrip = 130f; // near z-band kept height-constant: the play plane sits
                                               // back from z=0 (see LevelController.PlayPlaneZ). Keep this
                                               // past the cube's far Z so it flies/lands over a flat front
                                               // terrace — never behind an intervening ridge — and so the
                                               // cut wall's raised top hides against flat dirt (below)
        const int Res = 1025;                  // heightmap resolution (square)

        // ---- Craters ----
        internal const float CratersPerMetre = 0.017f; // ~34 craters across 2000 m ("moderate")

        // ---- Mine craters ----
        // Big pits blown from a gallery driven under the line. Placed at random across the field,
        // with depth varying widely per-crater: most are deep, but a random minority stay shallow.
        internal const float MinesPerMetre = 0.0035f;  // ~3 per 800 m
        internal const float MineDepthShallow = 0.20f; // depth-to-radius ratio for the shallowest mines
        internal const float MineDepthDeep = 0.62f;    // ...and for the deepest

        // ---- What the camera may reveal ----
        public const float CutRevealY = -80f;  // lowest world Y the camera should ever show
        internal const float WallBottomY = -120f; // the cut wall reaches safely below that
        internal const float WallSeamLift = 3f; // the cut wall's top overshoots the terrain surface by this
                                               // much, so it always covers the terrain's front edge and
                                               // leaves no see-through seam despite terrain LOD; the extra
                                               // sliver is hidden against the flat dirt terrace behind it

        // ---- Grass ----
        // Grass blankets the whole field except the shell/mine craters, whose bowls and
        // blown-earth rims stay bare mud. Tuft positions come from Poisson-disk (Bridson)
        // sampling — evenly spaced, no clumping or holes, X-wrapped so the pattern tiles with
        // the land. Blades are tinted close to the land's own earth-brown so the sward reads as
        // one colour with the ground, and a weak breeze waves them.
        const int GrassDetailRes = 1024;         // detail-map resolution (cells ~1.5 x 0.8 m, fine
                                                 // enough that the Poisson spacing survives the
                                                 // snap of each point into its cell)
        internal const int GrassDetailPatch = 32;    // cells per cull patch
        internal const float GrassSpacing = 4.5f;    // Poisson minimum distance between tufts; with
                                                     // 3-6 m wide tufts this reads as a dense sward
        internal const int GrassPoissonTries = 20;   // Bridson candidate attempts per active point
        internal const float GrassMaxSlopeDeg = 30f; // no grass on walls steeper than this
        internal const float CraterBareRadii = 1.35f; // a crater kills grass out to this many of its
                                                     // radii: the bowl plus its blown-earth rim
        internal const float GrassViewDistance = 800f; // detail draw distance; the default 80 m would
                                                     // cull everything (the camera sits ~320 m from
                                                     // the nearest land, see LevelController.CamZ)
        // ---- Palette (plain flat colours — no generated textures) ----
        // The mist colour lives in the active sky's HazeColor (MorningSky or MiddaySky, per the
        // level's Daytime): fog and the skybox's horizon band must be the same value for the
        // land to dissolve seamlessly into the sky.
        static readonly Color LandColor = new Color(0.44f, 0.36f, 0.26f); // flat surface earth
        static readonly Color DirtColor = new Color(0.36f, 0.28f, 0.20f); // flat cross-section dirt
        // Blades tinted close to LandColor so the grass reads as one earth-brown with the ground;
        // the two ends give the detail system a little healthy/dry variation without leaving brown.
        static readonly Color GrassHealthy = new Color(0.47f, 0.39f, 0.27f); // just above the land
        static readonly Color GrassDry = new Color(0.40f, 0.32f, 0.22f);     // just below it

        /// <summary>
        /// Builds the whole land into the active scene. <paramref name="width"/> is the level's
        /// playable width (the terrain spans x = -width/2 .. +width/2, plus one copy per side).
        /// <paramref name="cameraDistance"/> is how far back on -Z the gameplay camera sits from the
        /// play line, so the fog can start just behind it. <paramref name="playPlaneZ"/> is how far
        /// the play line sits into the land (+Z): the terrain's front edge is that much closer to the
        /// camera, so the fog's far end is anchored to the real far-edge distance.
        /// <paramref name="daytime"/> picks the sky whose haze the fog must match, and with it
        /// how thick the air is. <paramref name="weather"/> is the seam where weather will
        /// modulate the land's atmosphere (fog distances, grass wind);
        /// <see cref="Weather.Calm"/> is the identity and changes nothing.
        /// </summary>
        public static void Build(int seed, float width, float cameraDistance, float playPlaneZ,
            Daytime daytime, Weather weather)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Battlefield Land");

            float[,] heights01 = GenerateHeights(rng, width, out float[] cutLine, out List<Vector3> craters);

            var data = new TerrainData();
            data.heightmapResolution = Res;
            data.size = new Vector3(width, HeightScale, Depth); // set after resolution (resolution resets size)
            data.SetHeights(0, 0, heights01);
            PaintTerrain(data);
            PlantGrass(rng, data, width, craters);

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
                terrain.detailObjectDistance = GrassViewDistance;
                terrain.detailObjectDensity = 1f;

                var wGo = new GameObject($"Cut Wall (tile {tile})", typeof(MeshFilter), typeof(MeshRenderer));
                wGo.transform.SetParent(root.transform);
                wGo.transform.position = new Vector3(tile * width, 0f, 0f);
                wGo.GetComponent<MeshFilter>().sharedMesh = wallMesh;
                var mr = wGo.GetComponent<MeshRenderer>();
                mr.sharedMaterial = wallMat;
                mr.shadowCastingMode = ShadowCastingMode.Off; // a cross-section shouldn't shade the land
            }

            // Distance haze: clear at the play line, total well before the terrain's far edge —
            // the last ~250 m of land sit in solid haze, so even from a high camera (whose slant
            // distances shrink the fogged margin) the edge of the map never shows. The far edge
            // is at (front-edge distance) + Depth = (cameraDistance - playPlaneZ) + Depth, and
            // the colour must be the active sky's HazeColor (its horizon band) or the seam shows.
            // Per-daytime air thickness (fog start) is documented in docs/atmospheres.md; ambient
            // and the matching sky are applied per Daytime in LevelController.SetupCamera.
            ApplyFog(daytime, cameraDistance, playPlaneZ);
        }

        /// <summary>The camera distance at which the linear fog saturates; past it the land is
        /// pure haze (the campaign streamer uses this as its visibility horizon).</summary>
        internal static float FogEndDistance(float cameraDistance, float playPlaneZ)
            => cameraDistance - playPlaneZ + Depth - 250f;

        /// <summary>Per-daytime distance haze shared by the fixed land and the campaign's
        /// streamed chunks (see the comment above and docs/atmospheres.md).</summary>
        internal static void ApplyFog(Daytime daytime, float cameraDistance, float playPlaneZ)
        {
            Color haze;
            float startOffset;
            switch (daytime)
            {
                case Daytime.Midday: haze = MiddaySky.HazeColor; startOffset = 300f; break;
                case Daytime.Evening: haze = EveningSky.HazeColor; startOffset = 100f; break;
                case Daytime.Night: haze = NightSky.HazeColor; startOffset = 250f; break;
                default: haze = MorningSky.HazeColor; startOffset = 80f; break;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = cameraDistance + startOffset;
            RenderSettings.fogEndDistance = FogEndDistance(cameraDistance, playPlaneZ);
        }

        // ---------------------------------------------------------------- height field

        /// <summary>
        /// Fills the normalized heightmap and returns the front-edge surface line (metres, one
        /// entry per X sample) for the cut wall, plus every stamped crater as (x, z, bare radius)
        /// so the grass keeps out of the bowls. heights[iz, ix]: iz runs along Z, ix along X.
        /// </summary>
        static float[,] GenerateHeights(System.Random rng, float width,
            out float[] cutLine, out List<Vector3> craters)
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

            craters = new List<Vector3>();
            StampCraters(rng, metres, width, craters);
            StampMineCraters(rng, metres, width, craters);

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
                        metres[iz, ix] += CraterDelta(r, radius, depth, rim, rimSigma);
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
                        metres[iz, ix] += CraterDelta(r, radius, depth, rim, rimSigma);
                    }
                }
            }
        }

        /// <summary>The bowl-plus-raised-rim height change of one crater at radial distance
        /// <paramref name="r"/> from its centre. Shared with the campaign's streamed chunks.</summary>
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

        /// <summary>2D Perlin made periodic along X (period = width) by cross-fading two reads.</summary>
        static float TileableNoise(float x, float z, float frequency, float ox, float oz, float width)
        {
            float a = Mathf.PerlinNoise(x * frequency + ox, z * frequency + oz);
            float b = Mathf.PerlinNoise((x - width) * frequency + ox, z * frequency + oz);
            return Mathf.Lerp(a, b, x / width);
        }

        static float Offset(System.Random rng) => (float)(rng.NextDouble() * 1000.0 + 100.0);

        // ---------------------------------------------------------------- surface paint

        static void PaintTerrain(TerrainData data) => PaintTerrain(data, CreateLandLayer());

        /// <summary>The one flat-earth layer. Campaign chunks share a single instance so the
        /// streamed land never allocates textures per chunk.</summary>
        internal static TerrainLayer CreateLandLayer()
        {
            return new TerrainLayer
            {
                diffuseTexture = SolidTexture(LandColor),
                tileSize = new Vector2(25f, 25f),
                tileOffset = Vector2.zero,
                metallic = 0f,
                smoothness = 0f,
                // Dead-matte earth. Without this, terrain shaders take smoothness from the
                // diffuse texture's alpha channel, and a solid texture's alpha of 1 read as
                // full gloss — the land sheened with reflected sky at grazing angles.
                // ConstantOnly makes the 0 above the whole story; SolidTexture also bakes
                // alpha 0 so the distance basemap can't resurrect the shine.
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly,
            };
        }

        internal static void PaintTerrain(TerrainData data, TerrainLayer layer)
        {
            data.terrainLayers = new[] { layer };

            // One layer everywhere; a tiny alphamap is enough.
            data.alphamapResolution = 64;
            var alpha = new float[64, 64, 1];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    alpha[y, x, 0] = 1f;
            data.SetAlphamaps(0, 0, alpha);
        }

        /// <summary>
        /// A 1x1 flat-colour texture, so the land paints as one plain colour (no noise).
        /// Alpha is forced to 0: terrain shaders read a diffuse texture's alpha as smoothness,
        /// and the land must stay matte (see the layer's smoothnessSource in PaintTerrain).
        /// </summary>
        static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Land (flat colour)" };
            tex.SetPixel(0, 0, new Color(color.r, color.g, color.b, 0f));
            tex.Apply(false);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        // ---------------------------------------------------------------- grass

        /// <summary>
        /// Blankets the field with grass tufts using the terrain's built-in detail system
        /// (billboard blades, so the waving-breeze wind and distance culling come for free).
        /// Tuft positions are Poisson-disk sampled — evenly spaced with no clumps or holes, on
        /// an X-wrapped domain so the pattern tiles seamlessly across the side copies. Only the
        /// stamped craters (bowl plus blown-earth rim) and sheer walls stay bare. The blades are
        /// one earth-brown tint close to the land's own colour, so the sward blends into the
        /// ground rather than standing out. Each point is snapped into its detail cell (the
        /// system re-jitters it inside that ~1.5 m cell, small against the 4.5 m spacing, so the
        /// even look survives). The blade texture is painted in code from the same rng: no
        /// assets, same grass every run for a given seed.
        /// </summary>
        /// <summary>The grass tuft billboard description; the texture is shared so campaign
        /// chunks don't repaint it per chunk.</summary>
        internal static DetailPrototype CreateGrassPrototype(Texture2D bladesTexture)
        {
            return new DetailPrototype
            {
                prototypeTexture = bladesTexture,
                renderMode = DetailRenderMode.GrassBillboard,
                usePrototypeMesh = false,
                minWidth = 3f, maxWidth = 6f,   // world is ~6x life size (the planes span 60 m),
                minHeight = 3f, maxHeight = 7f, // so knee-high tufts land at a few metres
                noiseSpread = 0.15f,
                healthyColor = GrassHealthy,
                dryColor = GrassDry,
            };
        }

        /// <summary>Wires the detail system and the weak breeze onto <paramref name="data"/>.</summary>
        internal static void SetupGrassDetail(TerrainData data, DetailPrototype prototype, int detailRes)
        {
            data.detailPrototypes = new[] { prototype };
            data.SetDetailResolution(detailRes, GrassDetailPatch);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode); // cell value = tuft count

            // Weak breeze; near-white tint so the waving pass keeps the blades' brown.
            data.wavingGrassTint = new Color(0.95f, 0.93f, 0.85f);
            data.wavingGrassStrength = 0.22f;
            data.wavingGrassAmount = 0.25f;
            data.wavingGrassSpeed = 0.3f;
        }

        static void PlantGrass(System.Random rng, TerrainData data, float width, List<Vector3> craters)
        {
            SetupGrassDetail(data, CreateGrassPrototype(GrassBladesTexture(rng)), GrassDetailRes);

            var layer = new int[GrassDetailRes, GrassDetailRes]; // [z, x], like the heightmap
            foreach (var p in PoissonDiskPoints(rng, width, Depth, GrassSpacing, GrassPoissonTries))
            {
                float xNorm = p.x / width, zNorm = p.y / Depth;

                if (InCrater(p, craters, width)) continue;                        // bowls/rims stay mud
                if (data.GetSteepness(xNorm, zNorm) > GrassMaxSlopeDeg) continue; // and sheer walls

                int ix = Mathf.Min(GrassDetailRes - 1, (int)(xNorm * GrassDetailRes));
                int iz = Mathf.Min(GrassDetailRes - 1, (int)(zNorm * GrassDetailRes));
                layer[iz, ix]++;
            }
            data.SetDetailLayer(0, 0, 0, layer);
        }

        /// <summary>
        /// Is the point inside any crater's bare zone? Distances use the same rules as the
        /// crater stamps themselves: X wraps with the land's tiling, and Z inside the front
        /// strip is flattened (a crater near the play line smears across the whole strip, so
        /// its bald patch must smear identically or it would miss the visible bowl).
        /// </summary>
        static bool InCrater(Vector2 p, List<Vector3> craters, float width)
        {
            float zEff = Mathf.Max(p.y, FrontStrip);
            foreach (var c in craters)
            {
                float dx = Mathf.Abs(c.x - p.x);
                dx = Mathf.Min(dx, width - dx); // wrapped along X
                float dz = zEff - c.y;
                if (dx * dx + dz * dz < c.z * c.z) return true;
            }
            return false;
        }

        /// <summary>
        /// Bridson's Poisson-disk sampling over a width x depth rectangle: points at least
        /// <paramref name="radius"/> apart but as dense as that allows — even spacing with no
        /// clumps or holes, unlike plain Random.Range scatter. The X axis wraps (distances are
        /// measured around the seam), so the point set tiles with the land's side copies.
        /// </summary>
        static List<Vector2> PoissonDiskPoints(
            System.Random rng, float width, float depth, float radius, int tries)
        {
            // Background grid with cells small enough (diagonal = radius) to hold at most one
            // point each, so a fit test only scans the 5x5 neighbourhood.
            float cell = radius / Mathf.Sqrt(2f);
            int nx = Mathf.CeilToInt(width / cell);
            int nz = Mathf.CeilToInt(depth / cell);
            var grid = new int[nz, nx];          // 1-based point index; 0 = empty
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
                        ddx = Mathf.Min(ddx, width - ddx); // wrapped along X
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
                    float dist = radius * (1f + (float)rng.NextDouble()); // the r..2r annulus
                    var p = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                    p.x = Mathf.Repeat(p.x, width);
                    if (p.x >= width) p.x = 0f; // Mathf.Repeat can graze the end at float edges
                    if (p.y < 0f || p.y >= depth || !Fits(p)) continue;

                    Add(p);
                    placed = true;
                    break;
                }

                if (!placed) // no room left around this point: retire it (swap-remove, O(1))
                {
                    active[ai] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                }
            }
            return points;
        }

        /// <summary>
        /// Paints a small transparent texture of a few tapering, bending grass blades for the
        /// detail billboards. Zero-alpha texels still carry the blade colour so bilinear
        /// filtering and mips never bleed dark fringes around the blades.
        /// </summary>
        internal static Texture2D GrassBladesTexture(System.Random rng)
        {
            const int W = 64, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true)
            {
                name = "Grass blades (generated)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            // Blades are painted near-white: the detail system multiplies the texture by the
            // healthy/dry tint, so keeping the texture neutral lets that earth-brown tint set the
            // final colour instead of skewing it. The slight per-blade wobble is only luminance.
            var pixels = new Color[W * H];
            var bg = new Color(0.9f, 0.9f, 0.9f, 0f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            const int Blades = 9;
            for (int b = 0; b < Blades; b++)
            {
                float baseX = Mathf.Lerp(5f, W - 5f, (float)rng.NextDouble());
                float lean = ((float)rng.NextDouble() - 0.5f) * 36f; // sideways bend at the tip, px
                float height = Mathf.Lerp(H * 0.55f, H * 0.95f, (float)rng.NextDouble());
                float shade = Mathf.Lerp(0.82f, 1f, (float)rng.NextDouble()); // per-blade luminance
                var tone = new Color(shade, shade, shade);

                for (int y = 0; y < (int)height; y++)
                {
                    float t = y / height;
                    float centre = baseX + lean * t * t;            // bends ever harder toward the tip
                    float half = Mathf.Lerp(2.4f, 0.5f, t);         // tapers base -> tip
                    Color c = tone * Mathf.Lerp(0.7f, 1f, t);       // shaded in the sward, lit at the tip
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

        // ---------------------------------------------------------------- cut wall

        /// <summary>
        /// A vertical ribbon along the front edge (z = 0): the top sits <see cref="WallSeamLift"/>
        /// above the terrain's surface line so it always overlaps the terrain's front edge (no
        /// see-through seam), the bottom is flat at <see cref="WallBottomY"/>, normals face the
        /// camera (-Z). Built in x = -width/2..width/2 local space so copies just shift along X.
        /// </summary>
        internal static Mesh BuildCutWallMesh(float[] cutLine, float width)
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

        internal static Material CutWallMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", DirtColor);
            mat.SetFloat("_Smoothness", 0f);
            // Bare dirt: no specular glint or sky reflection under any light angle.
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return mat;
        }
    }
}
