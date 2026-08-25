using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuVolumeRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IMenuOptionRow
    {
        public event Action<IMenuFocusable> Hovered;
        public event Action<IMenuOptionRow> Engaged;

        Action<float> _onChanged;
        Text _caption;
        Text _value;
        Image _fill;
        MenuArrowView _left;
        MenuArrowView _right;
        int _step;
        bool _focused;
        bool _live;

        public float Value => AudioOptions.FromStep(_step);

        public static MenuVolumeRow Create(Transform parent, string label, float value, float top,
            Action<float> onChanged)
        {
            var go = new GameObject($"Volume ({label})", typeof(RectTransform), typeof(MenuVolumeRow));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(MenuTheme.VolumeRowWidth, MenuTheme.VolumeRowHeight);
            rt.anchoredPosition = new Vector2(0f, top);

            var view = go.GetComponent<MenuVolumeRow>();
            view._onChanged = onChanged;
            view._step = AudioOptions.ToStep(value);

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
            float barWidth = MenuTheme.VolumeBarWidth;
            float barTop = -(MenuTheme.StatCaptionRowHeight + MenuTheme.StatCaptionToValue);
            float center = barTop - MenuTheme.StatBarHeight * 0.5f;
            float arrowTop = center + arrow.y * 0.5f;
            float barX = arrow.x + MenuTheme.VolumeArrowGap;
            float rightX = barX + barWidth + MenuTheme.VolumeArrowGap;

            _left = MenuArrowView.Create(rt, true, new Vector2(0f, arrowTop));
            _right = MenuArrowView.Create(rt, false, new Vector2(rightX, arrowTop));

            Image track = CreateBar(rt, "Track", new Vector2(barX, barTop), barWidth,
                MenuTheme.Colors.Border);
            _fill = CreateBar(track.rectTransform, "Fill", Vector2.zero, 0f, MenuTheme.Colors.Accent);

            RectTransform fill = _fill.rectTransform;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.sizeDelta = Vector2.zero;
            fill.anchoredPosition = Vector2.zero;

            float valueX = rightX + arrow.x + MenuTheme.VolumeValueGap;
            _value = UIFactory.CreateInlineLabel(rt, AudioOptions.Percent(Value), MenuTheme.StatValueSize,
                new Vector2(valueX, center + MenuTheme.ItemRowHeight * 0.5f), MenuTheme.ItemRowHeight,
                MenuTheme.Colors.Fg, UIFactory.MediumFont);
            _value.rectTransform.sizeDelta =
                new Vector2(MenuTheme.VolumeValueWidth, MenuTheme.ItemRowHeight);

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

        static Image CreateBar(Transform parent, string name, Vector2 anchoredPos, float width, Color color)
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
            rt.anchoredPosition = anchoredPos;
            return img;
        }

        public void SetValue(float value)
        {
            _step = AudioOptions.ToStep(value);
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

            int next = Mathf.Clamp(_step + delta, 0, AudioOptions.Steps);
            if (next == _step) return;

            _step = next;
            Apply();
            _onChanged?.Invoke(Value);
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

            _value.text = AudioOptions.Percent(Value);
            _value.color = !_live ? palette.Muted : _focused ? palette.Accent : palette.Fg;
            _value.font = active ? UIFactory.BoldFont : UIFactory.MediumFont;

            _fill.rectTransform.sizeDelta = new Vector2(MenuTheme.VolumeBarWidth * Value, 0f);
            _fill.color = _live ? palette.Accent : palette.Muted;

            _left.SetState(_step > 0, active, !_live);
            _right.SetState(_step < AudioOptions.Steps, active, !_live);
        }

        public void OnPointerEnter(PointerEventData eventData) => RaiseHovered();

        public void OnPointerClick(PointerEventData eventData) => Engage();
    }
}
