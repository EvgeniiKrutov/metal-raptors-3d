using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class WaterSplash : MonoBehaviour
    {
        const int ShardMin = 9, ShardMax = 15;
        const int MistMin = 5, MistMax = 8;
        const int MeshVariants = 6;
        const float ReferenceSize = 40f;
        const float Gravity = 120f;

        const float ColumnLife = 0.55f;
        const float ColumnRise = 1.5f;
        const float ColumnWidth = 0.20f;

        const float ShardLifeMin = 0.9f, ShardLifeMax = 1.7f;
        const float ShardRiseMin = 90f, ShardRiseMax = 190f;
        const float ShardSpread = 0.30f;
        const float ShardScaleMin = 0.05f, ShardScaleMax = 0.15f;
        const float ShardSpinMin = 60f, ShardSpinMax = 220f;
        const float ShardShrinkFrom = 0.55f;

        const float MistLifeMin = 1.4f, MistLifeMax = 2.4f;
        const float MistRiseMin = 10f, MistRiseMax = 24f;
        const float MistOutward = 26f;
        const float MistScaleMin = 0.26f, MistScaleMax = 0.48f;
        const float MistGrowth = 2.4f;
        const float MistOpacity = 0.40f;

        const float NearDistance = 430f, FarDistance = 1250f;
        const float NearVolume = 0.16f, VolumeFloor = 0.025f;
        const float PitchMin = 0.45f, PitchMax = 0.7f;

        static readonly Color SprayColor = new Color(0.82f, 0.86f, 0.84f);
        static readonly Color MistColor = new Color(0.74f, 0.79f, 0.77f, MistOpacity);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static Mesh[] _shardMeshes;
        static Material _shardMaterial;

        class Piece
        {
            public Transform tr;
            public Material mat;
            public Vector3 velocity;
            public Vector3 spinAxis;
            public Vector3 startScale, endScale;
            public float spinRate;
            public float life, age;
            public bool falls, fades;
        }

        readonly List<Piece> _pieces = new List<Piece>();

        public static void Spawn(Vector3 position, float size, Vector3 listener)
        {
            var root = new GameObject("Water Splash");
            root.transform.position = position;

            var splash = root.AddComponent<WaterSplash>();
            splash.BuildColumn(size);
            splash.BuildShards(size);
            splash.BuildMist(size);

            PlaySound(position, listener);
        }

        static float SizeBoost(float size) => Mathf.Sqrt(size / ReferenceSize);

        void BuildColumn(float size)
        {
            float width = size * ColumnWidth;
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                new Vector3(width, width, width), SprayColor, emissive: false, keepCollider: false);
            go.name = "Plume";

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.SetParent(transform, true);

            _pieces.Add(new Piece
            {
                tr = go.transform,
                mat = renderer.sharedMaterial,
                life = ColumnLife,
                startScale = new Vector3(width, width * 0.4f, width),
                endScale = new Vector3(width * 0.7f, size * ColumnRise, width * 0.7f),
            });
        }

        void BuildShards(float size)
        {
            float boost = SizeBoost(size);
            int count = Random.Range(ShardMin, ShardMax + 1);

            for (int i = 0; i < count; i++)
            {
                float scale = size * Random.Range(ShardScaleMin, ShardScaleMax);

                var go = new GameObject("Spray");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    Random.Range(-1f, 1f) * size * 0.15f, size * 0.04f,
                    Random.Range(-1f, 1f) * size * 0.15f);
                go.transform.localRotation = Random.rotation;
                go.transform.localScale = Vector3.one * scale;

                go.AddComponent<MeshFilter>().sharedMesh = ShardMesh();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ShardMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                var dir = new Vector3(Random.Range(-1f, 1f) * ShardSpread, 1f,
                    Random.Range(-1f, 1f) * ShardSpread).normalized;

                _pieces.Add(new Piece
                {
                    tr = go.transform,
                    velocity = dir * Random.Range(ShardRiseMin, ShardRiseMax) * boost,
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(ShardSpinMin, ShardSpinMax) * (Random.value < 0.5f ? -1f : 1f),
                    life = Random.Range(ShardLifeMin, ShardLifeMax) * boost,
                    startScale = Vector3.one * scale,
                    endScale = Vector3.zero,
                    falls = true,
                });
            }
        }

        void BuildMist(float size)
        {
            float boost = SizeBoost(size);
            int count = Random.Range(MistMin, MistMax + 1);

            for (int i = 0; i < count; i++)
            {
                float scale = size * Random.Range(MistScaleMin, MistScaleMax);

                var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                    Vector3.one * scale, MistColor, emissive: false, keepCollider: false);
                go.name = "Mist";
                go.transform.rotation = Random.rotation;

                var renderer = go.GetComponent<Renderer>();
                UIFactory.MakeTransparent(renderer.sharedMaterial);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                go.transform.SetParent(transform, true);

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float outward = MistOutward * Random.Range(0.3f, 1f) * (size / ReferenceSize);

                _pieces.Add(new Piece
                {
                    tr = go.transform,
                    mat = renderer.sharedMaterial,
                    velocity = new Vector3(Mathf.Cos(angle) * outward,
                        Random.Range(MistRiseMin, MistRiseMax) * boost, Mathf.Sin(angle) * outward),
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(8f, 40f) * (Random.value < 0.5f ? -1f : 1f),
                    life = Random.Range(MistLifeMin, MistLifeMax),
                    startScale = Vector3.one * scale,
                    endScale = Vector3.one * (scale * MistGrowth),
                    fades = true,
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

                if (p.falls) p.velocity.y -= Gravity * dt;
                if (p.velocity != Vector3.zero) p.tr.position += p.velocity * dt;
                if (p.spinRate != 0f) p.tr.Rotate(p.spinAxis, p.spinRate * dt, Space.World);

                p.tr.localScale = ScaleAt(p, t);

                if (p.mat == null || !p.fades) continue;
                var c = MistColor;
                c.a = MistOpacity * (1f - t) * (1f - t);
                p.mat.SetColor(BaseColorId, c);
            }

            if (!alive) Destroy(gameObject);
        }

        static Vector3 ScaleAt(Piece p, float t)
        {
            if (!p.falls) return Vector3.Lerp(p.startScale, p.endScale, t);
            if (t < ShardShrinkFrom) return p.startScale;
            return Vector3.Lerp(p.startScale, p.endScale, (t - ShardShrinkFrom) / (1f - ShardShrinkFrom));
        }

        void OnDestroy()
        {
            foreach (var p in _pieces)
                if (p.mat != null) Destroy(p.mat);
            _pieces.Clear();
        }

        static Mesh ShardMesh()
        {
            if (_shardMeshes == null)
            {
                _shardMeshes = new Mesh[MeshVariants];
                for (int i = 0; i < MeshVariants; i++)
                {
                    _shardMeshes[i] = BlobMesh.Build();
                    _shardMeshes[i].name = $"Splash Shard {i}";
                }
            }
            return _shardMeshes[Random.Range(0, MeshVariants)];
        }

        static Material ShardMaterial()
        {
            if (_shardMaterial != null) return _shardMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _shardMaterial = new Material(shader) { name = "Splash Spray" };
            _shardMaterial.SetColor(BaseColorId, SprayColor);
            _shardMaterial.SetFloat("_Smoothness", 0.2f);
            return _shardMaterial;
        }

        static void PlaySound(Vector3 position, Vector3 listener)
        {
            float falloff = 1f - Mathf.InverseLerp(NearDistance, FarDistance,
                Vector3.Distance(position, listener));
            float volume = NearVolume * falloff;
            if (volume < VolumeFloor) return;

            var clip = Resources.Load<AudioClip>($"Sounds/explosion_{Random.Range(1, 4)}");
            if (clip == null) return;

            var go = new GameObject("WaterSplashSound");
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
