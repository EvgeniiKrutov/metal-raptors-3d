using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    [DefaultExecutionOrder(1000)]
    public class Searchlight : MonoBehaviour
    {
        const int Segments = 1;
        const float FailSeconds = 0.9f;
        const float FailFlickerRate = 22f;
        const float MinDown = 0.25f;

        static readonly Color BeamColor = new Color(1f, 0.851f, 0.627f);

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RangeId = Shader.PropertyToID("_Range");
        static readonly int SourceRadiusId = Shader.PropertyToID("_SourceRadius");
        static readonly int CosInnerId = Shader.PropertyToID("_CosInner");
        static readonly int CosOuterId = Shader.PropertyToID("_CosOuter");
        static readonly int FalloffId = Shader.PropertyToID("_Falloff");
        static readonly int GlowSizeId = Shader.PropertyToID("_GlowSize");
        static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        static readonly int SoftId = Shader.PropertyToID("_Soft");

        static Mesh _mesh;

        public Vector3 aim = Vector3.down;
        public Vector3 sweepAxis = Vector3.forward;
        public float sweep;
        public float period = 10f;
        public float length = 100f;
        public float reachY = float.NaN;
        public float reachOvershoot = 1.7f;
        public float sourceRadius = 1f;
        public float glowSize = 2f;
        public float intensity = 0.5f;
        public float glowIntensity = 1.5f;
        public float soft = 4f;

        Transform _beam;
        MeshRenderer _renderer;
        Material _material;
        float _phase, _seed;
        float _failAt = -1f;
        bool _out;

        public static Searchlight Mount(Transform parent, Vector3 localPosition)
        {
            if (parent == null) return null;

            var shader = Shader.Find("Hidden/SearchlightBeam");
            if (shader == null) return null;

            var go = new GameObject("Searchlight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var light = go.AddComponent<Searchlight>();
            light.Build(shader);
            return light;
        }

        public void Fail()
        {
            if (_failAt < 0f) _failAt = Time.time;
        }

        void Build(Shader shader)
        {
            _phase = Random.value * Mathf.PI * 2f;
            _seed = Random.value * 100f;

            _material = new Material(shader) { name = "Searchlight Beam (runtime)" };
            _material.SetColor(TintId, BeamColor);
            _material.SetFloat(CosInnerId, Mathf.Cos(PlaneSearchlight.InnerAngle * 0.5f * Mathf.Deg2Rad));
            _material.SetFloat(CosOuterId, Mathf.Cos(PlaneSearchlight.OuterAngle * 0.5f * Mathf.Deg2Rad));
            _material.SetFloat(FalloffId, PlaneSearchlight.Falloff);

            var beam = new GameObject("Searchlight Beam");
            _beam = beam.transform;
            beam.AddComponent<MeshFilter>().sharedMesh = SharedMesh();

            _renderer = beam.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.enabled = false;
        }

        void LateUpdate()
        {
            if (_renderer == null) return;

            float level = Level();
            if (level <= 0.001f)
            {
                _renderer.enabled = false;
                return;
            }

            float angle = sweep * Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, period) + _phase);
            Vector3 dir = transform.rotation * (Quaternion.AngleAxis(angle, sweepAxis) * aim.normalized);
            Vector3 origin = transform.position;

            float reach = length;
            if (!float.IsNaN(reachY))
                reach = Mathf.Max(0f, origin.y - reachY) / Mathf.Max(MinDown, -dir.y) * reachOvershoot;

            float halfWidth = Mathf.Min(reach,
                (reach + sourceRadius) * Mathf.Sin(PlaneSearchlight.OuterAngle * 0.5f * Mathf.Deg2Rad));

            _beam.SetPositionAndRotation(origin,
                Quaternion.LookRotation(dir, Mathf.Abs(dir.z) < 0.9f ? Vector3.forward : Vector3.up));

            _material.SetFloat(IntensityId, intensity * level);
            _material.SetFloat(GlowIntensityId, glowIntensity);
            _material.SetFloat(RangeId, reach);
            _material.SetFloat(SourceRadiusId, sourceRadius);
            _material.SetFloat(GlowSizeId, glowSize);
            _material.SetFloat(SoftId, soft);

            float side = 2f * halfWidth + glowSize * 4f;
            _renderer.localBounds = new Bounds(new Vector3(0f, 0f, reach * 0.5f),
                new Vector3(side, side, reach + sourceRadius + glowSize * 4f));
            _renderer.enabled = true;
        }

        float Level()
        {
            if (_out) return 0f;

            NightLightingController night = NightLightingController.Current;
            float level = night != null ? night.Blend : 0f;
            if (_failAt < 0f) return level;

            float elapsed = Time.time - _failAt;
            if (elapsed >= FailSeconds)
            {
                _out = true;
                return 0f;
            }

            bool lit = Mathf.PerlinNoise(elapsed * FailFlickerRate, _seed) > 0.45f;
            return level * (lit ? 1f - 0.5f * elapsed / FailSeconds : 0.08f);
        }

        void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        void OnDestroy()
        {
            if (_beam != null) Destroy(_beam.gameObject);
            if (_material != null) Destroy(_material);
        }

        static Mesh SharedMesh()
        {
            if (_mesh != null) return _mesh;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;
                vertices.Add(Vector3.zero);
                uvs.Add(new Vector3(-1f, t, 0f));
                vertices.Add(Vector3.zero);
                uvs.Add(new Vector3(1f, t, 0f));
                if (i == 0) continue;

                int a = (i - 1) * 2;
                triangles.Add(a);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 3);
            }

            int g = vertices.Count;
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector3(-1f, -1f, 1f));
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector3(1f, -1f, 1f));
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector3(-1f, 1f, 1f));
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector3(1f, 1f, 1f));
            triangles.Add(g);
            triangles.Add(g + 2);
            triangles.Add(g + 1);
            triangles.Add(g + 1);
            triangles.Add(g + 2);
            triangles.Add(g + 3);

            _mesh = new Mesh { name = "Searchlight Beam" };
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(triangles, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return _mesh;
        }
    }
}
