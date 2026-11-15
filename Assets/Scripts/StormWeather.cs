using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class StormWeather : MonoBehaviour
    {
        const string RainClipPath = "Sounds/rain_sound";
        const string ThunderClipPath = "Sounds/thunder_sound";
        const float RainVolume = 0.6f;
        const float ThunderVolume = 1f;
        const int ThunderVoices = 2;

        const float FirstStrikeMin = 2.5f, FirstStrikeMax = 5f;
        const float StrikeGapMin = 6f, StrikeGapMax = 12f;
        const float DoubleChance = 0.25f;
        const float DoubleGapMin = 0.35f, DoubleGapMax = 0.8f;

        const float NearChance = 0.35f;
        const float NearDepthMin = 0.6f, NearDepthMax = 1.3f;
        const float FarDepthMin = 1.8f, FarDepthMax = 3.6f;

        const float BoltSpan = 0.85f;
        const float BoltTop = 1.08f;
        const float BoltBottom = -0.68f;
        const int BoltSteps = 18;
        const float BoltWander = 48f;
        const float BoltTilt = 0.25f;
        const float BoltCore = 0.0065f;
        const float BoltCoreFar = 0.65f;
        const float BoltGlow = 9f;
        const float BoltGlowStrength = 0.22f;
        const int BranchesMin = 2, BranchesMax = 4;
        const int BranchStepsMin = 4, BranchStepsMax = 9;
        const float BranchAngleMin = 25f, BranchAngleMax = 55f;
        const float BranchStep = 0.7f;
        const float BranchCore = 0.55f;
        const float BranchStrength = 0.6f;
        const float BranchGlow = 5f;
        const float BranchGlowStrength = 0.12f;
        const float BoltDecay = 12f;
        const float BoltBoost = 1.3f;
        static readonly Color BoltColor = new Color(1.5f, 1.6f, 1.9f, 1f);

        const float ThunderDelayNear = 0.1f, ThunderDelayFar = 2.5f;
        const float ThunderJitter = 0.15f;
        const float ThunderGainFar = 0.45f;
        const float ThunderPitchFar = 0.86f;

        const float FlashNear = 1f, FlashFar = 0.6f;
        const float FlashSec = 0.95f;
        const float FlashDecay = 7f;
        const float ExposureKick = 0.55f;
        const float SkyKick = 0.6f;
        const float FogKick = 0.45f;
        const float LightKick = 1.3f;
        static readonly Color FlashSky = new Color(0.86f, 0.89f, 0.96f);
        static readonly Color FlashCloud = new Color(0.75f, 0.78f, 0.86f);

        const float RainMargin = 1.12f;
        const float RainStreakSec = 0.035f;
        const float RainPaceJitter = 0.15f;
        const float RainSlant = 0.15f;
        const float ReferenceHeight = 1080f;
        const float MobileShare = 0.5f;
        const float MobileWidth = 0.8f;
        static readonly Color RainColor = new Color(0.80f, 0.83f, 0.88f, 1f);

        struct RainLayer
        {
            public float near, far;
            public int count;
            public float fall, widthPx, alpha;
            public bool mobile;
        }

        static readonly RainLayer[] Layers =
        {
            new RainLayer { near = 0.38f, far = 0.62f, count = 160, fall = 1100f, widthPx = 2.4f, alpha = 0.30f, mobile = true },
            new RainLayer { near = 0.70f, far = 1.55f, count = 700, fall = 1000f, widthPx = 1.6f, alpha = 0.26f, mobile = true },
            new RainLayer { near = 1.60f, far = 3.20f, count = 1400, fall = 950f, widthPx = 1.2f, alpha = 0.16f, mobile = false },
        };

        static readonly int RainTimeId = Shader.PropertyToID("_RainTime");
        static readonly int StormFlashId = Shader.PropertyToID("_StormFlash");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int DepthId = Shader.PropertyToID("_Depth");
        static readonly int MotionId = Shader.PropertyToID("_Motion");
        static readonly int ViewId = Shader.PropertyToID("_View");
        static readonly int EyeId = Shader.PropertyToID("_Eye");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");

        class Bolt
        {
            public Transform tr;
            public MeshRenderer renderer;
            public Mesh mesh;
            public MaterialPropertyBlock block;
        }

        class Strike
        {
            public Bolt bolt;
            public float age;
            public float strength;
        }

        struct Thunder
        {
            public float wait;
            public float gain;
            public float pitch;
        }

        Vector3 _eye;
        float _halfW, _halfH;
        float _distance;
        float _playZ;
        float _scroll;
        ArenaClouds _clouds;
        SoundSystem _sound;

        readonly List<GameObject> _rainLayers = new List<GameObject>();
        readonly List<Mesh> _rainMeshes = new List<Mesh>();
        readonly List<Material> _rainMaterials = new List<Material>();
        int _rainDetail = -1;
        float _rainTime;

        Material _boltMat;
        readonly List<Bolt> _bolts = new List<Bolt>();
        int _nextBolt;
        readonly List<Strike> _strikes = new List<Strike>();
        float _nextStrike;
        float _pendingDouble;

        readonly List<Vector3> _path = new List<Vector3>();
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _tris = new List<int>();

        AudioClip _thunderClip;
        AudioSource[] _thunderSources;
        float[] _thunderGains;
        int _nextVoice;
        readonly List<Thunder> _thunders = new List<Thunder>();

        ColorAdjustments _grade;
        float _baseExposure;
        Material _sky;
        Color _skyTop, _skyHorizon, _skyBottom;
        Color _fogBase;
        Light _sun;
        float _sunBase;
        bool _flashing;

        public static StormWeather Begin(Camera cam, Vector3 eye, float halfViewWidth,
            float halfViewHeight, float cameraDistance, float playPlaneZ, ArenaClouds clouds,
            float scrollSpeed)
        {
            var storm = new GameObject("Storm Weather").AddComponent<StormWeather>();
            storm._eye = cam != null ? cam.transform.position : eye;
            storm._halfW = halfViewWidth;
            storm._halfH = halfViewHeight;
            storm._distance = cameraDistance;
            storm._playZ = playPlaneZ;
            storm._clouds = clouds;
            storm._scroll = scrollSpeed;
            storm.Build();
            return storm;
        }

        public void Listen(SoundSystem sound)
        {
            _sound = sound;
            if (sound == null) return;

            sound.AddAmbient(Resources.Load<AudioClip>(RainClipPath), RainVolume);

            _thunderSources = new AudioSource[ThunderVoices];
            _thunderGains = new float[ThunderVoices];
            for (int i = 0; i < ThunderVoices; i++) _thunderSources[i] = sound.AddShotSource();
        }

        void Build()
        {
            CaptureScene();
            BuildRain();
            BuildBolts();

            _thunderClip = Resources.Load<AudioClip>(ThunderClipPath);
            if (_thunderClip != null) _thunderClip.LoadAudioData();

            _nextStrike = Random.Range(FirstStrikeMin, FirstStrikeMax);
            Shader.SetGlobalFloat(StormFlashId, 0f);
            Shader.SetGlobalFloat(RainTimeId, 0f);
            GraphicsOptions.Changed += OnGraphicsChanged;
        }

        void CaptureScene()
        {
            _grade = CoastSky.Grade;
            if (_grade != null) _baseExposure = _grade.postExposure.value;

            _sky = RenderSettings.skybox;
            if (_sky != null && _sky.HasProperty(TopColorId))
            {
                _skyTop = _sky.GetColor(TopColorId);
                _skyHorizon = _sky.GetColor(HorizonColorId);
                _skyBottom = _sky.GetColor(BottomColorId);
            }
            else _sky = null;

            _fogBase = RenderSettings.fogColor;
            _sun = RenderSettings.sun;
            if (_sun != null) _sunBase = _sun.intensity;
        }

        static float DetailShare =>
            GraphicsOptions.GroundDetail == 0 ? 0.4f : GraphicsOptions.GroundDetail == 1 ? 0.7f : 1f;

        void BuildRain()
        {
            _rainDetail = GraphicsOptions.GroundDetail;

            var shader = Shader.Find("Hidden/RainStreaks");
            if (shader == null)
            {
                Debug.LogWarning("StormWeather: Hidden/RainStreaks not found; no rain.");
                return;
            }

            bool mobile = GraphicsOptions.Mobile;
            float share = DetailShare * (mobile ? MobileShare : 1f);
            float tanX = _halfW / _distance;
            float tanY = _halfH / _distance;

            foreach (RainLayer layer in Layers)
            {
                if (mobile && !layer.mobile) continue;

                int count = Mathf.Max(1, Mathf.RoundToInt(layer.count * share));
                float near = layer.near * _distance;
                float far = layer.far * _distance;
                float widthPx = layer.widthPx * (mobile ? MobileWidth : 1f);

                var mat = new Material(shader) { name = "Rain (runtime)" };
                mat.SetColor(ColorId, RainColor);
                mat.SetVector(DepthId, new Vector4(near, far, widthPx / ReferenceHeight * 2f * tanY,
                    layer.alpha));
                mat.SetVector(MotionId, new Vector4(-_scroll - layer.fall * RainSlant, -layer.fall,
                    RainStreakSec, RainPaceJitter));
                mat.SetVector(ViewId, new Vector4(tanX, tanY, RainMargin, 0f));
                mat.SetVector(EyeId, _eye);
                _rainMaterials.Add(mat);

                Mesh mesh = StreakMesh(count, far, tanX, tanY);
                _rainMeshes.Add(mesh);

                var go = new GameObject("Rain Layer");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(_eye.x, _eye.y, _eye.z + (near + far) * 0.5f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                Quiet(renderer);
                _rainLayers.Add(go);
            }
        }

        static void Quiet(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }

        static Mesh StreakMesh(int count, float far, float tanX, float tanY)
        {
            var vertices = new Vector3[count * 4];
            var seeds = new Vector4[count * 4];
            var indices = new int[count * 6];

            for (int i = 0; i < count; i++)
            {
                var seed = new Vector4(Random.value, Random.value, Random.value, Random.value);
                int v = i * 4;
                vertices[v] = new Vector3(-0.5f, 0f, 0f);
                vertices[v + 1] = new Vector3(0.5f, 0f, 0f);
                vertices[v + 2] = new Vector3(0.5f, 1f, 0f);
                vertices[v + 3] = new Vector3(-0.5f, 1f, 0f);
                seeds[v] = seed;
                seeds[v + 1] = seed;
                seeds[v + 2] = seed;
                seeds[v + 3] = seed;

                int t = i * 6;
                indices[t] = v;
                indices[t + 1] = v + 1;
                indices[t + 2] = v + 2;
                indices[t + 3] = v;
                indices[t + 4] = v + 2;
                indices[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "Rain Streaks" };
            if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.SetUVs(0, seeds);
            mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero,
                new Vector3(4f * far * tanX, 4f * far * tanY, 2f * far));
            mesh.UploadMeshData(true);
            return mesh;
        }

        void ClearRain()
        {
            foreach (GameObject go in _rainLayers)
                if (go != null) Destroy(go);
            foreach (Mesh mesh in _rainMeshes)
                if (mesh != null) Destroy(mesh);
            foreach (Material mat in _rainMaterials)
                if (mat != null) Destroy(mat);

            _rainLayers.Clear();
            _rainMeshes.Clear();
            _rainMaterials.Clear();
        }

        void OnGraphicsChanged()
        {
            if (GraphicsOptions.GroundDetail == _rainDetail) return;
            ClearRain();
            BuildRain();
        }

        void BuildBolts()
        {
            var shader = Shader.Find("Hidden/LightningBolt");
            if (shader == null)
            {
                Debug.LogWarning("StormWeather: Hidden/LightningBolt not found; flashes only.");
                return;
            }

            _boltMat = new Material(shader) { name = "Lightning (runtime)" };
            _boltMat.SetColor(ColorId, BoltColor);

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Lightning Bolt");
                go.transform.SetParent(transform, false);

                var mesh = new Mesh { name = "Lightning Bolt" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _boltMat;
                Quiet(renderer);
                renderer.enabled = false;

                _bolts.Add(new Bolt
                {
                    tr = go.transform,
                    renderer = renderer,
                    mesh = mesh,
                    block = new MaterialPropertyBlock(),
                });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _rainTime += dt;
            Shader.SetGlobalFloat(RainTimeId, _rainTime);
            if (dt <= 0f) return;

            TickStrikes(dt);
            TickThunder(dt);
        }

        void TickStrikes(float dt)
        {
            _nextStrike -= dt;
            if (_nextStrike <= 0f)
            {
                Fire();
                _nextStrike = Random.Range(StrikeGapMin, StrikeGapMax);
                if (Random.value < DoubleChance) _pendingDouble = Random.Range(DoubleGapMin, DoubleGapMax);
            }

            if (_pendingDouble > 0f)
            {
                _pendingDouble -= dt;
                if (_pendingDouble <= 0f) Fire();
            }

            float flash = 0f;
            for (int i = _strikes.Count - 1; i >= 0; i--)
            {
                Strike strike = _strikes[i];
                strike.age += dt;
                flash = Mathf.Max(flash, Envelope(strike.age, FlashDecay) * strike.strength);

                if (strike.bolt != null)
                {
                    strike.bolt.tr.position += Vector3.left * (_scroll * dt);
                    strike.bolt.block.SetFloat(AlphaId,
                        Mathf.Min(1f, Envelope(strike.age, BoltDecay) * BoltBoost));
                    strike.bolt.renderer.SetPropertyBlock(strike.bolt.block);
                }

                if (strike.age < FlashSec) continue;
                if (strike.bolt != null) strike.bolt.renderer.enabled = false;
                _strikes.RemoveAt(i);
            }

            ApplyFlash(flash);
        }

        static float Envelope(float t, float decay)
        {
            if (t < 0f) return 0f;
            if (t < 0.05f) return t / 0.05f;
            if (t < 0.11f) return Mathf.Lerp(1f, 0.3f, (t - 0.05f) / 0.06f);
            if (t < 0.16f) return Mathf.Lerp(0.3f, 0.85f, (t - 0.11f) / 0.05f);
            return 0.85f * Mathf.Exp(-(t - 0.16f) * decay);
        }

        void Fire()
        {
            bool near = Random.value < NearChance;
            float depth = near
                ? Random.Range(NearDepthMin, NearDepthMax)
                : Random.Range(FarDepthMin, FarDepthMax);
            float k = Mathf.InverseLerp(NearDepthMin, FarDepthMax, depth);

            Bolt bolt = null;
            if (_bolts.Count > 0)
            {
                bolt = _bolts[_nextBolt];
                _nextBolt = (_nextBolt + 1) % _bolts.Count;
                foreach (Strike other in _strikes)
                    if (other.bolt == bolt) other.bolt = null;
                Shape(bolt, depth, k);
            }

            _strikes.Add(new Strike { bolt = bolt, strength = Mathf.Lerp(FlashNear, FlashFar, k) });
            QueueThunder(k);
        }

        public void Hold()
        {
            enabled = false;
            ClearRain();
        }

        public void PoseBolt(int index, Vector3 top, float height, float far, float alpha,
            Quaternion facing)
        {
            if (index < 0 || index >= _bolts.Count) return;

            Bolt bolt = _bolts[index];
            Shape(bolt, top, height / (BoltTop - BoltBottom), Mathf.Clamp01(far));
            bolt.tr.rotation = facing;
            bolt.block.SetFloat(AlphaId, alpha);
            bolt.renderer.SetPropertyBlock(bolt.block);
        }

        public void HoldFlash(float strength) => ApplyFlash(strength);

        public int BoltCount => _bolts.Count;

        void Shape(Bolt bolt, float depth, float k)
        {
            float ratio = 1f + depth;
            float unit = _halfH * ratio;
            float x = _eye.x + Random.Range(-BoltSpan, BoltSpan) * _halfW * ratio;
            var origin = new Vector3(x, _eye.y + BoltTop * unit, _playZ + depth * _distance);
            Shape(bolt, origin, unit, k);
        }

        void Shape(Bolt bolt, Vector3 origin, float unit, float k)
        {
            float height = (BoltTop - BoltBottom) * unit;
            float step = height / BoltSteps;
            float core = BoltCore * unit * Mathf.Lerp(1f, BoltCoreFar, k);

            _verts.Clear();
            _uvs.Clear();
            _colors.Clear();
            _tris.Clear();

            Vector2 heading = new Vector2(Random.Range(-BoltTilt, BoltTilt), -1f).normalized;
            Channel(origin, heading, step, BoltSteps);
            var main = new List<Vector3>(_path);
            AddStrip(main, origin, core * BoltGlow, BoltGlowStrength, 2.2f, 0.6f);
            AddStrip(main, origin, core, 1f, 0.5f, 0.5f);

            int branches = Random.Range(BranchesMin, BranchesMax + 1);
            for (int b = 0; b < branches; b++)
            {
                int from = Random.Range(3, Mathf.Max(4, Mathf.RoundToInt(main.Count * 0.7f)));
                float angle = Random.Range(BranchAngleMin, BranchAngleMax) * (Random.value < 0.5f ? -1f : 1f);
                Channel(main[from], Rotate(heading, angle), step * BranchStep,
                    Random.Range(BranchStepsMin, BranchStepsMax + 1));
                AddStrip(_path, origin, core * BranchGlow, BranchGlowStrength, 2.2f, 0.2f);
                AddStrip(_path, origin, core * BranchCore, BranchStrength, 0.5f, 0.2f);
            }

            bolt.mesh.Clear();
            bolt.mesh.SetVertices(_verts);
            bolt.mesh.SetUVs(0, _uvs);
            bolt.mesh.SetColors(_colors);
            bolt.mesh.SetTriangles(_tris, 0);
            bolt.mesh.RecalculateBounds();

            bolt.tr.position = origin;
            bolt.block.SetFloat(AlphaId, 0f);
            bolt.renderer.SetPropertyBlock(bolt.block);
            bolt.renderer.enabled = true;
        }

        void Channel(Vector3 start, Vector2 heading, float step, int steps)
        {
            _path.Clear();
            _path.Add(start);

            Vector3 p = start;
            for (int i = 0; i < steps; i++)
            {
                Vector2 d = Rotate(heading, Random.Range(-BoltWander, BoltWander));
                p += new Vector3(d.x, d.y, 0f) * (step * Random.Range(0.6f, 1.3f));
                _path.Add(p);
            }
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        void AddStrip(List<Vector3> path, Vector3 origin, float width, float strength, float sharp,
            float taper)
        {
            int n = path.Count;
            if (n < 2) return;

            int start = _verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                var side = new Vector3(-dir.y, dir.x, 0f);
                float t = i / (float)(n - 1);
                float half = width * Mathf.Lerp(1f, taper, t) * 0.5f;
                Vector3 c = path[i] - origin;

                _verts.Add(c - side * half);
                _verts.Add(c + side * half);
                _uvs.Add(new Vector2(0f, sharp));
                _uvs.Add(new Vector2(1f, sharp));

                var col = new Color(1f, 1f, 1f, strength * Mathf.Lerp(1f, 0.5f, t));
                _colors.Add(col);
                _colors.Add(col);

                if (i == 0) continue;
                int v = start + i * 2;
                _tris.Add(v - 2);
                _tris.Add(v - 1);
                _tris.Add(v + 1);
                _tris.Add(v - 2);
                _tris.Add(v + 1);
                _tris.Add(v);
            }
        }

        void ApplyFlash(float f)
        {
            if (f <= 0.001f)
            {
                if (!_flashing) return;
                f = 0f;
                _flashing = false;
            }
            else _flashing = true;

            Shader.SetGlobalFloat(StormFlashId, f);
            if (_grade != null) _grade.postExposure.value = _baseExposure + f * ExposureKick;

            if (_sky != null)
            {
                _sky.SetColor(TopColorId, Color.Lerp(_skyTop, FlashSky, f * SkyKick));
                _sky.SetColor(HorizonColorId, Color.Lerp(_skyHorizon, FlashSky, f * SkyKick));
                _sky.SetColor(BottomColorId, Color.Lerp(_skyBottom, FlashSky, f * SkyKick));
            }

            RenderSettings.fogColor = Color.Lerp(_fogBase, FlashSky, f * FogKick);
            if (_clouds != null) _clouds.Flash(FlashCloud * f);
            if (_sun != null) _sun.intensity = _sunBase + f * LightKick;
        }

        void QueueThunder(float k)
        {
            if (_thunderClip == null) return;

            float wait = Mathf.Lerp(ThunderDelayNear, ThunderDelayFar, k)
                         + Random.Range(-ThunderJitter, ThunderJitter);
            _thunders.Add(new Thunder
            {
                wait = Mathf.Max(0f, wait),
                gain = Mathf.Lerp(1f, ThunderGainFar, k),
                pitch = Mathf.Lerp(1f, ThunderPitchFar, k) * Random.Range(0.96f, 1.04f),
            });
        }

        void TickThunder(float dt)
        {
            for (int i = _thunders.Count - 1; i >= 0; i--)
            {
                Thunder thunder = _thunders[i];
                thunder.wait -= dt;
                if (thunder.wait > 0f)
                {
                    _thunders[i] = thunder;
                    continue;
                }

                _thunders.RemoveAt(i);
                PlayThunder(thunder);
            }

            if (_sound == null || _thunderSources == null) return;
            float gain = _sound.SceneGain;
            for (int i = 0; i < _thunderSources.Length; i++)
                if (_thunderSources[i] != null) _thunderSources[i].volume = _thunderGains[i] * gain;
        }

        void PlayThunder(Thunder thunder)
        {
            if (_sound == null || _thunderSources == null || _thunderClip == null) return;

            int voice = _nextVoice;
            _nextVoice = (_nextVoice + 1) % _thunderSources.Length;
            AudioSource source = _thunderSources[voice];
            if (source == null) return;

            _thunderGains[voice] = ThunderVolume * thunder.gain;
            source.Stop();
            source.clip = _thunderClip;
            source.pitch = thunder.pitch;
            source.volume = _thunderGains[voice] * _sound.SceneGain;
            source.Play();
        }

        void OnDestroy()
        {
            GraphicsOptions.Changed -= OnGraphicsChanged;
            Shader.SetGlobalFloat(StormFlashId, 0f);
            ClearRain();

            foreach (Bolt bolt in _bolts)
                if (bolt.mesh != null) Destroy(bolt.mesh);
            _bolts.Clear();
            if (_boltMat != null) Destroy(_boltMat);
        }
    }
}
