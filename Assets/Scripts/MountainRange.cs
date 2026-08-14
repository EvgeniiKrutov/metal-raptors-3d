using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class MountainRange : MonoBehaviour
    {
        public const float CellWidth = CampaignTerrain.ChunkLength;

        const float ColStep = CellWidth / 12f;
        const float SkirtY = -60f;
        const float GradientLow = 20f, GradientHigh = 900f;
        const int GradientRes = 96;
        const float GreenTop = 0.06f, RockFrom = 0.14f;
        const float PeakFrom = 0.57f, PeakFull = 0.91f;

        struct Ridge
        {
            public float frontZ, crestZ, crestJitter;
            public float minCrest, maxCrest;
            public float wavelength;
            public int salt;
        }

        static readonly Ridge[] Ridges =
        {
            new Ridge
            {
                frontZ = 700f, crestZ = 980f, crestJitter = 90f,
                minCrest = 360f, maxCrest = 650f, wavelength = 900f, salt = 1,
            },
            new Ridge
            {
                frontZ = 950f, crestZ = 1180f, crestJitter = 110f,
                minCrest = 480f, maxCrest = 760f, wavelength = 1350f, salt = 2,
            },
            new Ridge
            {
                frontZ = 1180f, crestZ = 1450f, crestJitter = 130f,
                minCrest = 620f, maxCrest = 900f, wavelength = 1900f, salt = 3,
            },
        };

        public static MountainRange Current { get; private set; }

        Camera _cam;
        float _keep;
        Material _mat;

        class Cell
        {
            public GameObject root;
            public Mesh[] meshes;
        }

        readonly float[,] _phase = new float[Ridges.Length, 4];
        readonly Dictionary<int, Cell> _cells = new Dictionary<int, Cell>();
        readonly List<int> _removeScratch = new List<int>();

        public static MountainRange Begin(Camera cam, int seed, Daytime daytime)
        {
            if (cam == null) return null;

            var range = new GameObject("Mountains").AddComponent<MountainRange>();
            range._cam = cam;
            range._mat = RockMaterial(daytime);

            var rng = new System.Random(seed ^ 0x5EA1);
            for (int layer = 0; layer < Ridges.Length; layer++)
                for (int octave = 0; octave < 4; octave++)
                    range._phase[layer, octave] = (float)(rng.NextDouble() * 1000.0 + 100.0);

            float depth = Ridges[Ridges.Length - 1].crestZ - cam.transform.position.z;
            range._keep = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect
                          + CellWidth;

            range.UpdateCells(cam.transform.position.x);

            Current = range;
            return range;
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            UpdateCells(_cam.transform.position.x);
        }

        void UpdateCells(float camX)
        {
            int first = Mathf.FloorToInt((camX - _keep) / CellWidth);
            int last = Mathf.FloorToInt((camX + _keep) / CellWidth);

            _removeScratch.Clear();
            foreach (var kv in _cells)
                if (kv.Key < first || kv.Key > last) _removeScratch.Add(kv.Key);

            foreach (int cell in _removeScratch)
            {
                Dispose(_cells[cell]);
                _cells.Remove(cell);
            }

            for (int cell = first; cell <= last; cell++)
                if (!_cells.ContainsKey(cell)) _cells[cell] = BuildCell(cell);
        }

        Cell BuildCell(int cell)
        {
            var root = new GameObject($"Ridges {cell}");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(cell * CellWidth, 0f, 0f);

            var meshes = new Mesh[Ridges.Length];
            for (int layer = 0; layer < Ridges.Length; layer++)
            {
                var go = new GameObject($"Ridge {layer}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);

                meshes[layer] = BuildRidge(cell, layer);
                go.GetComponent<MeshFilter>().sharedMesh = meshes[layer];

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = _mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return new Cell { root = root, meshes = meshes };
        }

        void Dispose(Cell cell)
        {
            Destroy(cell.root);
            foreach (var mesh in cell.meshes) Destroy(mesh);
        }

        Mesh BuildRidge(int cell, int layer)
        {
            Ridge r = Ridges[layer];
            int cols = Mathf.RoundToInt(CellWidth / ColStep);
            float x0 = cell * CellWidth;

            var top = new Vector3[cols + 1];
            for (int i = 0; i <= cols; i++)
            {
                float localX = i * ColStep;
                float x = x0 + localX;
                top[i] = new Vector3(localX, Crest(layer, x), CrestZ(layer, x));
            }

            var verts = new Vector3[cols * 6];
            var normals = new Vector3[cols * 6];
            var uvs = new Vector2[cols * 6];
            var tris = new int[cols * 6];

            for (int i = 0; i < cols; i++)
            {
                var baseLeft = new Vector3(i * ColStep, SkirtY, r.frontZ);
                var baseRight = new Vector3((i + 1) * ColStep, SkirtY, r.frontZ);
                Vector3 topLeft = top[i];
                Vector3 topRight = top[i + 1];

                int v = i * 6;
                Write(verts, normals, uvs, v, baseLeft, topLeft, topRight);
                Write(verts, normals, uvs, v + 3, baseLeft, topRight, baseRight);

                for (int k = 0; k < 6; k++) tris[v + k] = v + k;
            }

            var mesh = new Mesh { name = $"Ridge {layer} (generated)" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Write(Vector3[] verts, Vector3[] normals, Vector2[] uvs, int at,
            Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.z > 0f) normal = -normal;

            verts[at] = a; verts[at + 1] = b; verts[at + 2] = c;
            normals[at] = normal; normals[at + 1] = normal; normals[at + 2] = normal;
            uvs[at] = Band(a.y); uvs[at + 1] = Band(b.y); uvs[at + 2] = Band(c.y);
        }

        static Vector2 Band(float y) =>
            new Vector2(0.5f, Mathf.Clamp01(Mathf.InverseLerp(GradientLow, GradientHigh, y)));

        float Crest(int layer, float x)
        {
            Ridge r = Ridges[layer];
            float ridge = Ridged(x / r.wavelength + _phase[layer, 0]) * 0.58f
                + Ridged(x / (r.wavelength * 0.38f) + _phase[layer, 1]) * 0.28f
                + Ridged(x / (r.wavelength * 0.22f) + _phase[layer, 2]) * 0.14f;

            ridge = Mathf.Clamp01(ridge);
            return Mathf.Lerp(r.minCrest, r.maxCrest, ridge * ridge);
        }

        static float Ridged(float t) => 1f - Mathf.Abs(Mathf.PerlinNoise(t, 0.5f) * 2f - 1f);

        float CrestZ(int layer, float x)
        {
            Ridge r = Ridges[layer];
            float wander = Mathf.PerlinNoise(x / 620f + _phase[layer, 3], 0.5f) - 0.5f;
            return r.crestZ + wander * 2f * r.crestJitter;
        }

        static Material RockMaterial(Daytime daytime)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "Mountain rock (runtime)",
            };
            mat.SetTexture("_BaseMap", RockGradient(daytime));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return mat;
        }

        static Texture2D RockGradient(Daytime daytime)
        {
            Color slope = DolomitesSky.MountainSlope(daytime);
            Color rock = DolomitesSky.MountainRock(daytime);
            Color peak = DolomitesSky.MountainPeak(daytime);

            var tex = new Texture2D(1, GradientRes, TextureFormat.RGBA32, false)
            {
                name = "Mountain band (generated)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[GradientRes];
            for (int row = 0; row < GradientRes; row++)
            {
                float t = (float)row / (GradientRes - 1);

                Color band = Color.Lerp(slope, rock, Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(GreenTop, RockFrom, t)));
                pixels[row] = Color.Lerp(band, peak, Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(PeakFrom, PeakFull, t)));
            }

            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        void OnDestroy()
        {
            foreach (var kv in _cells) Dispose(kv.Value);
            _cells.Clear();
            if (Current == this) Current = null;
        }
    }
}
