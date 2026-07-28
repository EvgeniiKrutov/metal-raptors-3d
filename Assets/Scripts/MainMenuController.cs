using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Builds the main menu at runtime: a flat text column on the left of the screen, and the
    /// two full-width career pages (the era cards and the chosen era's entries) it opens.
    /// See docs/main-menu.md.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        enum MenuScreen { Home, Eras, Era }

        MenuPanel _main;
        MenuPanel _challenges;
        MenuPanel _eraPanel;
        MenuCardRow _eras;

        GameObject _column;
        GameObject _erasPage;
        GameObject _eraPage;

        Text _erasTitle;
        Text _erasDescription;
        Text _eraTitle;

        IMenuFocusGroup _group;
        MenuPanel _homePanel;
        MenuScreen _screen;

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

            ShowHome(_main);
        }

        void Update()
        {
            if (_group == null) return;

            int step = ReadStep();
            if (step != 0) _group.MoveFocus(step);
            if (ReadSubmit()) _group.ActivateFocused();
            if (ReadCancel()) Cancel();
        }

        /// <summary>
        /// A page hung from the top of the canvas, taking <paramref name="widthFraction"/> of its
        /// width: 40% for the menu column, all of it for the era cards. Anchored in fractions,
        /// not reference pixels, so the split holds at any aspect ratio. Its left inset is the
        /// margin every screen reads from.
        /// </summary>
        static Transform CreatePage(Transform parent, string name, float widthFraction)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(widthFraction, 1f - MenuTheme.PadTopFraction);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(MenuTheme.PadLeft, 0f);
            rt.offsetMax = new Vector2(-MenuTheme.PadRight, 0f);
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
            panel.AddNav("challenges", () => ShowHome(_challenges));
            panel.AddNav("custom battle", () => SceneManager.LoadScene(SceneNames.Battlefield));
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

        /// <summary>The era picker: a header reading from the highlighted card, and the card row.</summary>
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

        /// <summary>
        /// One era's own page — back in the main menu's left column, since it is a list of
        /// entries and nothing needs the width. Only one era is unlocked, so the title is set
        /// from whichever card opened the page.
        /// </summary>
        GameObject BuildEraPage(Transform parent)
        {
            Transform page = CreatePage(parent, "Era Page", MenuTheme.ColumnFraction);
            _eraTitle = BuildTitle(page, string.Empty);

            _eraPanel = new MenuPanel(page, "Era Panel", MenuTheme.ListTop);
            _eraPanel.AddNav("start", () => SceneManager.LoadScene(SceneNames.CampaignLevel1));
            _eraPanel.AddNav("level select", null, interactable: false);

            _eraPanel.AddGap(MenuTheme.SectionGap);
            _eraPanel.AddNav("back", () => ShowHome(_main));
            return page.gameObject;
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

        void SetScreen(MenuScreen screen)
        {
            _screen = screen;
            _column.SetActive(screen == MenuScreen.Home);
            _erasPage.SetActive(screen == MenuScreen.Eras);
            _eraPage.SetActive(screen == MenuScreen.Era);
        }

        /// <summary>
        /// Escape goes wherever the screen's own back entry goes — the main list from
        /// everywhere, since the era picker is a step through career, not a layer to sit in.
        /// </summary>
        void Cancel()
        {
            if (_screen != MenuScreen.Home || _homePanel != _main) ShowHome(_main);
        }

        static int ReadStep()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            bool forward = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
                           || (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame));
            bool back = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame))
                        || (pad != null && (pad.dpad.up.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame));

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
