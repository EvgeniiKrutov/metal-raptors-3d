using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuStatRow
    {
        public const float BarHeight = MenuTheme.StatCaptionRowHeight + MenuTheme.StatCaptionToValue
                                       + MenuTheme.StatBarHeight;

        public const float BareValueHeight = MenuTheme.StatValueRowHeight;

        readonly RectTransform _fill;
        readonly Text _value;

        MenuStatRow(RectTransform fill, Text value)
        {
            _fill = fill;
            _value = value;
        }

        public static MenuStatRow CreateBar(Transform parent, string label, float top)
        {
            CreateCaption(parent, label, top);

            float barTop = top - MenuTheme.StatCaptionRowHeight - MenuTheme.StatCaptionToValue;
            Image track = CreateBarImage(parent, "Track", MenuTheme.StatBarWidth, barTop,
                MenuTheme.Colors.Border);
            Image fill = CreateBarImage(track.rectTransform, "Fill", 0f, 0f, MenuTheme.Colors.Accent);

            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;

            return new MenuStatRow(rt, null);
        }

        public static MenuStatRow CreateBareValue(Transform parent, float top) =>
            new MenuStatRow(null, UIFactory.CreateLabel(parent, string.Empty, MenuTheme.StatValueSize,
                top, MenuTheme.StatValueRowHeight, MenuTheme.Colors.Fg, UIFactory.MediumFont));

        public void SetFill(float fraction)
        {
            if (_fill == null) return;
            _fill.sizeDelta = new Vector2(MenuTheme.StatBarWidth * Mathf.Clamp01(fraction), 0f);
        }

        public void SetValue(string text)
        {
            if (_value != null) _value.text = text;
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
