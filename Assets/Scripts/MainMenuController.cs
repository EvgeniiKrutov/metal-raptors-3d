using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MainMenuController : MonoBehaviour
    {
        enum MenuScreen { Home, Eras, Era, Custom }

        MenuPanel _main;
        MenuPanel _challenges;
        MenuPanel _eraPanel;
        MenuPanel _levelsPanel;
        MenuPanel _customPanel;
        MenuCardRow _eras;

        GameObject _column;
        GameObject _erasPage;
        GameObject _eraPage;
        GameObject _customPage;

        MenuPlaneView _planeView;

        Text _erasTitle;
        Text _erasDescription;
        Text _eraTitle;
        MenuPreviewCard _mapPreview;

        int _mapIndex;
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
            _customPage = BuildCustomPage(canvas.transform);

            ShowHome(_main);
        }

        void Update()
        {
            if (_group == null) return;

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
            panel.AddNav("career", ShowEras);
            panel.AddNav("challenges", null, interactable: false);
            panel.AddNav("custom battle", ShowCustom);
            panel.AddNav("garage", () => SceneManager.LoadScene(SceneNames.Garage));
            panel.AddNav("online battles", null, interactable: false);
            panel.AddNav("options", null, interactable: false);
            return panel;
        }

        MenuPanel BuildChallengesPanel(Transform column)
        {
            var panel = new MenuPanel(column, "Challenges Panel", MenuTheme.ListTop);
            panel.AddCaption("challenges");
            panel.AddNav("level 1", () => SceneManager.LoadScene(SceneNames.Level1));

            bool unlocked = GameManager.Instance == null || GameManager.Instance.IsLevelUnlocked(2);
            MenuItemView level2 = panel.AddNav("level 2",
                () => SceneManager.LoadScene(SceneNames.Level2), unlocked);
            if (!unlocked) panel.AddTag(level2, "locked");

            panel.AddGap(MenuTheme.SectionGap);
            panel.AddNav("back", () => ShowHome(_main));
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
            _eras = new MenuCardRow(page, "Era Cards", top);

            CareerEra[] eras = CareerEras.All;
            for (int i = 0; i < eras.Length; i++)
            {
                int index = i;
                _eras.AddCard(eras[i].Title, eras[i].Years, eras[i].Unlocked, () => ShowEra(index));
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
            _eraPanel.AddNav("start", StartCampaign);
            _eraPanel.AddNav("level select", ShowLevels);

            _eraPanel.AddGap(MenuTheme.SectionGap);
            _eraPanel.AddNav("back", () => ShowHome(_main));

            _levelsPanel = BuildLevelsPanel(page);
            return page.gameObject;
        }

        MenuPanel BuildLevelsPanel(Transform page)
        {
            var panel = new MenuPanel(page, "Levels Panel", MenuTheme.ListTop);
            panel.AddCaption("level select");

            foreach (CampaignLevelEntry level in CampaignLevelList.All)
            {
                int number = level.Number;
                MenuItemView item = panel.AddNav(level.Label, () => StartCampaignLevel(number));
                panel.AddTag(item, level.MapName);
            }

            panel.AddGap(MenuTheme.SectionGap);
            panel.AddNav("back", ShowEraPanel);
            return panel;
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
            _customPanel.AddNav("back to menu", () => ShowHome(_main));

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
            SceneManager.LoadScene(SceneNames.CampaignLevel1);
        }

        static void StartCampaign() => StartCampaignLevel(CampaignRun.FirstLevel);

        static void StartCampaignLevel(int number)
        {
            CustomBattle.Clear();
            CampaignRun.Request(number);
            SceneManager.LoadScene(SceneNames.CampaignLevel1);
        }

        void ShowEraHeader(int index)
        {
            CareerEra era = CareerEras.All[index];
            _erasTitle.text = era.Title;
            _erasDescription.text = era.Description;
        }

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
            SetScreen(MenuScreen.Era);
            _eraTitle.text = CareerEras.All[index].Title;
            ShowEraPanel();
        }

        void ShowEraPanel()
        {
            _levelsPanel.SetActive(false);
            _eraPanel.SetActive(true);
            _group = _eraPanel;
        }

        void ShowLevels()
        {
            _eraPanel.SetActive(false);
            _levelsPanel.SetActive(true);
            _group = _levelsPanel;
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
            _customPage.SetActive(screen == MenuScreen.Custom);
            _planeView.SetActive(screen == MenuScreen.Home);
        }

        void Cancel()
        {
            if (_screen == MenuScreen.Era && _group == _levelsPanel)
            {
                ShowEraPanel();
                return;
            }
            if (_screen != MenuScreen.Home || _homePanel != _main) ShowHome(_main);
        }
    }
}
