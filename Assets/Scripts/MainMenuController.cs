using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MainMenuController : MonoBehaviour
    {
        enum MenuScreen { Home, Eras, Era, Levels, Custom }

        MenuPanel _main;
        MenuPanel _challenges;
        MenuPanel _eraPanel;
        MenuPanel _customPanel;
        MenuCardRow _eras;
        MenuLevelRow _levels;

        GameObject _column;
        GameObject _erasPage;
        GameObject _eraPage;
        GameObject _levelsPage;
        GameObject _customPage;

        MenuPlaneView _planeView;

        Text _erasTitle;
        Text _erasDescription;
        Text _eraTitle;
        Text _levelTitle;
        Text _levelDate;
        Text _levelBrief;
        MenuArrowView _levelLeft;
        MenuArrowView _levelRight;
        MenuPreviewCard _mapPreview;

        int _mapIndex;
        int _eraIndex;
        Daytime _daytime = Daytime.Morning;

        IMenuFocusGroup _group;
        MenuPanel _homePanel;
        MenuScreen _screen;

        void Start()
        {
            var canvas = UIFactory.CreateCanvas("MainMenu Canvas");
            UIFactory.CreateBackground(canvas.transform, MenuTheme.Colors.Bg);
            _planeView = MenuPlaneView.Build(canvas.transform, GameManager.CurrentPlane);

            Transform column = MenuLayout.CreatePage(canvas.transform, "Menu Column", MenuTheme.ColumnFraction);
            _column = column.gameObject;
            MenuLayout.BuildTitle(column, "METAL RAPTORS");
            _main = BuildMainPanel(column);
            _challenges = BuildChallengesPanel(column);

            _erasPage = BuildErasPage(canvas.transform);
            _eraPage = BuildEraPage(canvas.transform);
            _levelsPage = BuildLevelsPage(canvas.transform);
            _customPage = BuildCustomPage(canvas.transform);

            ShowHome(_main);
        }

        void Update()
        {
            if (_group == null || ScreenFade.IsBusy) return;

            int step = MenuInput.ReadStep();
            if (step != 0) _group.MoveFocus(step);

            int adjust = MenuInput.ReadAdjust();
            if (adjust != 0) _group.Adjust(adjust);

            if (MenuInput.ReadSubmit()) _group.ActivateFocused();
            if (MenuInput.ReadCancel()) Cancel();
        }

        MenuPanel BuildMainPanel(Transform column)
        {
            var panel = new MenuPanel(column, "Main Panel", MenuTheme.ListTop);
            panel.AddNav("career", () => ScreenFade.Swap(ShowEras));
            panel.AddNav("challenges", null, interactable: false);
            panel.AddNav("custom battle", () => ScreenFade.Swap(ShowCustom));
            panel.AddNav("garage", () => ScreenFade.Load(SceneNames.Garage));
            panel.AddNav("online battles", null, interactable: false);
            panel.AddNav("options", null, interactable: false);
            return panel;
        }

        MenuPanel BuildChallengesPanel(Transform column)
        {
            var panel = new MenuPanel(column, "Challenges Panel", MenuTheme.ListTop);
            panel.AddCaption("challenges");
            panel.AddNav("level 1", () => ScreenFade.Load(SceneNames.Level1));

            bool unlocked = GameManager.Instance == null || GameManager.Instance.IsLevelUnlocked(2);
            MenuItemView level2 = panel.AddNav("level 2",
                () => ScreenFade.Load(SceneNames.Level2), unlocked);
            if (!unlocked) panel.AddTag(level2, "locked");

            panel.AddGap(MenuTheme.SectionGap);
            panel.AddNav("back", GoHome);
            return panel;
        }

        GameObject BuildErasPage(Transform parent)
        {
            Transform page = MenuLayout.CreatePage(parent, "Career Page", 1f);
            _erasTitle = MenuLayout.BuildTitle(page, string.Empty);

            _erasDescription = UIFactory.CreateParagraph(page, string.Empty, MenuTheme.DescriptionSize,
                MenuTheme.ListTop, MenuTheme.DescriptionWidth, MenuTheme.DescriptionRowHeight,
                MenuTheme.DescriptionLineSpacing, MenuTheme.Colors.Muted, UIFactory.MediumFont);

            float top = MenuTheme.ListTop - MenuTheme.DescriptionRowHeight - MenuTheme.DescriptionToCards;
            _eras = new MenuCardRow(page, "Era Cards", top, CareerEras.All.Length);

            CareerEra[] eras = CareerEras.All;
            for (int i = 0; i < eras.Length; i++)
            {
                int index = i;
                _eras.AddCard(eras[i].Title, eras[i].Unlocked, eras[i].Emblem,
                    () => ScreenFade.Swap(() => ShowEra(index)));
            }

            _eras.Layout();
            _eras.FocusChanged += ShowEraHeader;
            return page.gameObject;
        }

        GameObject BuildEraPage(Transform parent)
        {
            Transform page = MenuLayout.CreatePage(parent, "Era Page", MenuTheme.ColumnFraction);
            _eraTitle = MenuLayout.BuildTitle(page, string.Empty);

            _eraPanel = new MenuPanel(page, "Era Panel", MenuTheme.ListTop);
            _eraPanel.AddNav(CampaignProgress.HighestCompleted > 0 ? "continue" : "start", StartCampaign);
            _eraPanel.AddNav("level select", () => ScreenFade.Swap(ShowLevels));

            _eraPanel.AddGap(MenuTheme.SectionGap);
            _eraPanel.AddNav("back", GoHome);
            return page.gameObject;
        }

        GameObject BuildLevelsPage(Transform parent)
        {
            Transform screen = MenuLayout.CreateScreen(parent, "Level Select Page");
            Transform page = MenuLayout.CreatePage(screen, "Level Select", 1f);

            _levelTitle = MenuLayout.BuildTitle(page, string.Empty);

            _levelDate = UIFactory.CreateLabel(page, string.Empty, MenuTheme.LevelDateSize,
                MenuTheme.ListTop, MenuTheme.LevelDateRowHeight, MenuTheme.Colors.Muted,
                UIFactory.MediumFont);

            float briefTop = MenuTheme.ListTop - MenuTheme.LevelDateRowHeight
                             - MenuTheme.LevelDateToBrief;
            _levelBrief = UIFactory.CreateParagraph(page, string.Empty, MenuTheme.DescriptionSize,
                briefTop, MenuTheme.DescriptionWidth, MenuTheme.LevelBriefRowHeight,
                MenuTheme.DescriptionLineSpacing, MenuTheme.Colors.Muted, UIFactory.MediumFont);

            _levels = MenuLevelRow.Create(page, "Level Cards", MenuTheme.LevelCardsTop);

            foreach (CampaignLevelEntry level in CampaignLevelList.All)
            {
                int number = level.Number;
                _levels.AddCard(level, CampaignProgress.IsUnlocked(number),
                    CampaignProgress.IsCompleted(number), () => StartCampaignLevel(number));
            }

            _levels.Layout();
            _levels.FocusChanged += ShowLevelHeader;
            _levels.ViewChanged += UpdateLevelArrows;

            BuildLevelArrows(screen);
            return screen.gameObject;
        }

        void BuildLevelArrows(Transform screen)
        {
            Transform band = MenuLayout.CreateRegion(screen, "Level Arrows", 0f, 1f, 0f, 0f);

            _levelLeft = CreateEdgeArrow(band, true, _levels.CardSize);
            _levelRight = CreateEdgeArrow(band, false, _levels.CardSize);

            _levelLeft.Clicked += () => _levels.Slide(-1);
            _levelRight.Clicked += () => _levels.Slide(1);

            _levelLeft.Hovered += () => _levelLeft.SetState(_levels.CanSlide(-1), true);
            _levelLeft.Exited += UpdateLevelArrows;
            _levelRight.Hovered += () => _levelRight.SetState(_levels.CanSlide(1), true);
            _levelRight.Exited += UpdateLevelArrows;
        }

        static MenuArrowView CreateEdgeArrow(Transform band, bool pointsLeft, float cardSize)
        {
            MenuArrowView view = MenuArrowView.Create(band, pointsLeft, Vector2.zero,
                MenuTheme.GarageArrowSize);

            float anchorX = pointsLeft ? 0f : 1f;
            float x = pointsLeft
                ? MenuTheme.SafeLeft + MenuTheme.GarageArrowInset
                : -(MenuTheme.SafeRight + MenuTheme.GarageArrowInset + MenuTheme.GarageArrowSize.x);

            RectTransform rt = view.RectTransform;
            rt.anchorMin = new Vector2(anchorX, 1f);
            rt.anchorMax = new Vector2(anchorX, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, MenuTheme.LevelCardsTop - cardSize * 0.5f);
            return view;
        }

        void UpdateLevelArrows()
        {
            _levelLeft.SetState(_levels.CanSlide(-1), false);
            _levelRight.SetState(_levels.CanSlide(1), false);
        }

        GameObject BuildCustomPage(Transform parent)
        {
            Transform screen = MenuLayout.CreateScreen(parent, "Custom Page");
            Transform column = MenuLayout.CreatePage(screen, "Custom Column", MenuTheme.ColumnFraction);
            MenuLayout.BuildTitle(column, "CUSTOM BATTLE");

            _customPanel = new MenuPanel(column, "Custom Panel", MenuTheme.ListTop);
            _customPanel.AddSelector("map", BattleMaps.Names(), _mapIndex, PickMap);
            _customPanel.AddSelector("weather", DaytimeNames.All, (int)_daytime, PickWeather);

            _customPanel.AddGap(MenuTheme.SectionGap);
            _customPanel.AddNav("start level", StartCustomBattle);

            _customPanel.AddGap(MenuTheme.SectionGap);
            _customPanel.AddNav("back to menu", GoHome);

            Transform band = MenuLayout.CreateRegion(screen, "Custom Preview", MenuTheme.ColumnFraction, 1f, 0f);
            _mapPreview = new MenuPreviewCard(band, PreviewTitle(), Vector2.zero);
            return screen.gameObject;
        }

        void PickMap(int index)
        {
            _mapIndex = index;
            _mapPreview.SetTitle(PreviewTitle());
        }

        void PickWeather(int index)
        {
            _daytime = (Daytime)index;
            _mapPreview.SetTitle(PreviewTitle());
        }

        string PreviewTitle() =>
            $"{BattleMaps.All[_mapIndex].Name} | {DaytimeNames.All[(int)_daytime]}";

        void StartCustomBattle()
        {
            CustomBattle.Request(BattleMaps.All[_mapIndex], _daytime);
            ScreenFade.Load(SceneNames.CampaignLevel1);
        }

        static void StartCampaign() => StartCampaignLevel(CampaignProgress.NextLevel);

        static void StartCampaignLevel(int number)
        {
            CustomBattle.Clear();
            CampaignRun.Request(number);
            ScreenFade.Load(SceneNames.CampaignLevel1);
        }

        void ShowEraHeader(int index)
        {
            CareerEra era = CareerEras.All[index];
            _erasTitle.text = era.Title;
            _erasDescription.text = era.Description;
        }

        void ShowLevelHeader(int index)
        {
            CampaignLevelEntry level = CampaignLevelList.All[index];
            _levelTitle.text = level.Title;
            _levelDate.text = level.Date;
            _levelBrief.text = level.Brief;
        }

        void GoHome() => ScreenFade.Swap(() => ShowHome(_main));

        void ShowHome(MenuPanel panel)
        {
            SetScreen(MenuScreen.Home);
            _homePanel = panel;
            _main.SetActive(panel == _main);
            _challenges.SetActive(panel == _challenges);
            _group = panel;
        }

        void ShowEras()
        {
            SetScreen(MenuScreen.Eras);
            _eras.FocusFirst();
            _group = _eras;
        }

        void ShowEra(int index)
        {
            _eraIndex = index;
            SetScreen(MenuScreen.Era);
            _eraTitle.text = CareerEras.All[index].Title;
            _group = _eraPanel;
        }

        void ShowEraPage() => ShowEra(_eraIndex);

        void ShowLevels()
        {
            SetScreen(MenuScreen.Levels);
            _levels.FocusOn(CampaignProgress.NextLevel - CampaignRun.FirstLevel);
            UpdateLevelArrows();
            _group = _levels;
        }

        void ShowCustom()
        {
            SetScreen(MenuScreen.Custom);
            _customPanel.SetActive(true);
            _group = _customPanel;
        }

        void SetScreen(MenuScreen screen)
        {
            _screen = screen;
            _column.SetActive(screen == MenuScreen.Home);
            _erasPage.SetActive(screen == MenuScreen.Eras);
            _eraPage.SetActive(screen == MenuScreen.Era);
            _levelsPage.SetActive(screen == MenuScreen.Levels);
            _customPage.SetActive(screen == MenuScreen.Custom);
            _planeView.SetActive(screen == MenuScreen.Home);
        }

        void Cancel()
        {
            if (_screen == MenuScreen.Levels)
            {
                ScreenFade.Swap(ShowEraPage);
                return;
            }
            if (_screen != MenuScreen.Home || _homePanel != _main) GoHome();
        }
    }
}
