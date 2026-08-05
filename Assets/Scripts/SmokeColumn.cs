using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class SmokeColumn : MonoBehaviour
    {
        const float EmitInterval = 0.55f;
        const float PuffLife = 13f;
        const float RiseMin = 19f, RiseMax = 28f;
        const float WindX = 7f;
        const float WindJitter = 5f;
        const float StartSizeMin = 12f, StartSizeMax = 18f;
        const float StartSpread = 5f;
        const float GrowthFactor = 4.5f;
        const float Opacity = 0.5f;
        const float SpinMin = 5f, SpinMax = 20f;

        const float EmberSize = 9f;
        const float EmberGlow = 1.6f;
        const float EmberPulseRate = 1.7f;
        const float EmberPulseDepth = 0.35f;

        static readonly Color SmokeColor = new Color(0.13f, 0.12f, 0.12f, Opacity);
        static readonly Color EmberColor = new Color(0.85f, 0.30f, 0.07f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        class Puff
        {
            public Transform tr;
            public Material mat;
            public Vector3 velocity;
            public Vector3 spinAxis;
            public float spinRate;
            public float startScale;
            public float age;
        }

        readonly List<Puff> _puffs = new List<Puff>();
        System.Random _rng;
        Material _emberMat;
        float _emitTimer;
        float _emberPhase;

        public static SmokeColumn Begin(Transform parent, Vector3 position, int seed)
        {
            var go = new GameObject("Smoke Column");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var column = go.AddComponent<SmokeColumn>();
            column._rng = new System.Random(seed);
            column._emberPhase = (float)column._rng.NextDouble() * Mathf.PI * 2f;
            column.BuildEmber();
            column.Prewarm();
            return column;
        }

        void BuildEmber()
        {
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                new Vector3(EmberSize * 1.6f, EmberSize * 0.5f, EmberSize * 1.6f),
                EmberColor, emissive: true, keepCollider: false);
            go.name = "Ember";

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _emberMat = renderer.sharedMaterial;
            go.transform.SetParent(transform, true);
        }

        void Prewarm()
        {
            int count = Mathf.FloorToInt(PuffLife / EmitInterval);
            for (int i = count; i > 0; i--) EmitPuff(i * EmitInterval);
            _emitTimer = EmitInterval;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            _emitTimer -= dt;
            if (_emitTimer <= 0f)
            {
                _emitTimer = EmitInterval;
                EmitPuff(0f);
            }

            Animate(dt);
            PulseEmber();
        }

        void EmitPuff(float initialAge)
        {
            float scale = Range(StartSizeMin, StartSizeMax);

            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                Vector3.one * scale, SmokeColor, emissive: false, keepCollider: false);
            go.name = "Puff";

            var renderer = go.GetComponent<Renderer>();
            UIFactory.MakeTransparent(renderer.sharedMaterial);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                Range(-StartSpread, StartSpread), 0f, Range(-StartSpread, StartSpread));
            go.transform.localRotation = Quaternion.Euler(
                Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));

            var puff = new Puff
            {
                tr = go.transform,
                mat = renderer.sharedMaterial,
                velocity = new Vector3(WindX + Range(-WindJitter, WindJitter),
                    Range(RiseMin, RiseMax), Range(-WindJitter, WindJitter) * 0.5f),
                spinAxis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)).normalized,
                spinRate = Range(SpinMin, SpinMax) * (_rng.NextDouble() < 0.5 ? -1f : 1f),
                startScale = scale,
            };
            _puffs.Add(puff);

            if (initialAge > 0f) Advance(puff, initialAge);
        }

        void Animate(float dt)
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var puff = _puffs[i];
                if (puff.tr == null) { _puffs.RemoveAt(i); continue; }

                if (puff.age >= PuffLife)
                {
                    if (puff.mat != null) Destroy(puff.mat);
                    Destroy(puff.tr.gameObject);
                    _puffs.RemoveAt(i);
                    continue;
                }
                Advance(puff, dt);
            }
        }

        static void Advance(Puff puff, float dt)
        {
            puff.age += dt;
            puff.tr.localPosition += puff.velocity * dt;
            puff.tr.Rotate(puff.spinAxis, puff.spinRate * dt, Space.Self);

            float t = Mathf.Clamp01(puff.age / PuffLife);
            puff.tr.localScale = Vector3.one * Mathf.Lerp(puff.startScale,
                puff.startScale * GrowthFactor, t);

            if (puff.mat == null) return;
            var c = SmokeColor;
            c.a = Opacity * (1f - t) * (1f - t);
            puff.mat.SetColor(BaseColorId, c);
        }

        void PulseEmber()
        {
            if (_emberMat == null) return;
            float pulse = 1f + Mathf.Sin(Time.time * EmberPulseRate + _emberPhase) * EmberPulseDepth;
            _emberMat.SetColor(EmissionColorId, EmberColor * (EmberGlow * pulse));
        }

        void OnDestroy()
        {
            foreach (var puff in _puffs)
                if (puff.mat != null) Destroy(puff.mat);
            _puffs.Clear();
            if (_emberMat != null) Destroy(_emberMat);
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    }
}
