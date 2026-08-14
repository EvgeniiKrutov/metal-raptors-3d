using UnityEngine;

namespace MetalRaptors
{
    public static class TerrainSurfaces
    {
        const int GrainRes = 128;
        const int BlotchCells = 6, GritCells = 12;
        const float BlotchDepth = 0.10f, GritDepth = 0.045f;
        const float Roughness = 0f;

        public static TerrainLayer GrainLayer(string name, Color color, float tile, int seed)
        {
            var tex = new Texture2D(GrainRes, GrainRes, TextureFormat.RGBA32, true)
            {
                name = name,
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

        public static TerrainLayer FlatLayer(string name, Color color, float tile)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
            };
            tex.SetPixel(0, 0, new Color(color.r, color.g, color.b, 0f));
            tex.Apply(false);

            return NewLayer(tex, tile);
        }

        public static Material FlatMaterial(Color color)
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
    }
}
