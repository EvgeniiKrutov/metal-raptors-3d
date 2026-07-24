using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    /// <summary>
    /// The drifting cloud layer for the terrain levels: soft blob-built clouds (same mesh
    /// family as <see cref="Explosion"/>) riding the play plane from right to left, tinted to
    /// the level's daytime. Presets come from the level's <see cref="CloudsPart"/>; start
    /// with <see cref="Begin"/>. Purely visual — no colliders, no shadows. Full design:
    /// docs/clouds.md.
    /// </summary>
    public class CloudSystem : MonoBehaviour
    {
        const float MinAltitude = 350f, MaxAltitude = 850f;
        const float DepthSpread = 60f;   // clouds sit within this of the play plane on Z, so
                                         // planes pass both in front of and behind them
        const float WindowMargin = 300f; // beyond the view edges; > any cloud's half-width
        const float BaseAlpha = 0.5f;
        const int BlobCountMin = 5, BlobCountMax = 9;

        // Presets indexed by CloudLevel (Low, Medium, High) — see docs/clouds.md.
        static readonly float[] DriftSpeed = { 6f, 12f, 24f };     // metres/second leftward
        static readonly float[] Spacing = { 520f, 300f, 160f };    // average metres between clouds
        static readonly float[] CloudWidth = { 45f, 80f, 130f };   // nominal cloud width, metres

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        struct Blob
        {
            public Transform tr;
            public Vector3 baseOffset;
            public Vector2 amplitude, frequency, phase; // slow X/Y hover of the blob in the cloud
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
        Color _tint;
        readonly List<Cloud> _clouds = new List<Cloud>();
        float _time;
        float _nextSpawnU; // conveyor-space X of the next cloud (docs/clouds.md)
        bool _primed;

        /// <summary>
        /// Starts the layer over the scene rendered by <paramref name="cam"/>.
        /// <paramref name="weather"/> is the future modulation seam, as in the sky classes;
        /// <see cref="Weather.Calm"/> changes nothing.
        /// </summary>
        public static CloudSystem Begin(Camera cam, Daytime daytime, Weather weather,
            CloudsPart part, float playPlaneZ)
        {
            var go = new GameObject("Clouds");
            var sys = go.AddComponent<CloudSystem>();
            sys._cam = cam;
            sys._playPlaneZ = playPlaneZ;
            sys._speed = DriftSpeed[(int)part.speed];
            sys._spacing = Spacing[(int)part.frequency];
            sys._width = CloudWidth[(int)part.size];
            sys._tint = TintFor(daytime);
            return sys;
        }

        static Color TintFor(Daytime daytime)
        {
            switch (daytime)
            {
                case Daytime.Midday: return new Color(0.97f, 0.98f, 1.00f);
                case Daytime.Evening: return new Color(0.98f, 0.80f, 0.66f);
                case Daytime.Night: return new Color(0.62f, 0.66f, 0.82f);
                default: return new Color(0.97f, 0.92f, 0.84f); // Morning
            }
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            _time += Time.deltaTime;

            // View half-width at the layer's farthest depth, so the margins cover every cloud.
            float depth = _playPlaneZ + DepthSpread - _cam.transform.position.z;
            float halfW = depth * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * _cam.aspect;
            float camX = _cam.transform.position.x;
            float left = camX - halfW - WindowMargin;
            float right = camX + halfW + WindowMargin;

            if (!_primed)
            {
                _nextSpawnU = left + _speed * _time; // first frame seeds the whole window
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
                // Yaw only: the X/Z stretch below must stay horizontal.
                go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float s = width * Random.Range(0.35f, 0.55f);
                go.transform.localScale = new Vector3(
                    s * Random.Range(1.1f, 1.7f),
                    s * Random.Range(0.8f, 1.15f),
                    s * Random.Range(1.1f, 1.7f));

                go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Build();
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
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Surface", 1f); // transparent
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
            foreach (var blob in cloud.blobs)
            {
                if (blob.tr == null) continue;
                var filter = blob.tr.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
            }
            if (cloud.root != null) Destroy(cloud.root.gameObject);
        }

        void OnDestroy()
        {
            foreach (var cloud in _clouds) DestroyCloud(cloud);
            _clouds.Clear();
        }
    }
}
