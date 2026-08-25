using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class CloudSystem : MonoBehaviour
    {
        const float MinAltitude = 350f, MaxAltitude = 850f;
        const float DepthSpread = 60f;
        const float WindowMargin = 300f;
        const float BaseAlpha = 0.5f;
        const int BlobCountMin = 5, BlobCountMax = 9;

        static readonly float[] DriftSpeed = { 6f, 12f, 24f };
        static readonly float[] Spacing = { 520f, 300f, 160f };
        static readonly float[] CloudWidth = { 45f, 80f, 130f };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        static readonly float[] HazeGlow = { 0.30f, 0.22f, 0.40f, 0.55f };

        struct Blob
        {
            public Transform tr;
            public Vector3 baseOffset;
            public Vector2 amplitude, frequency, phase;
        }

        class Cloud
        {
            public Transform root;
            public Material mat;
            public Blob[] blobs;
            public float speedMul;
        }

        Camera _cam;
        float _playPlaneZ;
        float _speed, _spacing, _width;
        Color _tint, _glow;
        readonly List<Cloud> _clouds = new List<Cloud>();
        float _time;
        float _nextSpawnU;
        bool _primed;

        public static CloudSystem Begin(Camera cam, Daytime daytime, Weather weather,
            CloudsPart part, float playPlaneZ)
            => Begin(cam, TintFor(daytime), GlowFor(daytime), part, playPlaneZ);

        public static CloudSystem Begin(Camera cam, Color tint, Color glow,
            CloudsPart part, float playPlaneZ)
        {
            var go = new GameObject("Clouds");
            var sys = go.AddComponent<CloudSystem>();
            sys._cam = cam;
            sys._playPlaneZ = playPlaneZ;
            sys._speed = DriftSpeed[(int)part.speed];
            sys._spacing = Spacing[(int)part.frequency];
            sys._width = CloudWidth[(int)part.size];
            sys._tint = tint;
            sys._glow = glow;
            return sys;
        }

        static Color TintFor(Daytime daytime)
        {
            switch (daytime)
            {
                case Daytime.Midday: return MiddaySky.CloudColor;
                case Daytime.Evening: return EveningSky.CloudColor;
                case Daytime.Night: return NightSky.CloudColor;
                default: return MorningSky.CloudColor;
            }
        }

        static Color GlowFor(Daytime daytime)
        {
            Color haze;
            switch (daytime)
            {
                case Daytime.Midday: haze = MiddaySky.HazeColor; break;
                case Daytime.Evening: haze = EveningSky.HazeColor; break;
                case Daytime.Night: haze = NightSky.HazeColor; break;
                default: haze = MorningSky.HazeColor; break;
            }
            return haze * HazeGlow[(int)daytime];
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            _time += Time.deltaTime;

            float depth = _playPlaneZ + DepthSpread - _cam.transform.position.z;
            float halfW = depth * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * _cam.aspect;
            float camX = _cam.transform.position.x;
            float left = camX - halfW - WindowMargin;
            float right = camX + halfW + WindowMargin;

            if (!_primed)
            {
                _nextSpawnU = left + _speed * _time;
                _primed = true;
            }

            while (_nextSpawnU - _speed * _time < right)
            {
                SpawnCloud(_nextSpawnU - _speed * _time);
                _nextSpawnU += _spacing * Random.Range(0.55f, 1.45f);
            }

            for (int i = _clouds.Count - 1; i >= 0; i--)
            {
                var cloud = _clouds[i];
                if (cloud.root == null) { _clouds.RemoveAt(i); continue; }

                cloud.root.position += Vector3.left * (_speed * cloud.speedMul * Time.deltaTime);
                if (cloud.root.position.x < left)
                {
                    DestroyCloud(cloud);
                    _clouds.RemoveAt(i);
                    continue;
                }

                for (int b = 0; b < cloud.blobs.Length; b++)
                {
                    var blob = cloud.blobs[b];
                    if (blob.tr == null) continue;
                    blob.tr.localPosition = blob.baseOffset + new Vector3(
                        Mathf.Sin(_time * blob.frequency.x + blob.phase.x) * blob.amplitude.x,
                        Mathf.Sin(_time * blob.frequency.y + blob.phase.y) * blob.amplitude.y,
                        0f);
                }
            }
        }

        void SpawnCloud(float x)
        {
            float width = _width * Random.Range(0.7f, 1.4f);
            var root = new GameObject("Cloud");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(x,
                Random.Range(MinAltitude, MaxAltitude),
                _playPlaneZ + Random.Range(-DepthSpread, DepthSpread));

            var mat = BuildMaterial();
            int count = Random.Range(BlobCountMin, BlobCountMax + 1);
            var blobs = new Blob[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Blob");
                go.transform.SetParent(root.transform, false);
                var offset = new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(-0.22f, 0.22f),
                    Random.Range(-0.08f, 0.08f)) * width;
                go.transform.localPosition = offset;
                go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float s = width * Random.Range(0.35f, 0.55f);
                go.transform.localScale = new Vector3(
                    s * Random.Range(1.1f, 1.7f),
                    s * Random.Range(0.8f, 1.15f),
                    s * Random.Range(1.1f, 1.7f));

                go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
                var renderer = go.AddComponent<MeshRenderer>();
                if (mat != null) renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                blobs[i] = new Blob
                {
                    tr = go.transform,
                    baseOffset = offset,
                    amplitude = new Vector2(Random.Range(0.05f, 0.12f), Random.Range(0.04f, 0.09f)) * width,
                    frequency = new Vector2(Random.Range(0.35f, 0.9f), Random.Range(0.35f, 0.9f)),
                    phase = new Vector2(Random.Range(0f, Mathf.PI * 2f), Random.Range(0f, Mathf.PI * 2f)),
                };
            }

            _clouds.Add(new Cloud
            {
                root = root.transform,
                mat = mat,
                blobs = blobs,
                speedMul = Random.Range(0.85f, 1.15f),
            });
        }

        Material BuildMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var mat = new Material(shader) { name = "Cloud (runtime)" };
            Color c = _tint;
            c.a = BaseAlpha * Random.Range(0.88f, 1.12f);
            mat.SetColor(BaseColorId, c);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColorId, _glow);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        static void DestroyCloud(Cloud cloud)
        {
            if (cloud.mat != null) Destroy(cloud.mat);
            if (cloud.root != null) Destroy(cloud.root.gameObject);
        }

        void OnDestroy()
        {
            foreach (var cloud in _clouds) DestroyCloud(cloud);
            _clouds.Clear();
        }
    }
}
