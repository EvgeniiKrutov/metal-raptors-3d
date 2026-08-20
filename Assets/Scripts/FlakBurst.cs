using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class FlakBurst : MonoBehaviour
    {
        const int PuffMin = 5, PuffMax = 7;
        const int MeshVariants = 6;

        const float CoreLife = 0.1f;
        const float CoreSize = 0.16f;
        const float CoreEndFactor = 0.35f;
        const float CoreGlow = 4f;

        const float LifeMin = 6f, LifeMax = 10f;
        const float LifeJitter = 0.12f;

        const float StartScale = 0.14f;
        const float EndScaleMin = 0.42f, EndScaleMax = 0.68f;
        const float ClusterSpread = 0.16f;

        const float BloomSpeed = 0.8f;
        const float BloomDamping = 3.2f;

        const float SinkMin = 3.5f, SinkMax = 7f;
        const float SinkRamp = 1.2f;
        const float SinkJitter = 0.15f;

        const float WindX = 7f, WindJitter = 3f;

        const float SpinMin = 3f, SpinMax = 14f;

        const float Opacity = 0.62f;
        const float FadeIn = 0.06f;
        const float FadeFrom = 0.55f;
        const float Lighten = 0.16f;

        const float NearDistance = 250f, FarDistance = 950f;
        const float NearVolume = 0.16f, VolumeFloor = 0.02f;
        const float PitchMin = 0.7f, PitchMax = 0.95f;

        static readonly Color CoreColor = new Color(1f, 0.85f, 0.5f);
        static readonly Color SootColor = new Color(0.26f, 0.25f, 0.24f);
        static readonly Color EarthColor = new Color(0.34f, 0.28f, 0.20f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        static Mesh[] _meshes;
        static Shader _lit;

        class Piece
        {
            public Transform tr;
            public Material mat;
            public Color color;
            public Vector3 shape;
            public Vector3 bloom;
            public Vector3 drift;
            public Vector3 spinAxis;
            public float sink;
            public float spinRate;
            public float life, age;
            public float startScale, endScale;
            public bool glows;
        }

        readonly List<Piece> _pieces = new List<Piece>();

        public static void Spawn(Vector3 position, float size, Vector3 listener, bool sound)
        {
            var root = new GameObject("Flak Burst");
            root.transform.position = position;

            var burst = root.AddComponent<FlakBurst>();
            burst.BuildCore(size);
            burst.BuildSmoke(size);

            if (sound) PlaySound(position, listener);
        }

        void BuildCore(float size)
        {
            float scale = size * CoreSize;
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Sphere, transform.position,
                Vector3.one * scale, CoreColor, emissive: true, keepCollider: false);
            go.name = "Core";

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.SetParent(transform, true);

            _pieces.Add(new Piece
            {
                tr = go.transform,
                mat = renderer.sharedMaterial,
                shape = Vector3.one,
                life = CoreLife,
                startScale = scale,
                endScale = scale * CoreEndFactor,
                glows = true,
            });
        }

        void BuildSmoke(float size)
        {
            Color tone = Color.Lerp(SootColor, EarthColor, Random.value);
            float life = Random.Range(LifeMin, LifeMax);
            float sink = Random.Range(SinkMin, SinkMax);
            var drift = new Vector3(WindX + Random.Range(-WindJitter, WindJitter), 0f,
                Random.Range(-WindJitter, WindJitter) * 0.5f);

            int count = Random.Range(PuffMin, PuffMax + 1);
            for (int i = 0; i < count; i++)
            {
                var offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f) * 0.5f) * (size * ClusterSpread);

                var shape = new Vector3(Random.Range(0.9f, 1.35f), Random.Range(0.8f, 1.1f),
                    Random.Range(0.9f, 1.35f));
                float start = size * StartScale;

                var go = new GameObject("Puff");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = offset;
                go.transform.localRotation = Random.rotation;
                go.transform.localScale = Vector3.Scale(Vector3.one * start, shape);

                go.AddComponent<MeshFilter>().sharedMesh = PuffMesh();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                float jitter = Random.Range(-0.03f, 0.03f);
                var color = new Color(tone.r + jitter, tone.g + jitter, tone.b + jitter, Opacity);
                var mat = SmokeMaterial(color);
                if (mat != null) renderer.sharedMaterial = mat;

                var outward = offset.sqrMagnitude > 0.001f
                    ? offset.normalized : Random.onUnitSphere;

                _pieces.Add(new Piece
                {
                    tr = go.transform,
                    mat = mat,
                    color = color,
                    shape = shape,
                    bloom = outward * (size * BloomSpeed * Random.Range(0.6f, 1.2f)),
                    drift = drift,
                    sink = sink * Random.Range(1f - SinkJitter, 1f + SinkJitter),
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(SpinMin, SpinMax) * (Random.value < 0.5f ? -1f : 1f),
                    life = life * Random.Range(1f - LifeJitter, 1f + LifeJitter),
                    startScale = start,
                    endScale = size * Random.Range(EndScaleMin, EndScaleMax),
                });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool alive = false;

            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                p.age += dt;

                if (p.age >= p.life)
                {
                    if (p.tr != null) p.tr.localScale = Vector3.zero;
                    continue;
                }
                alive = true;
                if (p.tr == null) continue;

                float t = p.age / p.life;

                if (!p.glows)
                {
                    p.bloom *= Mathf.Exp(-BloomDamping * dt);
                    Vector3 step = p.bloom + p.drift;
                    step.y -= p.sink * Mathf.Clamp01(p.age / SinkRamp);
                    p.tr.localPosition += step * dt;
                    p.tr.Rotate(p.spinAxis, p.spinRate * dt, Space.Self);
                }

                float ease = 1f - (1f - t) * (1f - t);
                p.tr.localScale = Vector3.Scale(
                    Vector3.one * Mathf.Lerp(p.startScale, p.endScale, ease), p.shape);

                if (p.mat == null) continue;

                if (p.glows)
                {
                    p.mat.SetColor(EmissionColorId, CoreColor * (CoreGlow * (1f - t)));
                    continue;
                }

                float lift = Lighten * t;
                var c = new Color(p.color.r + lift, p.color.g + lift, p.color.b + lift,
                    p.color.a * Alpha(t));
                p.mat.SetColor(BaseColorId, c);
            }

            if (!alive) Destroy(gameObject);
        }

        void OnDestroy()
        {
            foreach (var p in _pieces)
                if (p.mat != null) Destroy(p.mat);
            _pieces.Clear();
        }

        static float Alpha(float t)
        {
            if (t < FadeIn) return t / FadeIn;
            if (t <= FadeFrom) return 1f;
            float k = 1f - (t - FadeFrom) / (1f - FadeFrom);
            return k * k;
        }

        static Mesh PuffMesh()
        {
            if (_meshes == null)
            {
                _meshes = new Mesh[MeshVariants];
                for (int i = 0; i < MeshVariants; i++)
                {
                    _meshes[i] = BlobMesh.Build();
                    _meshes[i].name = $"Flak Puff {i}";
                }
            }
            return _meshes[Random.Range(0, MeshVariants)];
        }

        static Material SmokeMaterial(Color color)
        {
            if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) return null;

            var mat = new Material(_lit) { name = "Flak Smoke" };
            mat.SetColor(BaseColorId, color);
            mat.SetFloat("_Smoothness", 0f);
            UIFactory.MakeTransparent(mat);
            return mat;
        }

        static void PlaySound(Vector3 position, Vector3 listener)
        {
            float falloff = 1f - Mathf.InverseLerp(NearDistance, FarDistance,
                Vector3.Distance(position, listener));
            float volume = NearVolume * falloff;
            if (volume < VolumeFloor) return;

            var clip = Resources.Load<AudioClip>($"Sounds/explosion_{Random.Range(1, 4)}");
            if (clip == null) return;

            var go = new GameObject("FlakSound");
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.pitch = Random.Range(PitchMin, PitchMax);
            audio.PlayOneShot(clip, volume);
            Destroy(go, clip.length / audio.pitch + 0.1f);
        }
    }
}
