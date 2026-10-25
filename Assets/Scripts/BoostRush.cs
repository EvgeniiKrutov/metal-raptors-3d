using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class BoostRush : MonoBehaviour
    {
        const float FadeIn = 6f, FadeOut = 2.5f;
        const float NightDim = 0.5f;

        const int PuffCount = 22;
        const float ExhaustBack = 0.72f;
        const float PuffSeconds = 0.9f;
        const float PuffGapMin = 0.035f, PuffGapMax = 0.055f;
        const float PuffTaper = 0.55f;
        const float PuffLifeMin = 0.6f, PuffLifeMax = 0.8f;
        const float PuffStart = 0.24f, PuffEndMin = 0.65f, PuffEndMax = 0.85f;
        const float PuffDriftMin = 1.6f, PuffDriftMax = 2.2f;
        const float PuffRise = 0.12f;
        const float PuffSpread = 0.06f;
        const float PuffDepth = 0.1f;
        const float PuffOpacity = 0.8f;
        const float PuffFadeIn = 0.05f;
        const float PuffSpinMin = 80f, PuffSpinMax = 240f;

        static readonly Color PuffColor = new Color(0.86f, 0.85f, 0.83f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static Material _puffMaterial;

        public float Level { get; private set; }

        public bool Hidden { get; set; }

        public SoundSystem Sound { get; set; }

        class Puff
        {
            public Transform tr;
            public Renderer renderer;
            public Vector3 velocity, axis;
            public float age, life, start, end, spin;
            public bool live;
        }

        readonly List<Puff> _puffs = new List<Puff>();
        MaterialPropertyBlock _block;
        PlaneBoost _boost;
        CubeController _plane;
        BoostSpeedLines _screen;
        Vector3 _exhaustLocal;
        float _length;
        float _puffing, _nextPuff;
        bool _running;

        public static BoostRush Mount(GameObject body, PlaneBoost boost, CubeController plane,
            float planeLength, Vector3 noseLocal, Vector3? engineLocal, bool night)
        {
            var rush = body.AddComponent<BoostRush>();
            rush._boost = boost;
            rush._plane = plane;
            rush._length = Mathf.Max(1f, planeLength);
            rush._exhaustLocal = engineLocal ?? new Vector3(noseLocal.x * ExhaustBack, noseLocal.y, 0f);
            rush._screen = new BoostSpeedLines(night ? NightDim : 1f);
            rush.BuildPuffs();
            return rush;
        }

        void BuildPuffs()
        {
            _block = new MaterialPropertyBlock();
            Material material = PuffMaterial();
            if (material == null) return;

            for (int i = 0; i < PuffCount; i++)
            {
                var go = new GameObject("Boost Puff", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();

                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                _puffs.Add(new Puff { tr = go.transform, renderer = renderer });
            }
        }

        void LateUpdate()
        {
            bool hidden = Hidden || GameMenu.IsOpen || LevelBriefing.IsOpen;
            bool running = _boost != null && _boost.IsRunning;
            if (running && !_running && !hidden) Kick();
            _running = running;

            float dt = Time.deltaTime;
            Level = hidden
                ? 0f
                : Mathf.MoveTowards(Level, running ? 1f : 0f, (running ? FadeIn : FadeOut) * dt);

            float heading = _plane != null ? _plane.Heading : 0f;
            _screen.Tick(dt, heading, Level, running && !hidden);
            TickPuffs(dt);

            if (Sound != null) Sound.WindSwell = Level;
        }

        void Kick()
        {
            _puffing = PuffSeconds;
            _nextPuff = 0f;
        }

        void EmitPuffs(float dt)
        {
            if (_puffing <= 0f) return;

            _puffing -= dt;
            _nextPuff -= dt;
            while (_nextPuff <= 0f && _puffing > 0f)
            {
                _nextPuff += Random.Range(PuffGapMin, PuffGapMax);
                Launch(Mathf.Lerp(PuffTaper, 1f, _puffing / PuffSeconds));
            }
        }

        void Launch(float size)
        {
            Puff puff = null;
            foreach (Puff p in _puffs)
                if (!p.live) { puff = p; break; }
            if (puff == null) return;

            puff.age = 0f;
            puff.life = Random.Range(PuffLifeMin, PuffLifeMax);
            puff.start = PuffStart * size * _length;
            puff.end = Random.Range(PuffEndMin, PuffEndMax) * size * _length;
            puff.velocity = new Vector3(-Random.Range(PuffDriftMin, PuffDriftMax), PuffRise, 0f) * _length;
            puff.axis = Random.onUnitSphere;
            puff.spin = Random.Range(PuffSpinMin, PuffSpinMax) * (Random.value < 0.5f ? -1f : 1f);
            puff.tr.localPosition = _exhaustLocal + new Vector3(0f,
                Random.Range(-PuffSpread, PuffSpread), Random.Range(-PuffDepth, PuffDepth)) * _length;
            puff.tr.localRotation = Random.rotation;
            puff.live = true;
        }

        void TickPuffs(float dt)
        {
            EmitPuffs(dt);
            foreach (Puff puff in _puffs)
            {
                if (!puff.live) continue;

                puff.age += dt;
                if (puff.age >= puff.life)
                {
                    puff.live = false;
                    puff.renderer.enabled = false;
                    continue;
                }

                float t = puff.age / puff.life;
                puff.tr.localPosition += puff.velocity * dt;
                puff.tr.Rotate(puff.axis, puff.spin * dt, Space.Self);
                puff.tr.localScale = Vector3.one * Mathf.Lerp(puff.start, puff.end, 1f - (1f - t) * (1f - t));

                Color color = PuffColor;
                color.a = PuffOpacity * Mathf.Clamp01(puff.age / PuffFadeIn) * Mathf.Pow(1f - t, 1.5f);
                _block.SetColor(BaseColorId, color);
                puff.renderer.SetPropertyBlock(_block);
                puff.renderer.enabled = true;
            }
        }

        static Material PuffMaterial()
        {
            if (_puffMaterial != null) return _puffMaterial;

            Shader lit = NightReceivers.Lit;
            if (lit == null) return null;

            _puffMaterial = new Material(lit) { name = "Boost Puff" };
            _puffMaterial.SetColor(BaseColorId, PuffColor);
            _puffMaterial.SetFloat("_Smoothness", 0f);
            UIFactory.MakeTransparent(_puffMaterial);
            NightReceivers.SetResponse(_puffMaterial, NightReceivers.SmokeResponse);
            return _puffMaterial;
        }

        void OnDisable()
        {
            Level = 0f;
            _running = false;
            _puffing = 0f;
            if (Sound != null) Sound.WindSwell = 0f;
            _screen?.Clear();
            foreach (Puff puff in _puffs)
            {
                puff.live = false;
                if (puff.renderer != null) puff.renderer.enabled = false;
            }
        }

        void OnDestroy()
        {
            if (Sound != null) Sound.WindSwell = 0f;
            _screen?.Release();
        }
    }
}
