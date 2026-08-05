using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuBadge
    {
        readonly RectTransform _root;
        readonly Image _face;
        readonly Text _text;

        MenuBadge(RectTransform root, Image face, Text text)
        {
            _root = root;
            _face = face;
            _text = text;
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

            return new MenuBadge(root, face, text);
        }

        public void Set(string label, Color color)
        {
            _text.text = label.ToUpperInvariant();
            _text.color = MenuTheme.Colors.Bg;
            _face.color = color;
            _root.sizeDelta = new Vector2(_text.preferredWidth + 2f * MenuTheme.BadgePadX,
                MenuTheme.BadgeHeight);
        }
    }
}
