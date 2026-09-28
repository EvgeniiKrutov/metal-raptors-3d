using System;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public enum ScreenEdge { Left, Right, Top, Bottom }

    public class DangerSign : MonoBehaviour
    {
        public const float DefaultSeconds = 2f;

        const float AppearSec = 0.18f;
        const float FadeSec = 0.22f;
        const float BlinkHz = 3.2f;
        const float BlinkSharpness = 2.5f;
        const float BlinkFloor = 0.12f;
        const float PlateFloor = 0.45f;
        const float PopScale = 1.35f;
        const float ThrobScale = 0.04f;

        const float Size = 160f;
        const float EdgeInset = 34f;
        const float AlongMin = 0.2f;
        const float AlongMax = 0.8f;
        const float AlongResponse = 10f;

        const int TextureSize = 256;
        const float Root3 = 1.7320508f;
        const float ShapeRadius = 0.62f;
        const float CornerRound = 0.08f;
        const float OutlineWidth = 0.1f;
        const float BarTop = 0.38f;
        const float BarBottom = 0.02f;
        const float BarTopRadius = 0.075f;
        const float BarBottomRadius = 0.05f;
        const float DotY = -0.19f;
        const float DotRadius = 0.07f;
        const float GlowFalloff = 0.1f;
        const float GlowEdge = 0.12f;

        static readonly Color GlowColor = new Color(1f, 0.15f, 0.1f, 0.45f);
        static readonly Color PlateColor = new Color(0.16f, 0.02f, 0.02f, 0.6f);
        static readonly Color InkColor = new Color(0.97f, 0.2f, 0.15f);

        enum Layer { Glow, Plate, Ink }

        static readonly Sprite[] Sprites = new Sprite[3];

        static float HalfWidth => (ShapeRadius + CornerRound) * 0.5f;
        static float HalfHeight => (ShapeRadius * Root3 * 0.5f + CornerRound) * 0.5f;

        CanvasGroup _group;
        RectTransform _rt;
        Image _glow;
        Image _plate;
        Image _ink;
        ScreenEdge _edge;
        Func<float> _along;
        float _at = 0.5f;
        float _seconds;
        float _life;

        public float Seconds => _seconds;

        public static DangerSign Show(Transform parent, ScreenEdge edge,
            float seconds = DefaultSeconds, Func<float> along = null)
        {
            var go = new GameObject("Danger Sign", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var sign = go.AddComponent<DangerSign>();
            sign.Build(edge, seconds, along);
            return sign;
        }

        void Build(ScreenEdge edge, float seconds, Func<float> along)
        {
            _edge = edge;
            _along = along;
            _seconds = Mathf.Max(seconds > 0f ? seconds : DefaultSeconds, AppearSec + FadeSec);
            if (_along != null) _at = _along();

            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _rt = (RectTransform)transform;
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(Size, Size);

            _glow = LayerImage(Layer.Glow);
            _plate = LayerImage(Layer.Plate);
            _ink = LayerImage(Layer.Ink);

            Apply();
        }

        Image LayerImage(Layer layer)
        {
            var go = new GameObject(layer.ToString(), typeof(Image));
            go.transform.SetParent(transform, false);

            var image = go.GetComponent<Image>();
            image.sprite = SpriteFor(layer);
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return image;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life += dt;
            if (_along != null)
                _at = Mathf.Lerp(_at, _along(), 1f - Mathf.Exp(-AlongResponse * dt));

            Apply();

            if (_life >= _seconds) Destroy(gameObject);
        }

        void Apply()
        {
            float appear = Mathf.Clamp01(_life / AppearSec);
            float fade = Mathf.Clamp01((_life - (_seconds - FadeSec)) / FadeSec);
            float ease = 1f - Mathf.Pow(1f - appear, 3f);

            float wave = Mathf.Cos(_life * BlinkHz * 2f * Mathf.PI);
            float on = Mathf.Clamp01(0.5f + wave * BlinkSharpness);

            _group.alpha = ease * (1f - fade);
            _rt.localScale = Vector3.one * (Mathf.Lerp(PopScale, 1f, ease) * (1f + ThrobScale * on));

            _glow.color = Fade(GlowColor, GlowColor.a * on);
            _plate.color = Fade(PlateColor, PlateColor.a * Mathf.Lerp(PlateFloor, 1f, on));
            _ink.color = Fade(InkColor, Mathf.Lerp(BlinkFloor, 1f, on));

            Place();
        }

        void Place()
        {
            float at = Mathf.Clamp(_at, AlongMin, AlongMax);
            float side = EdgeInset + Size * HalfWidth;
            float cap = EdgeInset + Size * HalfHeight;

            Vector2 anchor;
            Vector2 offset;
            switch (_edge)
            {
                case ScreenEdge.Right:
                    anchor = new Vector2(1f, at);
                    offset = new Vector2(-(MenuTheme.SafeRight + side), 0f);
                    break;
                case ScreenEdge.Top:
                    anchor = new Vector2(at, 1f);
                    offset = new Vector2(0f, -(MenuTheme.SafeTop + cap));
                    break;
                case ScreenEdge.Bottom:
                    anchor = new Vector2(at, 0f);
                    offset = new Vector2(0f, MenuTheme.SafeBottom + cap);
                    break;
                default:
                    anchor = new Vector2(0f, at);
                    offset = new Vector2(MenuTheme.SafeLeft + side, 0f);
                    break;
            }

            _rt.anchorMin = anchor;
            _rt.anchorMax = anchor;
            _rt.anchoredPosition = offset;
        }

        static Color Fade(Color color, float alpha) =>
            new Color(color.r, color.g, color.b, alpha);

        static Sprite SpriteFor(Layer layer)
        {
            int index = (int)layer;
            if (Sprites[index] == null) Sprites[index] = Bake(layer);
            return Sprites[index];
        }

        static Sprite Bake(Layer layer)
        {
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Danger " + layer,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float toPixels = TextureSize * 0.5f;
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    var p = new Vector2((x + 0.5f) / TextureSize * 2f - 1f,
                        (y + 0.5f) / TextureSize * 2f - 1f);
                    float alpha = Alpha(layer, p, toPixels);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static float Alpha(Layer layer, Vector2 p, float toPixels)
        {
            var c = new Vector2(p.x, p.y + ShapeRadius / (2f * Root3));
            float shape = Triangle(c);

            switch (layer)
            {
                case Layer.Glow:
                    float edge = Mathf.Clamp01(
                        (1f - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y))) / GlowEdge);
                    return Mathf.Exp(-Mathf.Max(0f, shape) / GlowFalloff) * edge;

                case Layer.Plate:
                    return Cover(shape * toPixels);

                default:
                    float ring = Cover(shape * toPixels)
                                 * Mathf.Clamp01(0.5f + (shape + OutlineWidth) * toPixels);
                    return Mathf.Max(ring, Cover(Mark(c) * toPixels));
            }
        }

        static float Cover(float pixels) => Mathf.Clamp01(0.5f - pixels);

        static float Triangle(Vector2 p)
        {
            float r = ShapeRadius;
            p.x = Mathf.Abs(p.x) - r;
            p.y += r / Root3;
            if (p.x + Root3 * p.y > 0f)
                p = new Vector2(p.x - Root3 * p.y, -Root3 * p.x - p.y) * 0.5f;
            p.x -= Mathf.Clamp(p.x, -2f * r, 0f);
            return -p.magnitude * Mathf.Sign(p.y) - CornerRound;
        }

        static float Mark(Vector2 c)
        {
            float bar = Capsule(new Vector2(Mathf.Abs(c.x), c.y - BarBottom),
                BarBottomRadius, BarTopRadius, BarTop - BarBottom);
            float dot = (c - new Vector2(0f, DotY)).magnitude - DotRadius;
            return Mathf.Min(bar, dot);
        }

        static float Capsule(Vector2 p, float r1, float r2, float h)
        {
            float b = (r1 - r2) / h;
            float a = Mathf.Sqrt(1f - b * b);
            float k = Vector2.Dot(p, new Vector2(-b, a));
            if (k < 0f) return p.magnitude - r1;
            if (k > a * h) return (p - new Vector2(0f, h)).magnitude - r2;
            return Vector2.Dot(p, new Vector2(a, b)) - r1;
        }
    }
}
