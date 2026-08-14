using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class GarageController : MonoBehaviour
    {
        MenuPanel _panel;
        MenuItemView _selectItem;
        MenuBadge _typeBadge;
        MenuStatRow _country;
        MenuStatRow[] _bars;

        MenuArrowView _left;
        MenuArrowView _right;

        GaragePlaneView _planeView;
        Text _title;
        Text _description;

        int _index;

        PlaneModelConfig Plane => PlaneModels.All[_index];

        void Start()
        {
            _index = GameManager.Instance != null ? GameManager.Instance.SelectedPlaneIndex : 0;

            var canvas = UIFactory.CreateCanvas("Garage Canvas");
            UIFactory.CreateBackground(canvas.transform, MenuTheme.Colors.Bg);
            _planeView = GaragePlaneView.Build(canvas.transform, Plane);

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

            _panel.FocusFirst();
            Refresh();
        }

        void BuildPanel(Transform column)
        {
            _panel = new MenuPanel(column, "Garage Panel", MenuTheme.ListTop);

            _typeBadge = _panel.AddBadge();
            _country = _panel.AddStatText();

            _bars = new MenuStatRow[PlaneStatBars.All.Length];
            for (int i = 0; i < _bars.Length; i++)
                _bars[i] = _panel.AddStatBar(PlaneStatBars.All[i].label);

            _panel.AddGap(MenuTheme.SectionGap);
            _selectItem = _panel.AddNav("select plane", SelectPlane);
        }

        void BuildArrows(Transform screen)
        {
            _left = CreateArrow(screen, true, 0f, MenuTheme.GarageArrowInset);
            _right = CreateArrow(screen, false, 1f,
                -(MenuTheme.GarageArrowInset + MenuTheme.GarageArrowSize.x));

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
            if (adjust != 0) Step(adjust);

            int step = MenuInput.ReadStep();
            if (step != 0) _panel.MoveFocus(step);

            if (MenuInput.ReadSubmit()) _panel.ActivateFocused();
            if (MenuInput.ReadCancel()) ScreenFade.Load(SceneNames.MainMenu);
        }

        void Step(int delta)
        {
            int count = PlaneModels.All.Length;
            _index = ((_index + delta) % count + count) % count;
            Refresh();
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
            _country.SetValue(plane.country);

            for (int i = 0; i < _bars.Length; i++)
            {
                PlaneStatBar bar = PlaneStatBars.All[i];
                _bars[i].SetFill(bar.read(plane.stats) / bar.ceiling);
            }

            _planeView.SetPlane(plane);

            bool selected = GameManager.Instance != null
                            && GameManager.Instance.SelectedPlaneIndex == _index;
            _selectItem.SetLabel(selected ? "selected" : "select plane");
            _selectItem.SetInteractable(!selected);
        }
    }
}
