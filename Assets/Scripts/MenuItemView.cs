using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public enum MenuItemStyle { Nav, Option }

    /// <summary>
    /// One chrome-less text entry of the menu. Colour and weight follow its
    /// interactable / focused / selected state — see docs/main-menu.md.
    /// </summary>
    public class MenuItemView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IMenuFocusable
    {
        public event Action Activated;
        public event Action<IMenuFocusable> Hovered;

        Text _text;
        MenuItemStyle _style;
        bool _focused;
        bool _selected;

        public bool Interactable { get; private set; }
        public RectTransform RectTransform => _text.rectTransform;
        public float Width => _text.rectTransform.sizeDelta.x;

        public static MenuItemView Create(Transform parent, string label, Vector2 anchoredPos, int fontSize,
            float rowHeight, MenuItemStyle style, bool interactable = true)
        {
            var go = new GameObject($"Item ({label})", typeof(Text), typeof(MenuItemView));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<MenuItemView>();
            view._text = go.GetComponent<Text>();
            view._style = style;
            view.Interactable = interactable;

            Text text = view._text;
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = interactable;

            // Anchored to the parent's top left: x is the entry's own left edge, so a lone entry
            // sits flush at x = 0 and a run of them is laid out rightward from there.
            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;

            // Measured in the bold weight so the hit box does not shrink when focus lands on it.
            text.font = UIFactory.BoldFont;
            rt.sizeDelta = new Vector2(text.preferredWidth + 2f, rowHeight);

            view.Apply();
            return view;
        }

        /// <summary>Moves the entry's left edge along the row it shares with its neighbours.</summary>
        public void SetX(float x)
        {
            RectTransform rt = _text.rectTransform;
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }

        public void SetFocused(bool focused)
        {
            _focused = focused;
            Apply();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            Apply();
        }

        public void Activate()
        {
            if (Interactable) Activated?.Invoke();
        }

        /// <summary>A plain entry has no value to step, so left/right stay navigation keys.</summary>
        public bool Adjust(int delta) => false;

        void Apply()
        {
            MenuPalette palette = MenuTheme.Colors;
            Color color;
            bool bold;

            if (!Interactable)
            {
                color = palette.Muted;
                bold = false;
            }
            else if (_style == MenuItemStyle.Nav)
            {
                color = _focused ? palette.Accent : palette.Fg;
                bold = _focused;
            }
            else if (_selected)
            {
                color = palette.Accent;
                bold = true;
            }
            else
            {
                color = _focused ? palette.Fg : palette.Muted;
                bold = false;
            }

            _text.font = bold ? UIFactory.BoldFont : UIFactory.MediumFont;
            _text.color = color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Interactable) Hovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Activate();
        }
    }
}
