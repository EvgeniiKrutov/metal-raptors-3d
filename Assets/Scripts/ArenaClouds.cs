using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class ArenaClouds : MonoBehaviour
    {
        public const float FloorTop = -0.55f;

        const float FloorAlpha = 0.94f;
        const float CrownHeight = 0.25f;
        const float CrownAspectMin = 1.7f, CrownAspectMax = 2.5f;
        const float CrownStep = 0.5f;
        const float CrownJitter = 0.035f;
        const float BaseFrom = 0.12f;
        const float BaseBottom = -1.25f;
        const float BaseAspect = 2.4f;
        const float PieceDepthJitter = 6f;

        static readonly float[] FloorDepth = { 1.6f, 0.9f, 0.4f, 0f, -0.2f, -0.35f };
        static readonly float[] FloorLift = { 0.12f, 0.08f, 0.04f, 0f, -0.06f, -0.13f };
        static readonly float[] FloorFade = { 0.8f, 0.86f, 0.93f, 1f, 1f, 1f };

        static readonly float[] DriftDepth = { 1.1f, 0.45f, -0.18f };
        static readonly float[] DriftWidth = { 0.62f, 0.46f, 0.36f };
        static readonly float[] DriftSpacing = { 0.9f, 1.3f, 3.2f };
        static readonly float[] DriftAlpha = { 0.42f, 0.48f, 0.22f };
        const float DriftLow = FloorTop + 0.24f;
        const float DriftHigh = 0.9f;
        const int DriftBlobsMin = 5, DriftBlobsMax = 9;

        enum Kind { Crown, Base, Drift }

        class Piece
        {
            public Transform tr;
            public float half;
        }

        class Strip
        {
            public Kind kind;
            public float ratio;
            public float z;
            public float left;
            public float right;
            public float top;
            public float span;
            public float spacing;
            public float width;
            public float next;
            public Material mat;
            public readonly List<Piece> pieces = new List<Piece>();
        }

        readonly List<Strip> _strips = new List<Strip>();
        readonly List<Material> _materials = new List<Material>();

        Vector3 _eye;
        float _halfW, _halfH;
        float _distance;
        float _playZ;
        float _speed;
        Color _tint, _glow;

        public float FloorY => _eye.y + FloorTop * _halfH;

        public static ArenaClouds Begin(Vector3 eye, float halfViewWidth, float halfViewHeight,
            float cameraDistance, float playPlaneZ, Color tint, Color glow, float speed)
        {
            var clouds = new GameObject("Arena Clouds").AddComponent<ArenaClouds>();
            clouds._eye = eye;
            clouds._halfW = halfViewWidth;
            clouds._halfH = halfViewHeight;
            clouds._distance = cameraDistance;
            clouds._playZ = playPlaneZ;
            clouds._tint = tint;
            clouds._glow = glow;
            clouds._speed = speed;
            clouds.Build();
            return clouds;
        }

        void Build()
        {
            for (int i = 0; i < FloorDepth.Length; i++) BuildFloorRow(i);
            for (int i = 0; i < DriftDepth.Length; i++) BuildDrift(i);
        }

        Strip NewStrip(Kind kind, float depth, float alpha)
        {
            float ratio = 1f + depth;
            float half = _halfW * ratio;

            var strip = new Strip
            {
                kind = kind,
                ratio = ratio,
                z = _playZ + depth * _distance,
                left = _eye.x - half,
                right = _eye.x + half,
                mat = CloudSystem.CloudMaterial(_tint, _glow, alpha),
            };

            if (strip.mat != null) _materials.Add(strip.mat);
            _strips.Add(strip);
            return strip;
        }

        float Y(Strip strip, float fraction) => _eye.y + fraction * _halfH * strip.ratio;

        float Unit(Strip strip) => _halfH * strip.ratio;

        void BuildFloorRow(int index)
        {
            float alpha = FloorAlpha * FloorFade[index];
            bool front = index == FloorDepth.Length - 1;

            if (front)
            {
                Strip bed = NewStrip(Kind.Base, FloorDepth[index], alpha);
                bed.top = FloorTop + FloorLift[index];
                float height = (bed.top - BaseFrom - BaseBottom) * Unit(bed);
                Chain(bed, height * BaseAspect * 0.5f, height * BaseAspect * 1.2f);
            }

            Strip crown = NewStrip(Kind.Crown, FloorDepth[index], alpha);
            crown.top = FloorTop + FloorLift[index];
            float crownHeight = CrownHeight * Unit(crown);
            Chain(crown, crownHeight * CrownAspectMin * CrownStep, crownHeight * CrownAspectMax);
        }

        void Chain(Strip strip, float step, float margin)
        {
            int count = Mathf.CeilToInt((strip.right - strip.left + 2f * margin) / step) + 1;
            strip.span = count * step;

            for (int i = 0; i < count; i++)
            {
                var piece = new Piece { tr = Blob(transform, strip.mat) };
                Shape(strip, piece);
                Place(strip, piece, strip.left - margin + i * step);
                strip.pieces.Add(piece);
            }
        }

        void BuildDrift(int index)
        {
            Strip strip = NewStrip(Kind.Drift, DriftDepth[index], DriftAlpha[index]);
            strip.width = DriftWidth[index] * Unit(strip);
            strip.spacing = DriftSpacing[index] * Unit(strip);

            float x = strip.left - Random.Range(0f, strip.spacing);
            while (x < strip.right)
            {
                var piece = new Piece { tr = Cluster(strip) };
                Shape(strip, piece);
                Place(strip, piece, x);
                strip.pieces.Add(piece);
                x += strip.spacing * Random.Range(0.6f, 1.4f);
            }
            strip.next = x;
        }

        void Shape(Strip strip, Piece piece)
        {
            float unit = Unit(strip);

            switch (strip.kind)
            {
                case Kind.Crown:
                {
                    float height = CrownHeight * unit;
                    float width = height * Random.Range(CrownAspectMin, CrownAspectMax);
                    piece.tr.localScale = new Vector3(width, height * Random.Range(0.85f, 1.1f),
                        width * Random.Range(0.6f, 1f));
                    piece.half = width * 0.5f;
                    break;
                }
                case Kind.Base:
                {
                    float height = (strip.top - BaseFrom - BaseBottom) * unit;
                    float width = height * BaseAspect * Random.Range(0.85f, 1.2f);
                    piece.tr.localScale = new Vector3(width, height, width * 0.6f);
                    piece.half = width * 0.5f;
                    break;
                }
                default:
                {
                    float width = strip.width * Random.Range(0.7f, 1.35f);
                    piece.tr.localScale = Vector3.one * width;
                    piece.half = width * 0.8f;
                    break;
                }
            }
        }

        void Place(Strip strip, Piece piece, float x)
        {
            float y;
            switch (strip.kind)
            {
                case Kind.Crown:
                    y = Y(strip, strip.top - CrownHeight * 0.5f
                                 + Random.Range(-CrownJitter, CrownJitter));
                    break;
                case Kind.Base:
                    y = Y(strip, (strip.top - BaseFrom + BaseBottom) * 0.5f);
                    break;
                default:
                    y = Y(strip, Random.Range(DriftLow, DriftHigh));
                    break;
            }

            float z = strip.z + Random.Range(-PieceDepthJitter, PieceDepthJitter) * strip.ratio;
            piece.tr.position = new Vector3(x, y, z);
        }

        Transform Cluster(Strip strip)
        {
            var root = new GameObject("Cloud").transform;
            root.SetParent(transform, false);

            int count = Random.Range(DriftBlobsMin, DriftBlobsMax + 1);
            for (int i = 0; i < count; i++)
            {
                Transform blob = Blob(root, strip.mat);
                blob.localPosition = new Vector3(Random.Range(-0.5f, 0.5f),
                    Random.Range(-0.22f, 0.22f), Random.Range(-0.08f, 0.08f));
                blob.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float s = Random.Range(0.35f, 0.55f);
                blob.localScale = new Vector3(s * Random.Range(1.1f, 1.7f),
                    s * Random.Range(0.8f, 1.15f), s * Random.Range(1.1f, 1.7f));
            }
            return root;
        }

        static Transform Blob(Transform parent, Material mat)
        {
            var go = new GameObject("Blob");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();

            var renderer = go.AddComponent<MeshRenderer>();
            if (mat != null) renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        void Update()
        {
            float dx = _speed * Time.deltaTime;
            if (dx <= 0f) return;

            foreach (Strip strip in _strips)
            {
                strip.next -= dx;

                foreach (Piece piece in strip.pieces)
                {
                    Vector3 p = piece.tr.position;
                    p.x -= dx;

                    if (p.x + piece.half >= strip.left)
                    {
                        piece.tr.position = p;
                        continue;
                    }

                    Shape(strip, piece);
                    if (strip.kind != Kind.Drift)
                    {
                        Place(strip, piece, p.x + strip.span);
                        continue;
                    }

                    float x = Mathf.Max(strip.next, strip.right + piece.half);
                    Place(strip, piece, x);
                    strip.next = x + strip.spacing * Random.Range(0.6f, 1.4f);
                }
            }
        }

        void OnDestroy()
        {
            foreach (Material mat in _materials)
                if (mat != null) Destroy(mat);
            _materials.Clear();
        }
    }
}
