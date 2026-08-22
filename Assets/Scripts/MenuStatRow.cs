using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuStatRow
    {
        public const float BarHeight = MenuTheme.StatCaptionRowHeight + MenuTheme.StatCaptionToValue
                                       + MenuTheme.StatBarHeight;

        readonly RectTransform _root;
        readonly RectTransform _fill;

        MenuStatRow(RectTransform root, RectTransform fill)
        {
            _root = root;
            _fill = fill;
        }

        public float Top => _root.anchoredPosition.y;

        public static MenuStatRow CreateBar(Transform parent, string label, float top)
        {
            var go = new GameObject($"Stat ({label})", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(MenuTheme.StatBarWidth, BarHeight);
            root.anchoredPosition = new Vector2(0f, top);

            CreateCaption(root, label, 0f);

            float barTop = -(MenuTheme.StatCaptionRowHeight + MenuTheme.StatCaptionToValue);
            Image track = CreateBarImage(root, "Track", MenuTheme.StatBarWidth, barTop,
                MenuTheme.Colors.Border);
            Image fill = CreateBarImage(track.rectTransform, "Fill", 0f, 0f, MenuTheme.Colors.Accent);

            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;

            return new MenuStatRow(root, rt);
        }

        public void SetY(float y) =>
            _root.anchoredPosition = new Vector2(_root.anchoredPosition.x, y);

        public void SetFill(float fraction)
        {
            if (_fill == null) return;
            _fill.sizeDelta = new Vector2(MenuTheme.StatBarWidth * Mathf.Clamp01(fraction), 0f);
        }

        static void CreateCaption(Transform parent, string label, float top)
        {
            UIFactory.CreateLabel(parent, label.ToUpperInvariant(), MenuTheme.StatCaptionSize, top,
                MenuTheme.StatCaptionRowHeight, MenuTheme.Colors.Muted, UIFactory.BoldFont);
        }

        static Image CreateBarImage(Transform parent, string name, float width, float top, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, MenuTheme.StatBarHeight);
            rt.anchoredPosition = new Vector2(0f, top);
            return img;
        }
    }
}
