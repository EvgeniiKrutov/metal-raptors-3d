using UnityEngine;
using UnityEngine.InputSystem;
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
        MenuPanel _customPanel;
        MenuCardRow _eras;

        GameObject _column;
        GameObject _erasPage;
        GameObject _eraPage;
        GameObject _customPage;

        Text _erasTitle;
        Text _erasDescription;
        Text _eraTitle;
        MenuPreviewCard _mapPreview;

        int _mapIndex;
        Daytime _daytime = Daytime.Morning;

        IMenuFocusGroup _group;
        MenuPanel _homePanel;
        MenuScreen _screen;

        static readonly string[] WeatherNames = { "morning", "midday", "evening", "night" };

        void Start()
        {
            var canvas = UIFactory.CreateCanvas("MainMenu Canvas");
            UIFactory.CreateBackground(canvas.transform, MenuTheme.Colors.Bg);

            Transform column = CreatePage(canvas.transform, "Menu Column", MenuTheme.ColumnFraction);
            _column = column.gameObject;
            BuildTitle(column, "METAL RAPTORS");
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

            int step = ReadStep();
            if (step != 0) _group.MoveFocus(step);

            int adjust = ReadAdjust();
            if (adjust != 0) _group.Adjust(adjust);

            if (ReadSubmit()) _group.ActivateFocused();
            if (ReadCancel()) Cancel();
        }

        static Transform CreatePage(Transform parent, string name, float widthFraction) =>
            CreateRegion(parent, name, 0f, widthFraction, MenuTheme.PadLeft);

        static Transform CreateRegion(Transform parent, string name, float xMin, float xMax, float padLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f - MenuTheme.PadTopFraction);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(padLeft, 0f);
            rt.offsetMax = new Vector2(-MenuTheme.PadRight, 0f);
            return go.transform;
        }

        static Transform CreateScreen(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        static Text BuildTitle(Transform page, string title)
        {
            Text text = UIFactory.CreateLabel(page, title, MenuTheme.TitleSize, 0f,
                MenuTheme.TitleRowHeight, MenuTheme.Colors.Fg, UIFactory.BoldFont);

            UIFactory.CreateRule(page, -(MenuTheme.TitleRowHeight + MenuTheme.TitleToBar),
                new Vector2(MenuTheme.BarWidth, MenuTheme.BarHeight), MenuTheme.Colors.Accent);
            return text;
        }

        MenuPanel BuildMainPanel(Transform column)
        {
            var panel = new MenuPanel(column, "Main Panel", MenuTheme.ListTop);
            panel.AddNav("career", ShowEras);
            panel.AddNav("challenges", null, interactable: false);
            panel.AddNav("custom battle", ShowCustom);
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
            Transform page = CreatePage(parent, "Career Page", 1f);
            _erasTitle = BuildTitle(page, string.Empty);

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
            Transform page = CreatePage(parent, "Era Page", MenuTheme.ColumnFraction);
            _eraTitle = BuildTitle(page, string.Empty);

            _eraPanel = new MenuPanel(page, "Era Panel", MenuTheme.ListTop);
            _eraPanel.AddNav("start", StartCampaign);
            _eraPanel.AddNav("level select", null, interactable: false);

            _eraPanel.AddGap(MenuTheme.SectionGap);
            _eraPanel.AddNav("back", () => ShowHome(_main));
            return page.gameObject;
        }

        GameObject BuildCustomPage(Transform parent)
        {
            Transform screen = CreateScreen(parent, "Custom Page");
            Transform column = CreatePage(screen, "Custom Column", MenuTheme.ColumnFraction);
            BuildTitle(column, "CUSTOM BATTLE");

            _customPanel = new MenuPanel(column, "Custom Panel", MenuTheme.ListTop);
            _customPanel.AddSelector("map", BattleMaps.Names(), _mapIndex, PickMap);
            _customPanel.AddSelector("weather", WeatherNames, (int)_daytime, PickWeather);

            _customPanel.AddGap(MenuTheme.SectionGap);
            _customPanel.AddNav("start level", StartCustomBattle);

            _customPanel.AddGap(MenuTheme.SectionGap);
            _customPanel.AddNav("back to menu", () => ShowHome(_main));

            Transform band = CreateRegion(screen, "Custom Preview", MenuTheme.ColumnFraction, 1f, 0f);
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
            $"{BattleMaps.All[_mapIndex].Name} | {WeatherNames[(int)_daytime]}";

        void StartCustomBattle()
        {
            CustomBattle.Request(BattleMaps.All[_mapIndex], _daytime);
            SceneManager.LoadScene(SceneNames.CampaignLevel1);
        }

        static void StartCampaign()
        {
            CustomBattle.Clear();
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
            _eraPanel.SetActive(true);
            _group = _eraPanel;
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
        }

        void Cancel()
        {
            if (_screen != MenuScreen.Home || _homePanel != _main) ShowHome(_main);
        }

        static int ReadStep()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            bool forward = (kb != null && kb.downArrowKey.wasPressedThisFrame)
                           || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool back = (kb != null && kb.upArrowKey.wasPressedThisFrame)
                        || (pad != null && pad.dpad.up.wasPressedThisFrame);

            if (forward == back) return 0;
            return forward ? 1 : -1;
        }

        static int ReadAdjust()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            bool forward = (kb != null && kb.rightArrowKey.wasPressedThisFrame)
                           || (pad != null && pad.dpad.right.wasPressedThisFrame);
            bool back = (kb != null && kb.leftArrowKey.wasPressedThisFrame)
                        || (pad != null && pad.dpad.left.wasPressedThisFrame);

            if (forward == back) return 0;
            return forward ? 1 : -1;
        }

        static bool ReadSubmit()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                                                                   || kb.spaceKey.wasPressedThisFrame))
                   || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }

        static bool ReadCancel()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame)
                   || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }
    }
}
