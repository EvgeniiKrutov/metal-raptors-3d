using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuChoiceRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IMenuOptionRow
    {
        public event Action<IMenuFocusable> Hovered;
        public event Action<IMenuOptionRow> Engaged;

        Action<int> _onChanged;
        Text _caption;
        Text _value;
        MenuArrowView _left;
        MenuArrowView _right;
        string[] _values;
        int _index;
        bool _focused;
        bool _live;

        public int Index => _index;

        public static MenuChoiceRow Create(Transform parent, string label, string[] values, int index,
            float top, Action<int> onChanged)
        {
            var go = new GameObject($"Choice ({label})", typeof(RectTransform), typeof(MenuChoiceRow));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(MenuTheme.VolumeRowWidth, MenuTheme.VolumeRowHeight);
            rt.anchoredPosition = new Vector2(0f, top);

            var view = go.GetComponent<MenuChoiceRow>();
            view._onChanged = onChanged;
            view._values = values ?? new string[0];
            view._index = view.ClampIndex(index);

            CreateHitBox(rt);
            view.Build(rt, label);
            view.Apply();
            return view;
        }

        void Build(RectTransform rt, string label)
        {
            _caption = UIFactory.CreateLabel(rt, label.ToUpperInvariant(), MenuTheme.StatCaptionSize,
                0f, MenuTheme.StatCaptionRowHeight, MenuTheme.Colors.Muted, UIFactory.BoldFont);

            Vector2 arrow = MenuTheme.SelectorArrowSize;
            float barTop = -(MenuTheme.StatCaptionRowHeight + MenuTheme.StatCaptionToValue);
            float center = barTop - MenuTheme.StatBarHeight * 0.5f;
            float arrowTop = center + arrow.y * 0.5f;
            float barX = arrow.x + MenuTheme.VolumeArrowGap;
            float rightX = barX + MenuTheme.VolumeBarWidth + MenuTheme.VolumeArrowGap;

            _left = MenuArrowView.Create(rt, true, new Vector2(0f, arrowTop));
            _right = MenuArrowView.Create(rt, false, new Vector2(rightX, arrowTop));

            _value = UIFactory.CreateInlineLabel(rt, Current, MenuTheme.StatValueSize,
                new Vector2(barX, center + MenuTheme.ItemRowHeight * 0.5f), MenuTheme.ItemRowHeight,
                MenuTheme.Colors.Fg, UIFactory.MediumFont);
            _value.alignment = TextAnchor.MiddleCenter;
            _value.rectTransform.sizeDelta =
                new Vector2(MenuTheme.VolumeBarWidth, MenuTheme.ItemRowHeight);

            _left.Clicked += () => Step(-1);
            _right.Clicked += () => Step(1);
            _left.Hovered += RaiseHovered;
            _right.Hovered += RaiseHovered;
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

        string Current => _values.Length > 0 ? _values[_index] : string.Empty;

        int ClampIndex(int index) =>
            _values.Length > 0 ? Mathf.Clamp(index, 0, _values.Length - 1) : 0;

        public void SetIndex(int index)
        {
            _index = ClampIndex(index);
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

        public void Activate() => Engage();

        public bool Adjust(int delta)
        {
            Step(delta);
            return true;
        }

        void Step(int delta)
        {
            Engage();

            int next = ClampIndex(_index + delta);
            if (next == _index) return;

            _index = next;
            Apply();
            _onChanged?.Invoke(_index);
        }

        void Engage() => Engaged?.Invoke(this);

        void RaiseHovered()
        {
            if (_live) Hovered?.Invoke(this);
        }

        void Apply()
        {
            MenuPalette palette = MenuTheme.Colors;
            bool active = _live && _focused;

            _caption.color = active ? palette.Fg : palette.Muted;

            _value.text = Current;
            _value.color = !_live ? palette.Muted : _focused ? palette.Accent : palette.Fg;
            _value.font = active ? UIFactory.BoldFont : UIFactory.MediumFont;

            _left.SetState(_index > 0, active, !_live);
            _right.SetState(_index < _values.Length - 1, active, !_live);
        }

        public void OnPointerEnter(PointerEventData eventData) => RaiseHovered();

        public void OnPointerClick(PointerEventData eventData) => Engage();
    }
}
