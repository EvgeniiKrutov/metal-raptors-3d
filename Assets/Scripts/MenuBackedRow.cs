using UnityEngine;

namespace MetalRaptors
{
    public class MenuBackedRow : IMenuFocusGroup
    {
        readonly IMenuRow _row;
        readonly MenuItemView _back;
        bool _onBack;

        public MenuBackedRow(IMenuRow row, MenuItemView back)
        {
            _row = row;
            _back = back;
            _row.FocusChanged += _ => LeaveBack();
            _back.Hovered += _ => FocusBack();
        }

        public static MenuItemView CreateBack(Transform page, string label, float cardsTop,
            float cardSize, System.Action onBack)
        {
            float top = cardsTop - cardSize - MenuTheme.CardsToBack;
            MenuItemView back = MenuItemView.Create(page, label, new Vector2(0f, top),
                MenuTheme.ItemSize, MenuTheme.ItemRowHeight, MenuItemStyle.Nav);
            back.Activated += onBack;
            return back;
        }

        public void MoveFocus(int delta)
        {
            if (delta > 0 && !_onBack) FocusBack();
            else if (delta < 0 && _onBack) _row.Refocus();
        }

        public void Adjust(int delta)
        {
            if (!_onBack) _row.Adjust(delta);
        }

        public void ActivateFocused()
        {
            if (_onBack) _back.Activate();
            else _row.ActivateFocused();
        }

        void FocusBack()
        {
            _onBack = true;
            _row.Blur();
            _back.SetFocused(true);
        }

        void LeaveBack()
        {
            _onBack = false;
            _back.SetFocused(false);
        }
    }
}
