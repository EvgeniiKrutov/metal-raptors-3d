using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class MuzzleSmoke : MonoBehaviour
    {
        const int PuffMin = 4, PuffMax = 6;
        const float LifeMin = 2.2f, LifeMax = 3.4f;
        const float StartScale = 0.25f;
        const float EndScaleMin = 0.8f, EndScaleMax = 1.25f;
        const float Push = 2.2f;
        const float PushDamping = 2.6f;
        const float Scatter = 0.35f;
        const float Rise = 0.18f;
        const float Opacity = 0.55f;
        const float FadeIn = 0.08f;
        const float FadeFrom = 0.35f;

        static readonly Color SmokeColor = new Color(0.78f, 0.76f, 0.72f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        class Puff
        {
            public Transform tr;
            public Material mat;
            public Color color;
            public Vector3 velocity;
            public float life, age;
            public float startScale, endScale;
        }

        readonly List<Puff> _puffs = new List<Puff>();
        float _rise;

        public static void Spawn(Vector3 position, Vector3 direction, float size)
        {
            var root = new GameObject("Muzzle Smoke");
            root.transform.position = position;

            var smoke = root.AddComponent<MuzzleSmoke>();
            smoke._rise = size * Rise;
            smoke.Build(direction.normalized, size);
        }

        void Build(Vector3 direction, float size)
        {
            int count = Random.Range(PuffMin, PuffMax + 1);
            for (int i = 0; i < count; i++)
            {
                float start = size * StartScale;

                var go = new GameObject("Puff");
                go.transform.SetParent(transform, false);
                go.transform.localRotation = Random.rotation;
                go.transform.localScale = Vector3.one * start;

                go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                float shade = Random.Range(-0.05f, 0.05f);
                var color = new Color(SmokeColor.r + shade, SmokeColor.g + shade,
                    SmokeColor.b + shade, 0f);
                var mat = SmokeMaterial(color);
                if (mat != null) renderer.sharedMaterial = mat;

                float reach = Mathf.Lerp(0.4f, 1f, (i + 1f) / count);
                _puffs.Add(new Puff
                {
                    tr = go.transform,
                    mat = mat,
                    color = color,
                    velocity = (direction * reach + Random.insideUnitSphere * Scatter) * (size * Push),
                    life = Random.Range(LifeMin, LifeMax),
                    startScale = start,
                    endScale = size * Random.Range(EndScaleMin, EndScaleMax),
                });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float damping = Mathf.Exp(-PushDamping * dt);
            bool alive = false;

            foreach (var p in _puffs)
            {
                p.age += dt;
                if (p.age >= p.life)
                {
                    if (p.tr != null) p.tr.localScale = Vector3.zero;
                    continue;
                }
                alive = true;
                if (p.tr == null) continue;

                float t = p.age / p.life;
                p.velocity *= damping;
                p.tr.localPosition += (p.velocity + Vector3.up * _rise) * dt;

                float ease = 1f - (1f - t) * (1f - t);
                p.tr.localScale = Vector3.one * Mathf.Lerp(p.startScale, p.endScale, ease);

                if (p.mat != null)
                    p.mat.SetColor(BaseColorId,
                        new Color(p.color.r, p.color.g, p.color.b, Opacity * Alpha(t)));
            }

            if (!alive) Destroy(gameObject);
        }

        void OnDestroy()
        {
            foreach (var p in _puffs)
                if (p.mat != null) Destroy(p.mat);
            _puffs.Clear();
        }

        static float Alpha(float t)
        {
            if (t < FadeIn) return t / FadeIn;
            if (t <= FadeFrom) return 1f;
            float k = 1f - (t - FadeFrom) / (1f - FadeFrom);
            return k * k;
        }

        static Material SmokeMaterial(Color color)
        {
            Shader lit = NightReceivers.Lit;
            if (lit == null) return null;

            var mat = new Material(lit) { name = "Muzzle Smoke" };
            mat.SetColor(BaseColorId, color);
            mat.SetFloat("_Smoothness", 0f);
            UIFactory.MakeTransparent(mat);
            NightReceivers.SetResponse(mat, NightReceivers.SmokeResponse);
            return mat;
        }
    }
}
