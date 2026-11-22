using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class GroundFire : MonoBehaviour
    {
        const float SpreadSec = 0.7f;
        const float GrowSec = 0.3f;

        const float HeightMin = 1.2f, HeightMax = 1.8f;
        const float SpotJitter = 0.15f;
        const int ShellBlobs = 4, CoreBlobs = 3;
        const float ShellWidth = 0.84f, CoreWidth = 0.45f;
        const float CoreHeight = 0.62f;
        const float BaseRise = 0.18f, TipRise = 0.7f;
        const float Taper = 0.72f;
        const float Stretch = 0.87f;
        const float SwayHz = 1.1f, SwayAmount = 0.16f, CoreSway = 0.7f;
        const float StretchHz = 2.3f, StretchDepth = 0.16f;
        const float FlickerHzMin = 6f, FlickerHzMax = 12f, FlickerDepth = 0.22f;
        const float ShadeHz = 4.2f;
        const float ShellAlpha = 0.62f;
        const float ShellEmission = 2.2f, CoreEmission = 3.4f;
        const int ShellShades = 3, CoreShades = 2;

        const int SparkPool = 30;
        const float SparkEveryMin = 0.25f, SparkEveryMax = 0.7f;
        const float SparkLifeMin = 0.7f, SparkLifeMax = 1.4f;
        const float SparkRiseMin = 2f, SparkRiseMax = 4f;
        const float SparkDrift = 0.7f;
        const float SparkWobble = 2.5f;
        const float SparkSizeMin = 0.07f, SparkSizeMax = 0.13f;
        const float SparkEmission = 4f;

        const int PuffPool = 30;
        const float PuffEveryMin = 0.35f, PuffEveryMax = 0.8f;
        const float PuffLifeMin = 1.6f, PuffLifeMax = 2.4f;
        const float PuffRiseMin = 0.8f, PuffRiseMax = 1.4f;
        const float PuffDrift = 0.35f;
        const float PuffSizeMin = 0.18f, PuffSizeMax = 0.28f;
        const float PuffGrowth = 2.5f;
        const float PuffAlpha = 0.5f;
        const float PuffSpin = 30f;

        const float LightRange = 1.6f;
        const float LightIntensity = 3f;
        const float LightLift = 0.8f;
        const int LightPriority = 40;
        static readonly Vector2 LightFlicker = new Vector2(0.22f, 2.5f);
        static readonly Color LightColor = new Color(1f, 0.55f, 0.2f);

        static readonly Color ShellDeep = new Color(1f, 0.22f, 0.03f);
        static readonly Color ShellHot = new Color(1f, 0.5f, 0.08f);
        static readonly Color CoreLow = new Color(1f, 0.78f, 0.3f);
        static readonly Color CoreHot = new Color(1f, 0.95f, 0.7f);
        static readonly Color SparkColor = new Color(1f, 0.75f, 0.3f);
        static readonly Color PuffColor = new Color(0.12f, 0.11f, 0.1f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        class Tongue
        {
            public Vector3 foot;
            public float height, at, phase, rate, swayA, swayB;
            public Transform[] shell, core;
            public float[] shellHz, coreHz;
            public float sparkIn, puffIn;
        }

        class Mote
        {
            public Transform tr;
            public MeshRenderer renderer;
            public Vector3 velocity, axis;
            public float age, life, size;
            public bool live;
        }

        readonly List<Tongue> _tongues = new List<Tongue>();
        readonly List<Mote> _sparks = new List<Mote>();
        readonly List<Mote> _puffs = new List<Mote>();
        readonly Material[] _shells = new Material[ShellShades];
        readonly Material[] _cores = new Material[CoreShades];
        readonly float[] _shellPhase = new float[ShellShades];
        readonly float[] _corePhase = new float[CoreShades];
        Material _spark, _puff;
        MaterialPropertyBlock _block;
        System.Random _rng;
        NightLight2D _light;
        float _metre;
        float _lightRange;
        float _age;

        public static GroundFire Begin(Transform parent, Vector3 centre, IReadOnlyList<Vector3> spots,
            float metre, float reach, int seed)
        {
            var go = new GameObject("Ground Fire");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.identity;

            var fire = go.AddComponent<GroundFire>();
            fire._rng = new System.Random(seed);
            fire._metre = Mathf.Max(0.01f, metre);
            fire.Build(spots, Mathf.Max(1f, reach));
            return fire;
        }

        void Build(IReadOnlyList<Vector3> spots, float reach)
        {
            _block = new MaterialPropertyBlock();
            Shader shader = NightReceivers.Lit;
            if (shader != null)
            {
                for (int i = 0; i < ShellShades; i++)
                {
                    _shells[i] = Glowing(shader);
                    UIFactory.MakeTransparent(_shells[i]);
                    _shellPhase[i] = Range(0f, Mathf.PI * 2f);
                }
                for (int i = 0; i < CoreShades; i++)
                {
                    _cores[i] = Glowing(shader);
                    _corePhase[i] = Range(0f, Mathf.PI * 2f);
                }

                _spark = Glowing(shader);
                _spark.SetColor(BaseColorId, Firelight.Warm(SparkColor));
                _spark.SetColor(EmissionColorId, Firelight.Warm(SparkColor) * SparkEmission);

                _puff = new Material(shader);
                _puff.SetColor(BaseColorId, PuffColor);
                UIFactory.MakeTransparent(_puff);
                NightReceivers.SetResponse(_puff, NightReceivers.SmokeResponse);
            }

            float far = 1f;
            foreach (Vector3 spot in spots) far = Mathf.Max(far, Flat(spot - transform.position));
            foreach (Vector3 spot in spots)
                _tongues.Add(Kindle(spot, Flat(spot - transform.position) / far * SpreadSec));

            var lamp = new GameObject("Ground Fire Light");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up * (LightLift * _metre);
            _light = lamp.AddComponent<NightLight2D>();
            _light.type = NightLightType.Point;
            _light.color = LightColor;
            _light.intensity = 0f;
            _light.flicker = LightFlicker;
            _light.priority = LightPriority;
            _lightRange = reach * LightRange;
            _light.range = _lightRange * 0.3f;
        }

        static Material Glowing(Shader shader)
        {
            var mat = new Material(shader);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

        Tongue Kindle(Vector3 spot, float at)
        {
            var tongue = new Tongue
            {
                foot = spot + new Vector3(Range(-SpotJitter, SpotJitter), 0f, Range(-SpotJitter, SpotJitter)) * _metre,
                height = Range(HeightMin, HeightMax) * _metre,
                at = at,
                phase = Range(0f, Mathf.PI * 2f),
                rate = Range(0.8f, 1.25f),
                swayA = Range(0f, 100f),
                swayB = Range(0f, 100f),
                shell = new Transform[ShellBlobs],
                core = new Transform[CoreBlobs],
                shellHz = new float[ShellBlobs],
                coreHz = new float[CoreBlobs],
                sparkIn = Range(SparkEveryMin, SparkEveryMax),
                puffIn = Range(PuffEveryMin, PuffEveryMax),
            };

            for (int i = 0; i < ShellBlobs; i++)
            {
                tongue.shell[i] = Blob("Flame Shell", _shells[_rng.Next(ShellShades)]);
                tongue.shellHz[i] = Range(FlickerHzMin, FlickerHzMax);
            }
            for (int i = 0; i < CoreBlobs; i++)
            {
                tongue.core[i] = Blob("Flame Core", _cores[_rng.Next(CoreShades)]);
                tongue.coreHz[i] = Range(FlickerHzMin, FlickerHzMax);
            }
            return tongue;
        }

        Transform Blob(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));
            go.transform.localScale = Vector3.zero;

            go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (mat != null) renderer.sharedMaterial = mat;
            return go.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float now = Time.time;

            foreach (Tongue tongue in _tongues)
            {
                float grow = Mathf.Clamp01((_age - tongue.at) / GrowSec);
                if (grow <= 0f) continue;

                float g = Mathf.SmoothStep(0f, 1f, grow);
                float stretch = 1f + StretchDepth * Mathf.Sin(now * StretchHz * tongue.rate + tongue.phase);
                float sway = SwayAmount * tongue.height;
                var lean = new Vector2(
                    Mathf.PerlinNoise(now * SwayHz * tongue.rate + tongue.swayA, tongue.swayB) * 2f - 1f,
                    Mathf.PerlinNoise(tongue.swayB, now * SwayHz * tongue.rate + tongue.swayA) * 2f - 1f) * sway;

                float height = tongue.height * stretch * g;
                Stack(tongue.shell, tongue.shellHz, tongue.foot, height, ShellWidth * tongue.height * g,
                    lean, now, tongue.phase);
                Stack(tongue.core, tongue.coreHz, tongue.foot, height * CoreHeight,
                    CoreWidth * tongue.height * g, lean * CoreSway, now, tongue.phase + 1.3f);

                if (grow < 1f) continue;

                Vector3 tip = tongue.foot + new Vector3(lean.x, height * (BaseRise + TipRise), lean.y);
                tongue.sparkIn -= dt;
                if (tongue.sparkIn <= 0f)
                {
                    tongue.sparkIn = Range(SparkEveryMin, SparkEveryMax);
                    Spark(tip);
                }
                tongue.puffIn -= dt;
                if (tongue.puffIn <= 0f)
                {
                    tongue.puffIn = Range(PuffEveryMin, PuffEveryMax);
                    Puff(tip);
                }
            }

            Shade(now);
            MoveSparks(dt, now);
            MovePuffs(dt);

            if (_light != null)
            {
                float spread = Mathf.Clamp01(_age / SpreadSec);
                _light.intensity = LightIntensity * Mathf.SmoothStep(0.25f, 1f, spread);
                _light.range = _lightRange * Mathf.Lerp(0.3f, 1f, spread);
            }
        }

        void Stack(Transform[] blobs, float[] hz, Vector3 foot, float height, float width, Vector2 lean,
            float now, float phase)
        {
            int n = blobs.Length;
            for (int i = 0; i < n; i++)
            {
                if (blobs[i] == null) continue;

                float k = n > 1 ? (float)i / (n - 1) : 0f;
                float flicker = 1f + FlickerDepth * Mathf.Sin(now * hz[i] + phase + i * 1.7f);
                float r = width * Mathf.Lerp(1f, 1f - Taper, k) * flicker;
                float bend = Mathf.Pow(k, 1.5f);
                blobs[i].position = foot + new Vector3(lean.x * bend, height * (BaseRise + TipRise * k), lean.y * bend);
                blobs[i].localScale = new Vector3(r, r * Stretch, r);
            }
        }

        void Shade(float now)
        {
            for (int i = 0; i < ShellShades; i++)
            {
                if (_shells[i] == null) continue;
                float w = 0.5f + 0.5f * Mathf.Sin(now * ShadeHz + _shellPhase[i]);
                Color c = Color.Lerp(Firelight.Warm(ShellDeep), Firelight.Warm(ShellHot), w);
                _shells[i].SetColor(EmissionColorId, c * ShellEmission);
                c.a = ShellAlpha;
                _shells[i].SetColor(BaseColorId, c);
            }
            for (int i = 0; i < CoreShades; i++)
            {
                if (_cores[i] == null) continue;
                float w = 0.5f + 0.5f * Mathf.Sin(now * ShadeHz * 1.6f + _corePhase[i]);
                Color c = Color.Lerp(Firelight.Warm(CoreLow), Firelight.Warm(CoreHot), w);
                _cores[i].SetColor(BaseColorId, c);
                _cores[i].SetColor(EmissionColorId, c * CoreEmission);
            }
        }

        void Spark(Vector3 at)
        {
            Mote mote = Take(_sparks, SparkPool, "Spark", _spark);
            if (mote == null) return;

            mote.tr.position = at;
            mote.velocity = new Vector3(Range(-SparkDrift, SparkDrift), Range(SparkRiseMin, SparkRiseMax),
                Range(-SparkDrift, SparkDrift)) * _metre;
            mote.life = Range(SparkLifeMin, SparkLifeMax);
            mote.size = Range(SparkSizeMin, SparkSizeMax) * _metre;
            mote.axis = new Vector3(Range(0f, 100f), Range(0f, 100f), 0f);
        }

        void Puff(Vector3 at)
        {
            Mote mote = Take(_puffs, PuffPool, "Flame Smoke", _puff);
            if (mote == null) return;

            mote.tr.position = at;
            mote.tr.rotation = Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));
            mote.velocity = new Vector3(Range(-PuffDrift, PuffDrift), Range(PuffRiseMin, PuffRiseMax),
                Range(-PuffDrift, PuffDrift)) * _metre;
            mote.life = Range(PuffLifeMin, PuffLifeMax);
            mote.size = Range(PuffSizeMin, PuffSizeMax) * _metre;
            mote.axis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)).normalized;
        }

        Mote Take(List<Mote> pool, int cap, string name, Material mat)
        {
            foreach (Mote m in pool)
                if (!m.live) return Wake(m);
            if (pool.Count >= cap) return null;

            Transform tr = Blob(name, mat);
            var mote = new Mote { tr = tr, renderer = tr.GetComponent<MeshRenderer>() };
            pool.Add(mote);
            return Wake(mote);
        }

        static Mote Wake(Mote mote)
        {
            mote.live = true;
            mote.age = 0f;
            mote.tr.gameObject.SetActive(true);
            return mote;
        }

        static void Sleep(Mote mote)
        {
            mote.live = false;
            mote.tr.gameObject.SetActive(false);
        }

        void MoveSparks(float dt, float now)
        {
            foreach (Mote mote in _sparks)
            {
                if (!mote.live) continue;
                mote.age += dt;
                if (mote.age >= mote.life) { Sleep(mote); continue; }

                float k = mote.age / mote.life;
                Vector3 wobble = new Vector3(
                    Mathf.PerlinNoise(now * SparkWobble, mote.axis.x) * 2f - 1f, 0f,
                    Mathf.PerlinNoise(mote.axis.y, now * SparkWobble) * 2f - 1f) * (SparkDrift * _metre);
                mote.tr.position += (mote.velocity + wobble) * dt;
                mote.tr.localScale = Vector3.one * (mote.size * (1f - k));
            }
        }

        void MovePuffs(float dt)
        {
            foreach (Mote mote in _puffs)
            {
                if (!mote.live) continue;
                mote.age += dt;
                if (mote.age >= mote.life) { Sleep(mote); continue; }

                float k = mote.age / mote.life;
                mote.tr.position += mote.velocity * dt;
                mote.tr.Rotate(mote.axis, PuffSpin * dt, Space.World);
                mote.tr.localScale = Vector3.one * (mote.size * (1f + PuffGrowth * Mathf.Sqrt(k)));

                Color c = PuffColor;
                c.a = PuffAlpha * (1f - k) * Mathf.Clamp01(k * 6f);
                mote.renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                mote.renderer.SetPropertyBlock(_block);
            }
        }

        void OnDestroy()
        {
            foreach (Material mat in _shells) if (mat != null) Destroy(mat);
            foreach (Material mat in _cores) if (mat != null) Destroy(mat);
            if (_spark != null) Destroy(_spark);
            if (_puff != null) Destroy(_puff);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    }
}
