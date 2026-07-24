using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// A code-built explosion: a cluster of 6-7 low-poly blobs that expand, shrink and vanish
    /// while their colour runs orange -> warm yellow -> dark grey, plus one of the sibling
    /// repo's explosion_N.wav clips. Spawn with <see cref="Spawn"/>. No colliders, so the
    /// effect can never crash the player or soak a bullet. See docs/effects.md.
    /// </summary>
    public class Explosion : MonoBehaviour
    {
        const int BlobCountMin = 6, BlobCountMax = 7;
        const float LifeMin = 1.5f, LifeMax = 2f;
        const float MaxDelay = 0.3f;
        const float OffsetRadius = 0.5f;
        const float GrowEnd = 0.35f;
        const float StartScale = 0.15f, EndScale = 0.07f;
        const float YellowAt = 0.3f, GreyAt = 0.85f, EmissionOffAt = 0.75f;
        const float EmissionStrength = 2f;
        const float SoundVolume = 0.55f; // 2D playback; 3D rolloff would mute it at ~420 m

        static readonly Color Orange = new Color(1f, 0.45f, 0.08f);
        static readonly Color Yellow = new Color(1f, 0.93f, 0.45f);
        static readonly Color Grey = new Color(0.17f, 0.16f, 0.15f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Longest a spawned explosion can play — its final blob's delay plus lifetime —
        /// so a caller can wait for the blast to finish (see docs/effects.md).</summary>
        public static float Duration => MaxDelay + LifeMax;

        /// <summary>How long a downed plane's model stays visible after its explosion is spawned,
        /// so the blast begins a beat before the plane is removed (see docs/effects.md).</summary>
        public const float RemovalDelay = 0.15f;

        struct Blob
        {
            public Transform tr;
            public Material mat;
            public float delay, life, peakScale;
        }

        Blob[] _blobs;
        float _age;

        /// <param name="size">Rough size of the thing exploding; scales the whole effect.</param>
        public static void Spawn(Vector3 position, float size)
        {
            var root = new GameObject("Explosion");
            root.transform.position = position;
            var fx = root.AddComponent<Explosion>();

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            int count = Random.Range(BlobCountMin, BlobCountMax + 1);
            fx._blobs = new Blob[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Blob");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Random.insideUnitSphere * size * OffsetRadius;
                go.transform.localRotation = Random.rotation;
                go.transform.localScale = Vector3.zero; // hidden until its delay elapses

                go.AddComponent<MeshFilter>().sharedMesh = BuildBlobMesh();
                var renderer = go.AddComponent<MeshRenderer>();
                Material mat = null;
                if (shader != null)
                {
                    mat = new Material(shader);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    renderer.sharedMaterial = mat;
                }

                fx._blobs[i] = new Blob
                {
                    tr = go.transform,
                    mat = mat,
                    delay = Random.Range(0f, MaxDelay),
                    life = Random.Range(LifeMin, LifeMax),
                    peakScale = size * Random.Range(0.9f, 1.5f),
                };
            }

            PlaySound(position);
        }

        void Update()
        {
            _age += Time.deltaTime;
            bool anyAlive = false;
            for (int i = 0; i < _blobs.Length; i++)
            {
                var b = _blobs[i];
                float t = (_age - b.delay) / b.life;
                if (t < 0f) { anyAlive = true; continue; }
                if (t >= 1f) { if (b.tr != null) b.tr.localScale = Vector3.zero; continue; }
                anyAlive = true;
                if (b.tr == null) continue;

                b.tr.localScale = Vector3.one * (b.peakScale * ScaleAt(t));
                if (b.mat != null)
                {
                    Color c = ColorAt(t);
                    b.mat.SetColor(BaseColorId, c);
                    b.mat.SetColor(EmissionColorId, c * EmissionAt(t));
                }
            }
            if (!anyAlive) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_blobs == null) return;
            foreach (var b in _blobs)
            {
                if (b.mat != null) Destroy(b.mat);
                if (b.tr == null) continue;
                var filter = b.tr.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
            }
        }

        static float ScaleAt(float t)
        {
            if (t < GrowEnd)
            {
                float g = 1f - t / GrowEnd;
                return Mathf.Lerp(1f, StartScale, g * g);
            }
            float h = (t - GrowEnd) / (1f - GrowEnd);
            return Mathf.Lerp(1f, EndScale, h * h);
        }

        static Color ColorAt(float t)
        {
            if (t < YellowAt) return Color.Lerp(Orange, Yellow, t / YellowAt);
            float k = Mathf.Clamp01((t - YellowAt) / (GreyAt - YellowAt));
            return Color.Lerp(Yellow, Grey, k * k * (3f - 2f * k));
        }

        static float EmissionAt(float t)
        {
            return EmissionStrength * (1f - Mathf.Clamp01((t - YellowAt) / (EmissionOffAt - YellowAt)));
        }

        /// <summary>An icosphere with randomly displaced vertices, split per-face for flat shading.</summary>
        static Mesh BuildBlobMesh()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var baseVerts = new List<Vector3>
            {
                new Vector3(-1f,  t, 0f), new Vector3(1f,  t, 0f),
                new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f,  t), new Vector3(0f,  1f,  t),
                new Vector3(0f, -1f, -t), new Vector3(0f,  1f, -t),
                new Vector3( t, 0f, -1f), new Vector3( t, 0f,  1f),
                new Vector3(-t, 0f, -1f), new Vector3(-t, 0f,  1f),
            };
            for (int i = 0; i < baseVerts.Count; i++) baseVerts[i] = baseVerts[i].normalized;

            int[] icoFaces =
            {
                0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
                1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
                3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
                4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1,
            };

            var midCache = new Dictionary<long, int>();
            var faces = new List<int>();
            for (int i = 0; i < icoFaces.Length; i += 3)
            {
                int a = icoFaces[i], b = icoFaces[i + 1], c = icoFaces[i + 2];
                int ab = Midpoint(baseVerts, midCache, a, b);
                int bc = Midpoint(baseVerts, midCache, b, c);
                int ca = Midpoint(baseVerts, midCache, c, a);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            // Displace the shared vertices (so faces stay stitched); 0.5 keeps localScale = diameter.
            for (int i = 0; i < baseVerts.Count; i++)
                baseVerts[i] *= 0.5f * Random.Range(0.72f, 1.3f);

            var verts = new Vector3[faces.Count];
            var tris = new int[faces.Count];
            for (int i = 0; i < faces.Count; i++)
            {
                verts[i] = baseVerts[faces[i]];
                tris[i] = i;
            }

            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }

        static void PlaySound(Vector3 position)
        {
            var clip = Resources.Load<AudioClip>($"Sounds/explosion_{Random.Range(1, 4)}");
            if (clip == null) return;

            // Own carrier GameObject so the sound outlives the visual effect.
            var go = new GameObject("ExplosionSound");
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.PlayOneShot(clip, SoundVolume);
            Destroy(go, clip.length + 0.1f);
        }
    }
}
