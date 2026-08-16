using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuSelectorRow : MonoBehaviour, IPointerEnterHandler, IMenuFocusable
    {
        public event Action<IMenuFocusable> Hovered;

        string[] _values;
        Action<int> _onChanged;
        Text _value;
        MenuArrowView _left;
        MenuArrowView _right;
        int _index;
        bool _focused;

        public int Index => _index;

        public static MenuSelectorRow Create(Transform parent, string label, string[] values,
            int index, float top, Action<int> onChanged)
        {
            var go = new GameObject($"Selector ({label})", typeof(RectTransform), typeof(MenuSelectorRow));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(MenuTheme.SelectorRowWidth, MenuTheme.ItemRowHeight);
            rt.anchoredPosition = new Vector2(0f, top);

            var view = go.GetComponent<MenuSelectorRow>();
            view._values = values ?? new string[0];
            view._index = view._values.Length > 0
                ? Mathf.Clamp(index, 0, view._values.Length - 1) : 0;
            view._onChanged = onChanged;

            CreateHitBox(rt);

            UIFactory.CreateInlineLabel(rt, label, MenuTheme.ItemSize, Vector2.zero,
                MenuTheme.ItemRowHeight, MenuTheme.Colors.Fg, UIFactory.MediumFont);

            float arrowY = -0.5f * (MenuTheme.ItemRowHeight - MenuTheme.SelectorArrowSize.y);
            float valueX = MenuTheme.SelectorLabelWidth + MenuTheme.SelectorArrowSize.x
                           + MenuTheme.SelectorArrowGap;

            view._left = MenuArrowView.Create(rt, true,
                new Vector2(MenuTheme.SelectorLabelWidth, arrowY));
            view._right = MenuArrowView.Create(rt, false,
                new Vector2(valueX + MenuTheme.SelectorValueWidth + MenuTheme.SelectorArrowGap, arrowY));

            view._left.Clicked += () => view.Step(-1);
            view._right.Clicked += () => view.Step(1);
            view._left.Hovered += view.RaiseHovered;
            view._right.Hovered += view.RaiseHovered;

            view._value = UIFactory.CreateInlineLabel(rt, view.CurrentValue, MenuTheme.ItemSize,
                new Vector2(valueX, 0f), MenuTheme.ItemRowHeight, MenuTheme.Colors.Fg, UIFactory.MediumFont);
            view._value.alignment = TextAnchor.MiddleCenter;
            view._value.rectTransform.sizeDelta =
                new Vector2(MenuTheme.SelectorValueWidth, MenuTheme.ItemRowHeight);

            view.Apply();
            return view;
        }

        static void CreateHitBox(RectTransform parent)
        {
            var go = new GameObject("Hit Box", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = Color.clear;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        string CurrentValue => _values.Length > 0 ? _values[_index] : string.Empty;

        public void SetValues(string[] values, int index)
        {
            _values = values ?? new string[0];
            _index = _values.Length > 0 ? Mathf.Clamp(index, 0, _values.Length - 1) : 0;
            Apply();
        }

        void Step(int delta)
        {
            if (_values.Length == 0) return;

            int next = Mathf.Clamp(_index + delta, 0, _values.Length - 1);
            RaiseHovered();
            if (next == _index) return;

            _index = next;
            Apply();
            _onChanged?.Invoke(_index);
        }

        void RaiseHovered() => Hovered?.Invoke(this);

        void Apply()
        {
            MenuPalette palette = MenuTheme.Colors;

            _value.text = CurrentValue;
            _value.color = _focused ? palette.Accent : palette.Fg;
            _value.font = _focused ? UIFactory.BoldFont : UIFactory.MediumFont;

            _left.SetState(_index > 0, _focused);
            _right.SetState(_index < _values.Length - 1, _focused);
        }

        public void SetFocused(bool focused)
        {
            _focused = focused;
            Apply();
        }

        public void Activate() { }

        public bool Adjust(int delta)
        {
            Step(delta);
            return true;
        }

        public void OnPointerEnter(PointerEventData eventData) => RaiseHovered();
    }
}
