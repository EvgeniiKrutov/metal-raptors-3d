using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class PhosphorusFire : MonoBehaviour
    {
        const int Patches = 26;
        const int FlamesPerPatch = 3;
        const float SpreadSec = 4.5f;
        const float ReachX = 0.85f, ReachZ = 0.25f;
        const float FlameSizeMin = 0.05f, FlameSizeMax = 0.11f;
        const float FlameRiseMin = 0.01f, FlameRiseMax = 0.07f;
        const float FlameJitter = 0.04f;
        const float FlickerHzMin = 5f, FlickerHzMax = 11f;
        const float FlickerDepth = 0.32f;
        const float GrowSec = 0.6f;
        const float Emission = 3.2f;

        const float SmokeEvery = 0.07f;
        const float SmokeLife = 7f;
        const float SmokeRiseMin = 34f, SmokeRiseMax = 58f;
        const float SmokeWind = 9f;
        const float SmokeSizeMin = 0.06f, SmokeSizeMax = 0.1f;
        const float SmokeGrowth = 3.6f;
        const float SmokeOpacity = 0.62f;
        const float SmokeGlow = 0.35f;

        const float LightRange = 2.2f;
        const float LightIntensity = 3.6f;
        const float LightFlicker = 0.18f;
        const float LightLift = 0.25f;
        const float FlashIntensity = 6f;
        const float FlashRadius = 2.6f;
        const float FlashSec = 1.1f;

        static readonly Color Hot = new Color(1f, 1f, 0.97f);
        static readonly Color Edge = new Color(0.92f, 0.95f, 1f);
        static readonly Color Smoke = new Color(0.93f, 0.94f, 0.95f, SmokeOpacity);
        static readonly Color LightColor = new Color(0.96f, 0.98f, 1f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        struct Flame
        {
            public Transform tr;
            public Material mat;
            public float scale, hz, phase, at;
        }

        class Puff
        {
            public Transform tr;
            public Material mat;
            public Vector3 velocity;
            public Vector3 axis;
            public float spin, size, age;
        }

        readonly List<Flame> _flames = new List<Flame>();
        readonly List<Puff> _puffs = new List<Puff>();
        readonly List<Vector3> _spots = new List<Vector3>();
        System.Random _rng;
        float _size;
        float _age;
        float _smokeIn;
        NightLight2D _light;

        public static PhosphorusFire Begin(Vector3 centre, float size, int seed)
        {
            var go = new GameObject("Phosphorus Fire");
            go.transform.position = centre;

            var fire = go.AddComponent<PhosphorusFire>();
            fire._size = Mathf.Max(10f, size);
            fire._rng = new System.Random(seed);
            fire.Build();
            NightLights.Flash(centre, LightColor, FlashIntensity, fire._size * FlashRadius, FlashSec, 10);
            return fire;
        }

        void Build()
        {
            var shader = NightReceivers.Lit;

            for (int p = 0; p < Patches; p++)
            {
                float reach = p == 0 ? 0f : Mathf.Sqrt((float)p / (Patches - 1));
                float turn = Range(0f, Mathf.PI * 2f);
                var spot = new Vector3(Mathf.Cos(turn) * reach * ReachX * _size, 0f,
                    Mathf.Sin(turn) * reach * ReachZ * _size);
                _spots.Add(spot);

                for (int i = 0; i < FlamesPerPatch; i++)
                {
                    var go = new GameObject("White Flame");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = spot + new Vector3(
                        Range(-FlameJitter, FlameJitter) * _size,
                        Range(FlameRiseMin, FlameRiseMax) * _size,
                        Range(-FlameJitter, FlameJitter) * _size);
                    go.transform.localRotation = Quaternion.Euler(Range(0f, 360f), Range(0f, 360f),
                        Range(0f, 360f));
                    go.transform.localScale = Vector3.zero;

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

                    _flames.Add(new Flame
                    {
                        tr = go.transform,
                        mat = mat,
                        scale = _size * Range(FlameSizeMin, FlameSizeMax),
                        hz = Range(FlickerHzMin, FlickerHzMax),
                        phase = Range(0f, Mathf.PI * 2f),
                        at = reach * SpreadSec,
                    });
                }
            }

            var lamp = new GameObject("Phosphorus Light");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up * (_size * LightLift);
            _light = lamp.AddComponent<NightLight2D>();
            _light.type = NightLightType.Point;
            _light.color = LightColor;
            _light.intensity = 0f;
            _light.range = _size * LightRange * 0.3f;
            _light.priority = 90;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;

            float now = Time.time;
            for (int i = 0; i < _flames.Count; i++)
            {
                Flame flame = _flames[i];
                if (flame.tr == null) continue;

                float grow = Mathf.Clamp01((_age - flame.at) / GrowSec);
                if (grow <= 0f) continue;

                float t = now * flame.hz + flame.phase;
                float pulse = 1f + FlickerDepth * Mathf.Sin(t) * Mathf.Sin(t * 0.43f + 1.3f);
                flame.tr.localScale = Vector3.one * (flame.scale * pulse * Mathf.SmoothStep(0f, 1f, grow));

                if (flame.mat == null) continue;
                Color c = Color.Lerp(Edge, Hot, 0.5f + 0.5f * Mathf.Sin(t * 0.6f));
                flame.mat.SetColor(BaseColorId, c);
                flame.mat.SetColor(EmissionColorId, c * Emission);
            }

            float spread = Mathf.Clamp01(_age / SpreadSec);
            if (_light != null)
            {
                float flicker = 1f + LightFlicker * (Mathf.PerlinNoise(now * 3f, 0.4f) * 2f - 1f);
                _light.intensity = LightIntensity * Mathf.SmoothStep(0.25f, 1f, spread) * flicker;
                _light.range = _size * LightRange * Mathf.Lerp(0.3f, 1f, spread);
            }

            _smokeIn -= dt;
            if (_smokeIn <= 0f)
            {
                _smokeIn = SmokeEvery;
                EmitPuff(spread);
            }

            AnimateSmoke(dt);
        }

        void EmitPuff(float spread)
        {
            int open = Mathf.Max(1, Mathf.CeilToInt(_spots.Count * spread));
            Vector3 spot = _spots[_rng.Next(open)];

            var go = new GameObject("White Smoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = spot + Vector3.up * (_size * FlameRiseMax);
            go.transform.localRotation = Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));

            go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Material mat = null;
            var shader = NightReceivers.Lit;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor(BaseColorId, Smoke);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, new Color(Smoke.r, Smoke.g, Smoke.b) * SmokeGlow);
                UIFactory.MakeTransparent(mat);
                NightReceivers.SetResponse(mat, NightReceivers.SmokeResponse);
                renderer.sharedMaterial = mat;
            }

            _puffs.Add(new Puff
            {
                tr = go.transform,
                mat = mat,
                velocity = new Vector3(SmokeWind + Range(-3f, 3f), Range(SmokeRiseMin, SmokeRiseMax),
                    Range(-4f, 4f)),
                axis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)).normalized,
                spin = Range(-25f, 25f),
                size = _size * Range(SmokeSizeMin, SmokeSizeMax),
            });
        }

        void AnimateSmoke(float dt)
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = _puffs[i];
                puff.age += dt;

                if (puff.tr == null || puff.age >= SmokeLife)
                {
                    if (puff.tr != null) Destroy(puff.tr.gameObject);
                    if (puff.mat != null) Destroy(puff.mat);
                    _puffs.RemoveAt(i);
                    continue;
                }

                float k = puff.age / SmokeLife;
                puff.tr.localPosition += puff.velocity * dt;
                puff.tr.Rotate(puff.axis, puff.spin * dt, Space.World);
                puff.tr.localScale = Vector3.one * (puff.size * (1f + SmokeGrowth * Mathf.Sqrt(k)));

                if (puff.mat == null) continue;
                Color c = Smoke;
                c.a *= 1f - k * k;
                puff.mat.SetColor(BaseColorId, c);
            }
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        void OnDestroy()
        {
            foreach (Flame flame in _flames)
                if (flame.mat != null) Destroy(flame.mat);
            foreach (Puff puff in _puffs)
                if (puff.mat != null) Destroy(puff.mat);
        }
    }
}
