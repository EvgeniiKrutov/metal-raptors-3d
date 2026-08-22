using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuBadge
    {
        readonly RectTransform _root;
        readonly Image _face;
        readonly Text _text;
        readonly Text _value;

        MenuBadge(RectTransform root, Image face, Text text, Text value)
        {
            _root = root;
            _face = face;
            _text = text;
            _value = value;
        }

        public static MenuBadge Create(Transform parent, float top)
        {
            var go = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(0f, MenuTheme.BadgeHeight);
            root.anchoredPosition = new Vector2(0f, top);

            var face = go.GetComponent<Image>();
            face.raycastTarget = false;

            var label = new GameObject("Label", typeof(Text));
            label.transform.SetParent(root, false);

            var text = label.GetComponent<Text>();
            text.font = UIFactory.BoldFont;
            text.fontSize = MenuTheme.BadgeSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return new MenuBadge(root, face, text, CreateValue(root));
        }

        static Text CreateValue(RectTransform badge)
        {
            Text text = UIFactory.CreateInlineLabel(badge, string.Empty, MenuTheme.StatValueSize,
                Vector2.zero, MenuTheme.BadgeHeight, MenuTheme.Colors.Fg, UIFactory.MediumFont);

            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(MenuTheme.BadgeValueGap, 0f);
            return text;
        }

        public void Set(string label, Color color)
        {
            _text.text = label.ToUpperInvariant();
            _text.color = MenuTheme.Colors.Bg;
            _face.color = color;
            _root.sizeDelta = new Vector2(_text.preferredWidth + 2f * MenuTheme.BadgePadX,
                MenuTheme.BadgeHeight);
        }

        public void SetValue(string text)
        {
            _value.text = text;
            _value.rectTransform.sizeDelta =
                new Vector2(_value.preferredWidth, MenuTheme.BadgeHeight);
        }
    }
}
