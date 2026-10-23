using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class BoatWake
    {
        const int TextureLength = 128, TextureWidth = 32;
        const int StreakSeed = 1918;
        const int StreakWaves = 4;

        const int WashSegments = 20, ArmSegments = 24;
        const float WashTuck = 0.06f, WashLength = 2.6f;
        const float WashHalfStart = 0.5f, WashHalfEnd = 1.7f;
        const float ArmAhead = 0.18f, ArmLength = 3.6f, ArmAngle = 13f;
        const float ArmHalfStart = 0.12f, ArmHalfEnd = 0.9f;
        const float WashOpacity = 0.9f, ArmOpacity = 0.6f;

        const float Lift = 0.8f, SwellLift = 2.5f;
        const float FadeIn = 0.04f, FadePower = 1.6f, EdgePower = 0.8f;
        const float StreakDepth = 0.45f;
        const float BoundsHeight = 40f;

        static readonly Color FoamColor = new Color(0.9f, 0.93f, 0.93f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        static Texture2D _texture;
        static Material _washMaterial, _armMaterial;

        readonly GameObject _go;
        readonly Mesh _mesh;
        readonly Vector3[] _flat;
        readonly Vector3[] _verts;

        public float Reach { get; }

        public BoatWake(Transform parent, float length, float beam, float bowAhead = ArmAhead,
            float run = 1f, float strength = 1f)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var wash = new List<int>();
            var arms = new List<int>();

            float washStart = length * (0.5f - WashTuck);
            float washRun = length * WashLength * run;
            Strip(verts, uvs, wash, WashSegments,
                t => new Vector3(washStart + t * washRun, 0f, 0f),
                t => beam * Mathf.Lerp(WashHalfStart, WashHalfEnd, Mathf.Sqrt(t)));

            float angle = ArmAngle * Mathf.Deg2Rad;
            float armRun = length * ArmLength * run;
            float armStart = -length * bowAhead;
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                Strip(verts, uvs, arms, ArmSegments,
                    t => new Vector3(armStart + t * armRun * Mathf.Cos(angle), 0f,
                        side * (beam * 0.5f + t * armRun * Mathf.Sin(angle))),
                    t => beam * Mathf.Lerp(ArmHalfStart, ArmHalfEnd, Mathf.Sqrt(t)));
            }

            _flat = verts.ToArray();
            _verts = verts.ToArray();

            var normals = new Vector3[_flat.Length];
            var bounds = new Bounds(_flat[0], Vector3.zero);
            for (int i = 0; i < _flat.Length; i++)
            {
                normals[i] = Vector3.up;
                bounds.Encapsulate(_flat[i]);
            }
            bounds.Expand(new Vector3(0f, BoundsHeight, 0f));
            Reach = bounds.max.x;

            _mesh = new Mesh { name = "Boat Wake (generated)" };
            _mesh.MarkDynamic();
            _mesh.SetVertices(_verts);
            _mesh.SetNormals(normals);
            _mesh.SetUVs(0, uvs);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(wash, 0, false);
            _mesh.SetTriangles(arms, 1, false);
            _mesh.bounds = bounds;

            _go = new GameObject("Boat Wake", typeof(MeshFilter), typeof(MeshRenderer));
            _go.transform.SetParent(parent, false);
            _go.GetComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = _go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                FoamMaterial(ref _washMaterial, WashOpacity, "Boat Wash"),
                FoamMaterial(ref _armMaterial, ArmOpacity, "Boat Wake Arm"),
            };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            if (strength < 1f) Soften(renderer, strength);
        }

        static void Soften(Renderer renderer, float strength)
        {
            var block = new MaterialPropertyBlock();
            Color color = FoamColor;
            color.a = WashOpacity * strength;
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block, 0);

            color.a = ArmOpacity * strength;
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block, 1);
        }

        public void Follow(Vector3 centre, float time)
        {
            if (_go == null) return;

            _go.transform.position = new Vector3(centre.x, SeaSurface.Level, centre.z);
            for (int i = 0; i < _flat.Length; i++)
            {
                Vector3 p = _flat[i];
                float x = centre.x + p.x, z = centre.z + p.z;
                p.y = SeaSurface.HeightAt(x, z, time) - SeaSurface.Level
                      + Lift + SwellLift * SeaSurface.Swell(z);
                _verts[i] = p;
            }
            _mesh.SetVertices(_verts);
        }

        public void Release()
        {
            if (_go != null) Object.Destroy(_go);
            if (_mesh != null) Object.Destroy(_mesh);
        }

        static void Strip(List<Vector3> verts, List<Vector2> uvs, List<int> tris, int segments,
            System.Func<float, Vector3> centre, System.Func<float, float> half)
        {
            int start = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 c = centre(t);
                float h = half(t);
                verts.Add(c + new Vector3(0f, 0f, -h));
                uvs.Add(new Vector2(t, 0f));
                verts.Add(c + new Vector3(0f, 0f, h));
                uvs.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
                tris.Add(a + 1); tris.Add(a + 3); tris.Add(a + 2);
            }
        }

        static Material FoamMaterial(ref Material cached, float opacity, string name)
        {
            if (cached != null) return cached;

            var shader = NightReceivers.Lit;
            if (shader == null) return null;

            Color color = FoamColor;
            color.a = opacity;

            cached = new Material(shader) { name = name };
            cached.SetTexture(BaseMapId, FoamTexture());
            cached.SetColor(BaseColorId, color);
            cached.SetFloat("_Smoothness", 0.1f);
            UIFactory.MakeTransparent(cached);
            return cached;
        }

        static Texture2D FoamTexture()
        {
            if (_texture != null) return _texture;

            var rng = new System.Random(StreakSeed);
            var rates = new float[StreakWaves];
            var phases = new float[StreakWaves];
            for (int k = 0; k < StreakWaves; k++)
            {
                rates[k] = (float)(Mathf.PI * (3 + k * 4 + rng.NextDouble() * 3));
                phases[k] = (float)(rng.NextDouble() * Mathf.PI * 2f);
            }

            var pixels = new Color32[TextureLength * TextureWidth];
            for (int y = 0; y < TextureWidth; y++)
            {
                float v = (y + 0.5f) / TextureWidth;

                float noise = 0f;
                for (int k = 0; k < StreakWaves; k++)
                    noise += Mathf.Sin(v * rates[k] + phases[k]) / (k + 1);
                float streak = 1f - StreakDepth * (0.5f + 0.5f * Mathf.Clamp(noise * 0.6f, -1f, 1f));
                float edge = Mathf.Pow(Mathf.Sin(Mathf.PI * v), EdgePower) * streak;

                for (int x = 0; x < TextureLength; x++)
                {
                    float u = (x + 0.5f) / TextureLength;
                    float fade = Mathf.Clamp01(u / FadeIn) * Mathf.Pow(1f - u, FadePower);
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(edge * fade));
                    pixels[y * TextureLength + x] = new Color32(255, 255, 255, a);
                }
            }

            _texture = new Texture2D(TextureLength, TextureWidth, TextureFormat.RGBA32, true)
            {
                name = "Boat Wake Foam",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(true, true);
            return _texture;
        }
    }
}
