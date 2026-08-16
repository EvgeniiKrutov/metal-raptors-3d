using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuPanel : IMenuFocusGroup
    {
        readonly List<IMenuFocusable> _focusables = new List<IMenuFocusable>();
        readonly RectTransform _root;
        float _cursor;
        int _focus = -1;

        public GameObject Root => _root.gameObject;

        public IMenuFocusable Focused =>
            _focus >= 0 && _focus < _focusables.Count ? _focusables[_focus] : null;

        public MenuPanel(Transform parent, string name, float top)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            _root = (RectTransform)go.transform;
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, top);
            _root.sizeDelta = new Vector2(0f, 0f);
        }

        public void AddGap(float pixels) => _cursor -= pixels;

        public Text AddCaption(string caption)
        {
            Text text = UIFactory.CreateLabel(_root, caption.ToUpperInvariant(), MenuTheme.CaptionSize,
                _cursor, MenuTheme.CaptionRowHeight, MenuTheme.Colors.Muted, UIFactory.BoldFont);
            _cursor -= MenuTheme.CaptionRowHeight + MenuTheme.CaptionToContent;
            return text;
        }

        public MenuStatRow AddStatBar(string label)
        {
            MenuStatRow row = MenuStatRow.CreateBar(_root, label, _cursor);
            _cursor -= MenuStatRow.BarHeight + MenuTheme.StatRowGap;
            return row;
        }

        public MenuStatRow AddStatText()
        {
            MenuStatRow row = MenuStatRow.CreateBareValue(_root, _cursor);
            _cursor -= MenuStatRow.BareValueHeight + MenuTheme.StatRowGap;
            return row;
        }

        public MenuBadge AddBadge()
        {
            MenuBadge badge = MenuBadge.Create(_root, _cursor);
            _cursor -= MenuTheme.BadgeHeight + MenuTheme.BadgeToContent;
            return badge;
        }

        public MenuItemView AddNav(string label, Action onActivate, bool interactable = true)
        {
            MenuItemView item = MenuItemView.Create(_root, label, new Vector2(0f, _cursor),
                MenuTheme.ItemSize, MenuTheme.ItemRowHeight, MenuItemStyle.Nav, interactable);

            if (interactable)
            {
                if (onActivate != null) item.Activated += onActivate;
                Register(item);
            }

            _cursor -= MenuTheme.ItemRowHeight + MenuTheme.ItemGap;
            return item;
        }

        public void AddTag(MenuItemView item, string tag)
        {
            Text label = UIFactory.CreateInlineLabel(_root, tag.ToUpperInvariant(), MenuTheme.CaptionSize,
                Vector2.zero, MenuTheme.ItemRowHeight, MenuTheme.Colors.Muted, UIFactory.BoldFont);

            Vector2 pos = item.RectTransform.anchoredPosition;
            label.rectTransform.anchoredPosition =
                new Vector2(pos.x + item.Width + MenuTheme.TagGap, pos.y);
        }

        public MenuItemView[] AddOptionRow(string[] labels, int selected, Action<int> onPick)
        {
            var items = new MenuItemView[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                MenuItemView item = MenuItemView.Create(_root, labels[i], new Vector2(0f, _cursor),
                    MenuTheme.OptionSize, MenuTheme.OptionRowHeight, MenuItemStyle.Option);

                item.SetSelected(i == selected);
                item.Activated += () =>
                {
                    onPick(index);
                    for (int j = 0; j < items.Length; j++) items[j].SetSelected(j == index);
                };

                Register(item);
                items[i] = item;
            }

            float x = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                items[i].SetX(x);
                x += items[i].Width + MenuTheme.OptionGap;
            }

            _cursor -= MenuTheme.OptionRowHeight;
            return items;
        }

        public MenuSelectorRow AddSelector(string label, string[] values, int selected, Action<int> onChanged)
        {
            MenuSelectorRow row = MenuSelectorRow.Create(_root, label, values, selected, _cursor, onChanged);
            Register(row);

            _cursor -= MenuTheme.ItemRowHeight + MenuTheme.ItemGap;
            return row;
        }

        void Register(IMenuFocusable item)
        {
            _focusables.Add(item);
            item.Hovered += Focus;
        }

        public void Focus(IMenuFocusable item)
        {
            int index = _focusables.IndexOf(item);
            if (index >= 0) FocusIndex(index);
        }

        public void FocusFirst()
        {
            for (int i = 0; i < _focusables.Count; i++)
                if (Reachable(_focusables[i])) { FocusIndex(i); return; }

            FocusIndex(0);
        }

        public void MoveFocus(int delta)
        {
            if (_focusables.Count == 0) return;

            int next = _focus < 0 ? 0 : Wrap(_focus + delta);
            for (int i = 0; i < _focusables.Count && !Reachable(_focusables[next]); i++)
                next = Wrap(next + delta);

            FocusIndex(next);
        }

        int Wrap(int index) =>
            (index % _focusables.Count + _focusables.Count) % _focusables.Count;

        static bool Reachable(IMenuFocusable item) =>
            !(item is MonoBehaviour behaviour) || behaviour.gameObject.activeInHierarchy;

        public void Adjust(int delta)
        {
            if (_focus >= 0 && _focus < _focusables.Count && _focusables[_focus].Adjust(delta)) return;
            MoveFocus(delta);
        }

        public void ActivateFocused()
        {
            if (_focus >= 0 && _focus < _focusables.Count) _focusables[_focus].Activate();
        }

        void FocusIndex(int index)
        {
            if (_focusables.Count == 0) return;
            _focus = Mathf.Clamp(index, 0, _focusables.Count - 1);
            for (int i = 0; i < _focusables.Count; i++) _focusables[i].SetFocused(i == _focus);
        }

        public void SetActive(bool active)
        {
            Root.SetActive(active);
            if (active) FocusFirst();
        }
    }
}
