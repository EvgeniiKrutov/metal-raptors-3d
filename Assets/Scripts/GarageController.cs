using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class GarageController : MonoBehaviour
    {
        static float ColourRowHeight => MenuTheme.ItemRowHeight + MenuTheme.ItemGap;

        MenuPanel _panel;
        MenuItemView _selectItem;
        MenuItemView _backItem;
        MenuSelectorRow _colour;
        MenuBadge _typeBadge;
        MenuStatRow[] _bars;

        MenuArrowView _left;
        MenuArrowView _right;

        GaragePlaneView _planeView;
        Text _title;
        Text _description;

        int _index;
        float[] _barTops;
        float _selectTop;
        float _backTop;

        PlaneModelConfig Plane => PlaneModels.All[_index];

        static PlaneSkin SkinOf(PlaneModelConfig plane) =>
            GameManager.Instance != null ? GameManager.Instance.SkinFor(plane)
                                         : PlaneSkins.Default(plane);

        void Start()
        {
            _index = GameManager.Instance != null ? GameManager.Instance.SelectedPlaneIndex : 0;

            var canvas = UIFactory.CreateCanvas("Garage Canvas");
            UIFactory.CreateBackground(canvas.transform, MenuTheme.Colors.Bg);
            _planeView = GaragePlaneView.Build(canvas.transform, Plane, SkinOf(Plane));

            Transform screen = MenuLayout.CreateScreen(canvas.transform, "Garage Screen");
            Transform column = MenuLayout.CreateRegion(screen, "Garage Column", 0f,
                MenuTheme.ColumnFraction, MenuTheme.GaragePadLeft);

            _title = MenuLayout.BuildTitle(column, string.Empty);
            BuildPanel(column);
            BuildArrows(screen);

            _description = UIFactory.CreateCenteredParagraph(canvas.transform, string.Empty,
                MenuTheme.DescriptionSize, MenuTheme.GarageDescriptionBottom,
                MenuTheme.GarageDescriptionWidth, MenuTheme.GarageDescriptionRowHeight,
                MenuTheme.DescriptionLineSpacing, MenuTheme.Colors.Muted, UIFactory.MediumFont);

            Refresh();
            _panel.FocusFirst();
        }

        void BuildPanel(Transform column)
        {
            _panel = new MenuPanel(column, "Garage Panel", MenuTheme.ListTop);

            _typeBadge = _panel.AddBadge();
            _colour = _panel.AddSelector("colour", PlaneSkins.Labels(Plane), 0, PickColour);

            _bars = new MenuStatRow[PlaneStatBars.All.Length];
            _barTops = new float[_bars.Length];
            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i] = _panel.AddStatBar(PlaneStatBars.All[i].label);
                _barTops[i] = _bars[i].Top;
            }

            _panel.AddGap(MenuTheme.SectionGap);
            _selectItem = _panel.AddNav("select plane", SelectPlane);
            _selectTop = _selectItem.RectTransform.anchoredPosition.y;

            _panel.AddGap(MenuTheme.SectionGap);
            _backItem = _panel.AddNav("back", GoBack);
            _backTop = _backItem.RectTransform.anchoredPosition.y;
        }

        void BuildArrows(Transform screen)
        {
            _left = CreateArrow(screen, true, 0f,
                MenuTheme.SafeLeft + MenuTheme.GarageArrowInset);
            _right = CreateArrow(screen, false, 1f,
                -(MenuTheme.SafeRight + MenuTheme.GarageArrowInset + MenuTheme.GarageArrowSize.x));

            _left.Clicked += () => Step(-1);
            _right.Clicked += () => Step(1);

            _left.Hovered += () => _left.SetState(true, true);
            _left.Exited += () => _left.SetState(true, false);
            _right.Hovered += () => _right.SetState(true, true);
            _right.Exited += () => _right.SetState(true, false);
        }

        static MenuArrowView CreateArrow(Transform parent, bool pointsLeft, float anchorX, float offsetX)
        {
            MenuArrowView view = MenuArrowView.Create(parent, pointsLeft, Vector2.zero,
                MenuTheme.GarageArrowSize);

            RectTransform rt = view.RectTransform;
            rt.anchorMin = new Vector2(anchorX, 0.5f);
            rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(offsetX, 0f);

            view.SetState(true, false);
            return view;
        }

        void Update()
        {
            if (ScreenFade.IsBusy) return;

            int adjust = MenuInput.ReadAdjust();
            if (adjust != 0)
            {
                if (_panel.Focused == (IMenuFocusable)_colour) _panel.Adjust(adjust);
                else Step(adjust);
            }

            int step = MenuInput.ReadStep();
            if (step != 0) _panel.MoveFocus(step);

            if (MenuInput.ReadSubmit()) _panel.ActivateFocused();
            if (MenuInput.ReadCancel()) GoBack();
        }

        static void GoBack() => ScreenFade.Load(SceneNames.MainMenu);

        void Step(int delta)
        {
            int count = PlaneModels.All.Length;
            _index = ((_index + delta) % count + count) % count;
            Refresh();
        }

        void PickColour(int index)
        {
            PlaneSkin[] skins = PlaneSkins.Of(Plane);
            if (index < 0 || index >= skins.Length) return;

            if (GameManager.Instance != null) GameManager.Instance.SetSkin(Plane, skins[index]);
            _planeView.SetSkin(skins[index]);
        }

        void SelectPlane()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetSelectedPlane(_index);
            _planeView.PlaySpinUp();
            Refresh();
        }

        void Refresh()
        {
            PlaneModelConfig plane = Plane;

            _title.text = plane.displayName.ToUpperInvariant();
            _description.text = plane.description;
            _typeBadge.Set(plane.type.label, plane.type.color);
            _typeBadge.SetValue(plane.country);

            for (int i = 0; i < _bars.Length; i++)
            {
                PlaneStatBar bar = PlaneStatBars.All[i];
                _bars[i].SetFill(bar.read(plane.stats) / bar.ceiling);
            }

            RefreshColour(plane);

            _planeView.SetPlane(plane, SkinOf(plane));

            bool selected = GameManager.Instance != null
                            && GameManager.Instance.SelectedPlaneIndex == _index;
            _selectItem.SetLabel(selected ? "selected" : "select plane");
            _selectItem.SetInteractable(!selected);
        }

        void RefreshColour(PlaneModelConfig plane)
        {
            bool selectable = PlaneSkins.Selectable(plane);

            if (selectable)
                _colour.SetValues(PlaneSkins.Labels(plane),
                    PlaneSkins.IndexOf(plane, SkinOf(plane)));

            _colour.gameObject.SetActive(selectable);

            float shift = selectable ? 0f : ColourRowHeight;
            for (int i = 0; i < _bars.Length; i++) _bars[i].SetY(_barTops[i] + shift);
            _selectItem.SetY(_selectTop + shift);
            _backItem.SetY(_backTop + shift);

            if (!selectable && _panel.Focused == (IMenuFocusable)_colour)
                _panel.Focus(_selectItem);
        }
    }
}
