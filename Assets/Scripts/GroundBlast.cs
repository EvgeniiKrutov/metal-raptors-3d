using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class GroundBlast : MonoBehaviour
    {
        const int ClodMin = 7, ClodMax = 12;
        const int DustMin = 5, DustMax = 8;
        const int MeshVariants = 6;

        const float FlashLife = 0.16f;
        const float FlashSize = 0.6f;
        const float FlashEndFactor = 0.2f;
        const float FlashGlow = 3f;

        const float ClodLifeMin = 1.1f, ClodLifeMax = 2.1f;
        const float ClodRiseMin = 60f, ClodRiseMax = 145f;
        const float ClodSpread = 0.45f;
        const float ClodScaleMin = 0.09f, ClodScaleMax = 0.26f;
        const float ClodSpinMin = 90f, ClodSpinMax = 340f;
        const float ClodShrinkFrom = 0.7f;
        const float Gravity = 110f;

        const float DustLifeMin = 1.8f, DustLifeMax = 3.2f;
        const float DustRiseMin = 16f, DustRiseMax = 32f;
        const float DustOutward = 22f;
        const float DustScaleMin = 0.30f, DustScaleMax = 0.55f;
        const float DustGrowth = 2.6f;
        const float DustOpacity = 0.45f;
        const float DustSpinMin = 10f, DustSpinMax = 45f;
        const float ReferenceSize = 40f;

        const float NearDistance = 430f, FarDistance = 1250f;
        const float NearVolume = 0.2f, VolumeFloor = 0.025f;
        const float PitchMin = 0.5f, PitchMax = 0.8f;

        static readonly Color FlashColor = new Color(1f, 0.82f, 0.45f);
        static readonly Color ClodColor = new Color(0.23f, 0.18f, 0.13f);
        static readonly Color DustColor = new Color(0.31f, 0.26f, 0.20f, DustOpacity);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        static Mesh[] _clodMeshes;
        static Material _clodMaterial;

        class Piece
        {
            public Transform tr;
            public Material mat;
            public Vector3 velocity;
            public Vector3 spinAxis;
            public float spinRate;
            public float life, age;
            public float startScale, endScale;
            public bool falls, fades, glows;
        }

        readonly List<Piece> _pieces = new List<Piece>();

        public static void Spawn(Vector3 position, float size, Vector3 listener)
        {
            var root = new GameObject("Ground Blast");
            root.transform.position = position;

            var blast = root.AddComponent<GroundBlast>();
            blast.BuildFlash(size);
            blast.BuildClods(size);
            blast.BuildDust(size);

            PlaySound(position, listener);
        }

        void BuildFlash(float size)
        {
            float scale = size * FlashSize;
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Sphere, transform.position,
                Vector3.one * scale, FlashColor, emissive: true, keepCollider: false);
            go.name = "Flash";

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.SetParent(transform, true);

            _pieces.Add(new Piece
            {
                tr = go.transform,
                mat = renderer.sharedMaterial,
                life = FlashLife,
                startScale = scale,
                endScale = scale * FlashEndFactor,
                glows = true,
            });
        }

        void BuildClods(float size)
        {
            float boost = SizeBoost(size);
            int count = Random.Range(ClodMin, ClodMax + 1);
            for (int i = 0; i < count; i++)
            {
                float scale = size * Random.Range(ClodScaleMin, ClodScaleMax);

                var go = new GameObject("Clod");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    Random.Range(-1f, 1f) * size * 0.2f, size * 0.05f,
                    Random.Range(-1f, 1f) * size * 0.2f);
                go.transform.localRotation = Random.rotation;
                go.transform.localScale = Vector3.one * scale;

                go.AddComponent<MeshFilter>().sharedMesh = ClodMesh();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ClodMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                var dir = new Vector3(Random.Range(-1f, 1f) * ClodSpread, 1f,
                    Random.Range(-1f, 1f) * ClodSpread).normalized;

                _pieces.Add(new Piece
                {
                    tr = go.transform,
                    velocity = dir * Random.Range(ClodRiseMin, ClodRiseMax) * boost,
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(ClodSpinMin, ClodSpinMax) * (Random.value < 0.5f ? -1f : 1f),
                    life = Random.Range(ClodLifeMin, ClodLifeMax) * boost,
                    startScale = scale,
                    endScale = 0f,
                    falls = true,
                });
            }
        }

        void BuildDust(float size)
        {
            float boost = SizeBoost(size);
            int count = Random.Range(DustMin, DustMax + 1);
            for (int i = 0; i < count; i++)
            {
                float scale = size * Random.Range(DustScaleMin, DustScaleMax);

                var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                    Vector3.one * scale, DustColor, emissive: false, keepCollider: false);
                go.name = "Dust";
                go.transform.rotation = Random.rotation;

                var renderer = go.GetComponent<Renderer>();
                UIFactory.MakeTransparent(renderer.sharedMaterial);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                go.transform.SetParent(transform, true);

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float outward = DustOutward * Random.Range(0.2f, 1f) * (size / ReferenceSize);

                _pieces.Add(new Piece
                {
                    tr = go.transform,
                    mat = renderer.sharedMaterial,
                    velocity = new Vector3(Mathf.Cos(angle) * outward,
                        Random.Range(DustRiseMin, DustRiseMax) * boost, Mathf.Sin(angle) * outward),
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(DustSpinMin, DustSpinMax) * (Random.value < 0.5f ? -1f : 1f),
                    life = Random.Range(DustLifeMin, DustLifeMax),
                    startScale = scale,
                    endScale = scale * DustGrowth,
                    fades = true,
                });
            }
        }

        static float SizeBoost(float size) => Mathf.Sqrt(size / ReferenceSize);

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

                if (p.falls) p.velocity.y -= Gravity * dt;
                if (p.velocity != Vector3.zero) p.tr.position += p.velocity * dt;
                if (p.spinRate != 0f) p.tr.Rotate(p.spinAxis, p.spinRate * dt, Space.World);

                p.tr.localScale = Vector3.one * ScaleAt(p, t);

                if (p.mat == null) continue;
                if (p.fades)
                {
                    var c = DustColor;
                    c.a = DustOpacity * (1f - t) * (1f - t);
                    p.mat.SetColor(BaseColorId, c);
                }
                else if (p.glows)
                {
                    p.mat.SetColor(EmissionColorId, FlashColor * (FlashGlow * (1f - t)));
                }
            }

            if (!alive) Destroy(gameObject);
        }

        void OnDestroy()
        {
            foreach (var p in _pieces)
                if (p.mat != null) Destroy(p.mat);
            _pieces.Clear();
        }

        static float ScaleAt(Piece p, float t)
        {
            if (!p.falls) return Mathf.Lerp(p.startScale, p.endScale, t);
            if (t < ClodShrinkFrom) return p.startScale;
            return Mathf.Lerp(p.startScale, p.endScale, (t - ClodShrinkFrom) / (1f - ClodShrinkFrom));
        }

        static Mesh ClodMesh()
        {
            if (_clodMeshes == null)
            {
                _clodMeshes = new Mesh[MeshVariants];
                for (int i = 0; i < MeshVariants; i++)
                {
                    _clodMeshes[i] = BlobMesh.Build();
                    _clodMeshes[i].name = $"Blast Clod {i}";
                }
            }
            return _clodMeshes[Random.Range(0, MeshVariants)];
        }

        static Material ClodMaterial()
        {
            if (_clodMaterial != null) return _clodMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _clodMaterial = new Material(shader) { name = "Blast Clod" };
            _clodMaterial.SetColor(BaseColorId, ClodColor);
            _clodMaterial.SetFloat("_Smoothness", 0f);
            return _clodMaterial;
        }

        static void PlaySound(Vector3 position, Vector3 listener)
        {
            float falloff = 1f - Mathf.InverseLerp(NearDistance, FarDistance,
                Vector3.Distance(position, listener));
            float volume = NearVolume * falloff;
            if (volume < VolumeFloor) return;

            var clip = Resources.Load<AudioClip>($"Sounds/explosion_{Random.Range(1, 4)}");
            if (clip == null) return;

            var go = new GameObject("GroundBlastSound");
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
