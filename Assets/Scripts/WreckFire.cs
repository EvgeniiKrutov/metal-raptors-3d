using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class WreckFire : MonoBehaviour
    {
        const int FlameCount = 7;
        const float FlameSizeMin = 0.10f, FlameSizeMax = 0.26f;
        const float FlameSpread = 0.42f;
        const float FlameRiseMin = 0.28f, FlameRiseMax = 0.85f;
        const float FlickerHzMin = 3.5f, FlickerHzMax = 8f;
        const float FlickerDepth = 0.36f;
        const float EmissionStrength = 2.4f;

        const float SmokeScale = 0.55f;
        const float SmokeLift = 0.35f;

        static readonly Color Deep = new Color(1f, 0.29f, 0.04f);
        static readonly Color Hot = new Color(1f, 0.82f, 0.34f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        struct Flame
        {
            public Transform tr;
            public Material mat;
            public float scale, hz, phase;
        }

        Flame[] _flames;

        public static WreckFire Begin(Transform parent, Vector3 basePoint, float radius, int seed)
        {
            var go = new GameObject("Wreck Fire");
            go.transform.SetParent(parent, false);
            go.transform.position = basePoint;

            float size = Mathf.Max(1f, radius);
            var fire = go.AddComponent<WreckFire>();
            fire.Build(size, new System.Random(seed));

            SmokeColumn.Begin(go.transform, basePoint + Vector3.up * (size * SmokeLift),
                seed, SmokeScale);
            return fire;
        }

        void Build(float radius, System.Random rng)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _flames = new Flame[FlameCount];

            for (int i = 0; i < FlameCount; i++)
            {
                var go = new GameObject("Flame");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    Range(rng, -FlameSpread, FlameSpread) * radius,
                    Range(rng, FlameRiseMin, FlameRiseMax) * radius,
                    Range(rng, -FlameSpread, FlameSpread) * radius);
                go.transform.localRotation = Quaternion.Euler(
                    Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));

                go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Material mat = null;
                if (shader != null)
                {
                    mat = new Material(shader);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    renderer.sharedMaterial = mat;
                }

                _flames[i] = new Flame
                {
                    tr = go.transform,
                    mat = mat,
                    scale = radius * Range(rng, FlameSizeMin, FlameSizeMax),
                    hz = Range(rng, FlickerHzMin, FlickerHzMax),
                    phase = Range(rng, 0f, Mathf.PI * 2f),
                };
            }
        }

        void Update()
        {
            if (_flames == null) return;

            float now = Time.time;
            for (int i = 0; i < _flames.Length; i++)
            {
                var flame = _flames[i];
                if (flame.tr == null) continue;

                float t = now * flame.hz + flame.phase;
                float pulse = 1f + FlickerDepth * Mathf.Sin(t) * Mathf.Sin(t * 0.41f + 1.7f);
                flame.tr.localScale = Vector3.one * (flame.scale * pulse);

                if (flame.mat == null) continue;
                Color c = Color.Lerp(Deep, Hot, 0.5f + 0.5f * Mathf.Sin(t * 0.7f));
                flame.mat.SetColor(BaseColorId, c);
                flame.mat.SetColor(EmissionColorId, c * EmissionStrength);
            }
        }

        void OnDestroy()
        {
            if (_flames == null) return;

            foreach (var flame in _flames)
                if (flame.mat != null) Destroy(flame.mat);
        }

        static float Range(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
