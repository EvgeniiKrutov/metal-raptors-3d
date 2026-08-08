using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class SeaSurface : MonoBehaviour
    {
        public const float Level = 22f;
        public const float NearEdge = 170f;

        const float HalfWidth = 1850f;
        const float ColStep = 65f;
        const float ShoreRowStep = 22f;
        const float ShoreRowEnd = 400f;
        const float NearRowStep = 45f;
        const float NearRowEnd = 760f;
        const float FarRowGrowth = 1.34f;
        const float FarZ = 1700f;

        const float CalmZ = 350f, SwellZ = 780f;
        const float CalmFactor = 0.05f;
        const float NormalBoost = 3.5f;

        struct Wave
        {
            public float dirX, dirZ, k, omega, amp;
        }

        static readonly Wave[] Waves = BuildWaves();

        Camera _cam;
        Mesh _mesh;
        Material _mat;

        float[] _colX;
        float[] _rowZ;
        Vector3[] _grid;
        Vector3[] _verts;
        Vector3[] _normals;
        Bounds _bounds;

        public static SeaSurface Begin(Camera cam, Daytime daytime)
        {
            if (cam == null) return null;

            var go = new GameObject("Sea", typeof(MeshFilter), typeof(MeshRenderer));
            var sea = go.AddComponent<SeaSurface>();
            sea._cam = cam;
            sea.Build(daytime);
            return sea;
        }

        static Wave[] BuildWaves()
        {
            return new[]
            {
                MakeWave(1f, 0.35f, 260f, 3.4f, 26f),
                MakeWave(0.7f, -0.7f, 150f, 1.9f, 19f),
                MakeWave(1f, 0.12f, 74f, 0.85f, 12f),
            };
        }

        static Wave MakeWave(float dirX, float dirZ, float length, float amp, float speed)
        {
            float inv = 1f / Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
            float k = 2f * Mathf.PI / length;
            return new Wave
            {
                dirX = dirX * inv,
                dirZ = dirZ * inv,
                k = k,
                omega = speed * k,
                amp = amp,
            };
        }

        void Build(Daytime daytime)
        {
            _colX = BuildColumns();
            _rowZ = BuildRows();

            int gx = _colX.Length, gz = _rowZ.Length;
            _grid = new Vector3[gx * gz];

            int quads = (gx - 1) * (gz - 1);
            _verts = new Vector3[quads * 6];
            _normals = new Vector3[quads * 6];

            var tris = new int[quads * 6];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;

            float amplitude = 0f;
            foreach (var wave in Waves) amplitude += wave.amp;

            float near = _rowZ[0], far = _rowZ[gz - 1];
            float wide = _colX[gx - 1];
            _bounds = new Bounds(new Vector3(0f, 0f, (near + far) * 0.5f),
                new Vector3(wide * 2.2f, amplitude * 4f, (far - near) * 1.2f));

            transform.position = new Vector3(0f, Level, 0f);

            _mesh = new Mesh { name = "Sea (generated)" };
            _mesh.indexFormat = _verts.Length > 65000
                ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.MarkDynamic();

            Refresh(0f, 0f);
            _mesh.SetTriangles(tris, 0, false);
            _mesh.bounds = _bounds;

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _mat = BuildMaterial(daytime);
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static float[] BuildColumns()
        {
            int cols = Mathf.CeilToInt(HalfWidth * 2f / ColStep);
            float half = cols * ColStep * 0.5f;

            var x = new float[cols + 1];
            for (int i = 0; i <= cols; i++) x[i] = -half + i * ColStep;
            return x;
        }

        static float[] BuildRows()
        {
            var rows = new List<float> { NearEdge };

            float z = NearEdge;
            while (z < ShoreRowEnd)
            {
                z += ShoreRowStep;
                rows.Add(z);
            }

            while (z < NearRowEnd)
            {
                z += NearRowStep;
                rows.Add(z);
            }

            float step = NearRowStep;
            while (z < FarZ)
            {
                step *= FarRowGrowth;
                z += step;
                rows.Add(z);
            }
            return rows.ToArray();
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            float rootX = Mathf.Floor(_cam.transform.position.x / ColStep) * ColStep;
            transform.position = new Vector3(rootX, Level, 0f);
            Refresh(rootX, Time.time);
        }

        void Refresh(float rootX, float time)
        {
            int gx = _colX.Length, gz = _rowZ.Length;

            for (int j = 0; j < gz; j++)
            {
                float z = _rowZ[j];
                float swell = Swell(z);
                int row = j * gx;

                for (int i = 0; i < gx; i++)
                {
                    float x = _colX[i];
                    _grid[row + i] = new Vector3(x, WaveAt(rootX + x, z, time) * swell, z);
                }
            }

            int v = 0;
            for (int j = 0; j < gz - 1; j++)
            {
                int a = j * gx, b = (j + 1) * gx;

                for (int i = 0; i < gx - 1; i++)
                {
                    Vector3 p00 = _grid[a + i];
                    Vector3 p01 = _grid[b + i];
                    Vector3 p10 = _grid[a + i + 1];
                    Vector3 p11 = _grid[b + i + 1];

                    Vector3 n0 = FaceNormal(p00, p01, p10);
                    _verts[v] = p00; _normals[v++] = n0;
                    _verts[v] = p01; _normals[v++] = n0;
                    _verts[v] = p10; _normals[v++] = n0;

                    Vector3 n1 = FaceNormal(p01, p11, p10);
                    _verts[v] = p01; _normals[v++] = n1;
                    _verts[v] = p11; _normals[v++] = n1;
                    _verts[v] = p10; _normals[v++] = n1;
                }
            }

            _mesh.SetVertices(_verts);
            _mesh.SetNormals(_normals);
            _mesh.bounds = _bounds;
        }

        static float Swell(float z) => Mathf.Lerp(CalmFactor, 1f,
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CalmZ, SwellZ, z)));

        static float WaveAt(float x, float z, float time)
        {
            float h = 0f;
            for (int i = 0; i < Waves.Length; i++)
            {
                Wave w = Waves[i];
                h += w.amp * Mathf.Sin(w.k * (w.dirX * x + w.dirZ * z) - w.omega * time);
            }
            return h;
        }

        static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 u = b - a; u.y *= NormalBoost;
            Vector3 w = c - a; w.y *= NormalBoost;

            Vector3 n = Vector3.Cross(u, w);
            if (n.y < 0f) n = -n;
            return n.normalized;
        }

        static Material BuildMaterial(Daytime daytime)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "Sea (runtime)",
            };
            mat.SetColor("_BaseColor", CoastSky.SeaColor(daytime));
            mat.SetFloat("_Smoothness", 0.18f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return mat;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_mat != null) Destroy(_mat);
        }
    }
}
