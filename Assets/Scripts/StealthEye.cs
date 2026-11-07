using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class StealthEye
    {
        const int TexWidth = 160;
        const int TexHeight = 96;
        const float HalfWidth = 0.8f;
        const float HalfHeight = 0.44f;
        const float PupilRadius = 0.2f;
        const float GapRadius = 0.27f;

        const float HeightShare = 1.25f;
        const float GapShare = 0.45f;
        const float FadeInSec = 0.2f;
        const float FadeOutSec = 0.6f;
        const float PulseRate = 9f;
        const float PulseScale = 0.08f;

        static readonly Color Calm = new Color(0.88f, 0.89f, 0.91f, 0.95f);
        static readonly Color Amber = new Color(1f, 0.72f, 0.2f, 1f);
        static readonly Color Alarm = new Color(1f, 0.22f, 0.16f, 1f);

        static Sprite _sprite;

        readonly RectTransform _root;
        readonly CanvasGroup _group;
        readonly Image _fill;
        float _alpha;

        public StealthEye(Transform parent)
        {
            float height = HudTheme.BarHeight * HeightShare;
            float width = height * TexWidth / TexHeight;

            var go = new GameObject("Stealth Eye", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 0.5f);
            _root.sizeDelta = new Vector2(width, height);
            _root.anchoredPosition = new Vector2(
                HudTheme.ColumnLeft + HudTheme.BarWidth + HudTheme.BarHeight * GapShare,
                -HudTheme.ColumnTop - HudTheme.BarHeight * 0.5f);

            _group = go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            Image back = Layer(go.transform, "Track");
            back.color = HudTheme.Track;

            _fill = Layer(go.transform, "Fill");
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Vertical;
            _fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _fill.fillAmount = 0f;
        }

        static Image Layer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = EyeSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public void Set(float level, bool locked, bool shown)
        {
            float dt = Time.unscaledDeltaTime;
            float target = shown ? 1f : 0f;
            float step = dt / (shown ? FadeInSec : FadeOutSec);
            _alpha = Mathf.MoveTowards(_alpha, target, step);
            _group.alpha = _alpha;

            _fill.fillAmount = Mathf.Clamp01(level);

            Color color = level < 0.5f
                ? Color.Lerp(Calm, Amber, level * 2f)
                : Color.Lerp(Amber, Alarm, (level - 0.5f) * 2f);

            float scale = 1f;
            if (locked)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseRate);
                color = Color.Lerp(Alarm, Color.white, pulse * 0.35f);
                scale = 1f + PulseScale * pulse;
            }

            _fill.color = color;
            _root.localScale = Vector3.one * scale;
        }

        static Sprite EyeSprite
        {
            get
            {
                if (_sprite != null) return _sprite;

                var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false)
                {
                    name = "Stealth Eye",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                float scale = TexHeight;
                float rise = (HalfWidth * HalfWidth - HalfHeight * HalfHeight) / (2f * HalfHeight);
                float radius = HalfHeight + rise;
                float aa = 1.2f / scale;

                var pixels = new Color32[TexWidth * TexHeight];
                for (int y = 0; y < TexHeight; y++)
                {
                    float py = (y + 0.5f - TexHeight * 0.5f) / scale;
                    for (int x = 0; x < TexWidth; x++)
                    {
                        float px = (x + 0.5f - TexWidth * 0.5f) / scale;

                        float upper = Mathf.Sqrt(px * px + (py + rise) * (py + rise)) - radius;
                        float lower = Mathf.Sqrt(px * px + (py - rise) * (py - rise)) - radius;
                        float lens = Mathf.Max(upper, lower);
                        float centre = Mathf.Sqrt(px * px + py * py);

                        float body = Mathf.Clamp01(0.5f - lens / aa);
                        float gap = Mathf.Clamp01((centre - PupilRadius) / aa + 0.5f)
                                    * Mathf.Clamp01((GapRadius - centre) / aa + 0.5f);
                        float alpha = body * (1f - gap);

                        pixels[y * TexWidth + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply();

                _sprite = Sprite.Create(tex, new Rect(0f, 0f, TexWidth, TexHeight),
                    new Vector2(0.5f, 0.5f), 100f);
                _sprite.hideFlags = HideFlags.HideAndDontSave;
                return _sprite;
            }
        }
    }
}
