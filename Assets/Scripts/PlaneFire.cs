using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class PlaneFire : MonoBehaviour
    {
        const int FlameCount = 5;
        const float SizeFactor = 0.20f;
        const float SizeJitter = 0.35f;
        const float NoseSetback = 0.06f;
        const float TrailSpread = 0.10f;
        const float NoseSpread = 0.03f;
        const float LateralSpread = 0.05f;
        const float FallbackNoseFactor = 0.35f;
        const float FlickerHzMin = 5f, FlickerHzMax = 10f;
        const float FlickerDepth = 0.32f;
        const float EmissionStrength = 2.6f;

        static readonly Color Deep = new Color(1f, 0.32f, 0.05f);
        static readonly Color Hot = new Color(1f, 0.86f, 0.38f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        static readonly Vector3[] Corners =
        {
            new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
            new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f),
            new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
            new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, 1f),
        };

        struct Flame
        {
            public Transform tr;
            public Material mat;
            public float scale, hz, phase;
        }

        Flame[] _flames;

        public static PlaneFire Ignite(GameObject plane, float size)
        {
            if (plane == null) return null;

            var root = new GameObject("Fire");
            root.transform.SetParent(plane.transform, false);
            var fire = root.AddComponent<PlaneFire>();
            fire.Build(Mathf.Max(1f, size));
            return fire;
        }

        public void Extinguish() => Destroy(gameObject);

        void Build(float size)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Vector3 nose = NoseLocal(transform.parent, size);
            nose.x -= size * NoseSetback;
            _flames = new Flame[FlameCount];

            for (int i = 0; i < FlameCount; i++)
            {
                var go = new GameObject("Flame");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = nose + new Vector3(
                    Random.Range(-TrailSpread, NoseSpread) * size,
                    Random.Range(-LateralSpread, LateralSpread) * size,
                    Random.Range(-LateralSpread, LateralSpread) * size);
                go.transform.localRotation = Random.rotation;

                go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;

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
                    scale = size * SizeFactor * Random.Range(1f - SizeJitter, 1f + SizeJitter),
                    hz = Random.Range(FlickerHzMin, FlickerHzMax),
                    phase = Random.Range(0f, Mathf.PI * 2f),
                };
            }
        }

        static Vector3 NoseLocal(Transform body, float size)
        {
            var fallback = new Vector3(size * FallbackNoseFactor, 0f, 0f);
            if (body == null) return fallback;

            var hitbox = body.GetComponentInChildren<Collider>();
            var fuselage = hitbox != null ? hitbox.GetComponent<Renderer>() : null;
            if (fuselage == null) return fallback;

            Bounds local = fuselage.localBounds;
            Transform tr = fuselage.transform;

            float noseX = float.NegativeInfinity;
            foreach (Vector3 sign in Corners)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, sign);
                float x = body.InverseTransformPoint(tr.TransformPoint(corner)).x;
                if (x > noseX) noseX = x;
            }

            Vector3 centre = body.InverseTransformPoint(tr.TransformPoint(local.center));
            return new Vector3(noseX, centre.y, centre.z);
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
            {
                if (flame.mat != null) Destroy(flame.mat);
            }
        }
    }
}
