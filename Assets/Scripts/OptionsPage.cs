using System;
using UnityEngine;

namespace MetalRaptors
{
    public class OptionsPage : IMenuFocusGroup
    {
        class Category
        {
            public MenuItemView Nav;
            public GameObject Root;
            public IMenuOptionRow[] Rows;
            public Action Refresh;
        }

        readonly GameObject _root;
        readonly MenuPanel _categories;
        readonly Category[] _cats;
        readonly Action _onBack;

        int _shown;
        int _row;
        bool _live;

        public GameObject Root => _root;

        public OptionsPage(Transform parent, Action onBack)
        {
            _onBack = onBack;

            Transform screen = MenuLayout.CreateScreen(parent, "Options Page");
            _root = screen.gameObject;

            Transform column = MenuLayout.CreatePage(screen, "Options Column", MenuTheme.ColumnFraction);
            MenuLayout.BuildTitle(column, "OPTIONS");

            _categories = new MenuPanel(column, "Options Categories", MenuTheme.ListTop);
            _cats = GraphicsOptions.Mobile
                ? new[] { BuildSound(screen) }
                : new[] { BuildSound(screen), BuildGraphics(screen) };

            _categories.AddGap(MenuTheme.SectionGap);
            MenuItemView back = _categories.AddNav("back", () => _onBack?.Invoke());
            back.Hovered += LeaveValues;

            Enter();
        }

        Category BuildSound(Transform screen)
        {
            var cat = new Category();
            Transform values = CreateValues(screen, "Options Sound");
            cat.Root = values.gameObject;

            var rows = new MenuVolumeRow[3];
            rows[0] = MenuVolumeRow.Create(values, "general", AudioOptions.Master, RowTop(0),
                AudioOptions.SetMaster);
            rows[1] = MenuVolumeRow.Create(values, "music", AudioOptions.Music, RowTop(1),
                AudioOptions.SetMusic);
            rows[2] = MenuVolumeRow.Create(values, "sfx", AudioOptions.Sfx, RowTop(2),
                AudioOptions.SetSfx);

            cat.Rows = rows;
            cat.Refresh = () =>
            {
                rows[0].SetValue(AudioOptions.Master);
                rows[1].SetValue(AudioOptions.Music);
                rows[2].SetValue(AudioOptions.Sfx);
            };

            Adopt(cat, "sound");
            return cat;
        }

        Category BuildGraphics(Transform screen)
        {
            var cat = new Category();
            Transform values = CreateValues(screen, "Options Graphics");
            cat.Root = values.gameObject;

            var rows = new MenuChoiceRow[5];
            rows[0] = MenuChoiceRow.Create(values, "god rays", GraphicsOptions.SwitchLabels,
                GraphicsOptions.GodRays ? 1 : 0, RowTop(0), GraphicsOptions.SetGodRays);
            rows[1] = MenuChoiceRow.Create(values, "shadows", GraphicsOptions.ShadowLabels,
                (int)GraphicsOptions.Shadows, RowTop(1), GraphicsOptions.SetShadows);
            rows[2] = MenuChoiceRow.Create(values, "bloom", GraphicsOptions.BloomLabels,
                (int)GraphicsOptions.BloomTier, RowTop(2), GraphicsOptions.SetBloom);
            rows[3] = MenuChoiceRow.Create(values, "ground detail", GraphicsOptions.DetailLabels,
                GraphicsOptions.GroundDetail, RowTop(3), GraphicsOptions.SetGroundDetail);
            rows[4] = MenuChoiceRow.Create(values, "frame cap", GraphicsOptions.FrameCapLabels,
                GraphicsOptions.FrameCap, RowTop(4), GraphicsOptions.SetFrameCap);

            cat.Rows = rows;
            cat.Refresh = () =>
            {
                rows[0].SetIndex(GraphicsOptions.GodRays ? 1 : 0);
                rows[1].SetIndex((int)GraphicsOptions.Shadows);
                rows[2].SetIndex((int)GraphicsOptions.BloomTier);
                rows[3].SetIndex(GraphicsOptions.GroundDetail);
                rows[4].SetIndex(GraphicsOptions.FrameCap);
            };

            Adopt(cat, "graphics");
            return cat;
        }

        void Adopt(Category cat, string label)
        {
            cat.Nav = _categories.AddNav(label, () => EnterValues(cat));
            cat.Nav.Hovered += item => Preview(cat);

            foreach (IMenuOptionRow row in cat.Rows)
            {
                row.Engaged += engaged => FocusRow(cat, engaged);
                row.Hovered += focusable => FocusRow(cat, focusable as IMenuOptionRow);
            }
        }

        static Transform CreateValues(Transform screen, string name) =>
            MenuLayout.CreateRegion(screen, name, MenuTheme.ColumnFraction, 1f, MenuTheme.VolumePadLeft);

        static float RowTop(int index) =>
            MenuTheme.ListTop - index * (MenuTheme.VolumeRowHeight + MenuTheme.VolumeRowGap);

        public void SetActive(bool active)
        {
            _root.SetActive(active);
            if (active) Enter();
        }

        public void Enter()
        {
            _live = false;
            _shown = 0;
            _row = 0;

            foreach (Category cat in _cats) cat.Refresh();

            _categories.Focus(_cats[0].Nav);
            ApplyRows();
        }

        public void MoveFocus(int delta)
        {
            if (!_live)
            {
                _categories.MoveFocus(delta);
                return;
            }

            int count = Current.Rows.Length;
            _row = ((_row + delta) % count + count) % count;
            ApplyRows();
        }

        public void Adjust(int delta)
        {
            if (_live) Current.Rows[_row].Adjust(delta);
        }

        public void ActivateFocused()
        {
            if (!_live) _categories.ActivateFocused();
        }

        public bool Cancel()
        {
            if (!_live) return false;
            LeaveValues(null);
            return true;
        }

        Category Current => _cats[_shown];

        void Preview(Category cat)
        {
            Show(cat);
            _live = false;
            ApplyRows();
        }

        void EnterValues(Category cat)
        {
            Show(cat);
            _live = true;
            ApplyRows();
        }

        void Show(Category cat)
        {
            int index = Array.IndexOf(_cats, cat);
            if (index < 0 || index == _shown) return;

            _shown = index;
            _row = 0;
        }

        void FocusRow(Category cat, IMenuOptionRow row)
        {
            if (row == null) return;

            int index = Array.IndexOf(cat.Rows, row);
            if (index < 0) return;

            Show(cat);
            _live = true;
            _row = index;
            _categories.Focus(cat.Nav);
            ApplyRows();
        }

        void LeaveValues(IMenuFocusable item)
        {
            if (!_live) return;
            _live = false;
            ApplyRows();
        }

        void ApplyRows()
        {
            for (int c = 0; c < _cats.Length; c++)
            {
                Category cat = _cats[c];
                bool shown = c == _shown;
                cat.Root.SetActive(shown);

                for (int i = 0; i < cat.Rows.Length; i++)
                {
                    cat.Rows[i].SetLive(shown && _live);
                    cat.Rows[i].SetFocused(shown && _live && i == _row);
                }
            }
        }
    }
}
