using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuOptionButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IMenuOptionRow
    {
        public event Action<IMenuFocusable> Hovered;
        public event Action<IMenuOptionRow> Engaged;

        Action _onActivate;
        Text _text;
        bool _focused;
        bool _live;

        public bool Interactable { get; private set; } = true;

        public static MenuOptionButton Create(Transform parent, string label, float top, Action onActivate)
        {
            var go = new GameObject($"Button ({label})", typeof(Text), typeof(MenuOptionButton));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<MenuOptionButton>();
            view._onActivate = onActivate;
            view._text = go.GetComponent<Text>();

            Text text = view._text;
            text.fontSize = MenuTheme.ItemSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, top);

            view.SetLabel(label);
            return view;
        }

        public void SetLabel(string label)
        {
            _text.text = label;
            _text.font = UIFactory.BoldFont;
            _text.rectTransform.sizeDelta = new Vector2(_text.preferredWidth + 2f, MenuTheme.ItemRowHeight);
            Apply();
        }

        public void SetInteractable(bool interactable)
        {
            Interactable = interactable;
            Apply();
        }

        public void SetLive(bool live)
        {
            _live = live;
            Apply();
        }

        public void SetFocused(bool focused)
        {
            _focused = focused;
            Apply();
        }

        public void Activate()
        {
            Engaged?.Invoke(this);
            if (Interactable) _onActivate?.Invoke();
        }

        public bool Adjust(int delta) => false;

        void Apply()
        {
            MenuPalette palette = MenuTheme.Colors;
            bool active = _live && _focused;

            _text.color = !_live || !Interactable ? palette.Muted : _focused ? palette.Accent : palette.Fg;
            _text.font = active ? UIFactory.BoldFont : UIFactory.MediumFont;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_live) Hovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData) => Activate();
    }
}
