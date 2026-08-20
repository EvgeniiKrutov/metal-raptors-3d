using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class CrateBurst : MonoBehaviour
    {
        const int SplinterMin = 12, SplinterMax = 18;
        const int DustMin = 5, DustMax = 8;

        const float SplinterLifeMin = 0.7f, SplinterLifeMax = 1.4f;
        const float SplinterSpeedMin = 0.9f, SplinterSpeedMax = 2.4f;
        const float SplinterLengthMin = 0.10f, SplinterLengthMax = 0.26f;
        const float SplinterThickness = 0.045f;
        const float SplinterSpinMin = 180f, SplinterSpinMax = 620f;
        const float SplinterEndFactor = 0.35f;
        const float Gravity = 150f;
        const float Drag = 1.1f;

        const float DustLifeMin = 0.9f, DustLifeMax = 1.6f;
        const float DustRise = 16f;
        const float DustOutward = 26f;
        const float DustScaleMin = 0.30f, DustScaleMax = 0.55f;
        const float DustGrowth = 2.2f;
        const float DustOpacity = 0.5f;

        static readonly Color WoodLight = new Color(0.52f, 0.36f, 0.19f);
        static readonly Color WoodDark = new Color(0.28f, 0.18f, 0.10f);
        static readonly Color DustColor = new Color(0.38f, 0.28f, 0.18f, DustOpacity);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        class Piece
        {
            public Transform tr;
            public Material mat;
            public Color color;
            public Vector3 velocity;
            public Vector3 spinAxis;
            public float spinRate;
            public float life, age;
            public Vector3 startScale;
            public float endFactor;
            public bool falls;
            public bool fades;
        }

        readonly List<Piece> _pieces = new List<Piece>();

        public static void Spawn(Vector3 position, float size)
        {
            var root = new GameObject("Crate Burst");
            root.transform.position = position;

            var burst = root.AddComponent<CrateBurst>();
            burst.BuildSplinters(size);
            burst.BuildDust(size);
        }

        void BuildSplinters(float size)
        {
            int count = Random.Range(SplinterMin, SplinterMax + 1);
            for (int i = 0; i < count; i++)
            {
                float length = size * Random.Range(SplinterLengthMin, SplinterLengthMax);
                float thick = size * SplinterThickness * Random.Range(0.7f, 1.4f);
                Vector3 scale = new Vector3(length, thick, thick * Random.Range(0.8f, 1.6f));
                Color color = Color.Lerp(WoodDark, WoodLight, Random.value);

                var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                    scale, color, emissive: false, keepCollider: false);
                go.name = "Splinter";
                go.transform.rotation = Random.rotation;
                go.transform.SetParent(transform, true);

                var renderer = go.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                Vector2 dir = Random.insideUnitCircle.normalized;
                _pieces.Add(new Piece
                {
                    tr = go.transform,
                    mat = renderer.sharedMaterial,
                    color = color,
                    velocity = new Vector3(dir.x, dir.y, 0f)
                               * (size * Random.Range(SplinterSpeedMin, SplinterSpeedMax)),
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(SplinterSpinMin, SplinterSpinMax)
                               * (Random.value < 0.5f ? -1f : 1f),
                    life = Random.Range(SplinterLifeMin, SplinterLifeMax),
                    startScale = scale,
                    endFactor = SplinterEndFactor,
                    falls = true,
                });
            }
        }

        void BuildDust(float size)
        {
            int count = Random.Range(DustMin, DustMax + 1);
            for (int i = 0; i < count; i++)
            {
                float scale = size * Random.Range(DustScaleMin, DustScaleMax);
                var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                    Vector3.one * scale, DustColor, emissive: false, keepCollider: false);
                go.name = "Crate Dust";
                go.transform.rotation = Random.rotation;
                go.transform.SetParent(transform, true);

                var renderer = go.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                UIFactory.MakeTransparent(renderer.sharedMaterial);

                Vector2 dir = Random.insideUnitCircle;
                _pieces.Add(new Piece
                {
                    tr = go.transform,
                    mat = renderer.sharedMaterial,
                    color = DustColor,
                    velocity = new Vector3(dir.x * DustOutward, dir.y * DustOutward + DustRise, 0f),
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(10f, 45f) * (Random.value < 0.5f ? -1f : 1f),
                    life = Random.Range(DustLifeMin, DustLifeMax),
                    startScale = Vector3.one * scale,
                    endFactor = DustGrowth,
                    fades = true,
                });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                Piece piece = _pieces[i];
                piece.age += dt;

                if (piece.age >= piece.life || piece.tr == null)
                {
                    if (piece.tr != null) Destroy(piece.tr.gameObject);
                    _pieces.RemoveAt(i);
                    continue;
                }

                float t = piece.age / piece.life;

                piece.tr.position += piece.velocity * dt;
                if (piece.falls)
                {
                    piece.velocity.y -= Gravity * dt;
                    piece.velocity *= Mathf.Max(0f, 1f - Drag * dt);
                }
                piece.tr.Rotate(piece.spinAxis, piece.spinRate * dt, Space.World);
                piece.tr.localScale = Vector3.Lerp(piece.startScale,
                    piece.startScale * piece.endFactor, t);

                if (piece.fades && piece.mat != null)
                {
                    Color color = piece.color;
                    color.a = piece.color.a * (1f - t);
                    piece.mat.SetColor(BaseColorId, color);
                }
            }

            if (_pieces.Count == 0) Destroy(gameObject);
        }
    }
}
