using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Builds the main menu at runtime: black background, white "METAL RAPTORS" title,
    /// and four buttons — CAMPAIGN, AIR FIGHTS, BATTLEFIELD, GARAGE.
    ///
    ///   * CAMPAIGN     swaps to a sub-panel with the endless Level 1 (see docs/campaign.md).
    ///   * AIR FIGHTS   swaps to a sub-panel to pick Level 1 / Level 2.
    ///   * BATTLEFIELD  loads the Battlefield scene.
    ///   * GARAGE       loads the Garage scene.
    ///
    /// Level 2 stays locked (via GameManager progress) until Level 1 is completed. Both the
    /// level select and the campaign panel carry a weather selector (see docs/atmospheres.md).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        static readonly (string label, Daytime daytime)[] WeatherOptions =
        {
            ("MORNING", Daytime.Morning),
            ("MIDDAY", Daytime.Midday),
            ("EVENING", Daytime.Evening),
            ("NIGHT", Daytime.Night),
        };

        static readonly Color OptionOnColor = new Color(0.85f, 0.50f, 0.20f);
        static readonly Color OptionOffColor = new Color(0.13f, 0.14f, 0.17f);

        GameObject _mainPanel;
        GameObject _levelPanel;
        GameObject _campaignPanel;

        void Start()
        {
            var canvas = UIFactory.CreateCanvas("MainMenu Canvas");
            UIFactory.CreateBackground(canvas.transform, Color.black);

            UIFactory.CreateText(canvas.transform, "METAL RAPTORS", 110,
                new Vector2(0, 300), new Vector2(1600, 200), TextAnchor.MiddleCenter, FontStyle.Bold);

            _mainPanel = BuildMainPanel(canvas.transform);
            _levelPanel = BuildLevelPanel(canvas.transform);
            _campaignPanel = BuildCampaignPanel(canvas.transform);

            Show(_mainPanel);
        }

        GameObject BuildMainPanel(Transform parent)
        {
            var panel = NewPanel(parent, "Main Panel");
            UIFactory.CreateButton(panel.transform, "CAMPAIGN", new Vector2(0, 40),
                () => Show(_campaignPanel));
            UIFactory.CreateButton(panel.transform, "AIR FIGHTS", new Vector2(0, -70),
                () => Show(_levelPanel));
            UIFactory.CreateButton(panel.transform, "BATTLEFIELD", new Vector2(0, -180),
                () => SceneManager.LoadScene(SceneNames.Battlefield));
            UIFactory.CreateButton(panel.transform, "GARAGE", new Vector2(0, -290),
                () => SceneManager.LoadScene(SceneNames.Garage));
            return panel;
        }

        GameObject BuildLevelPanel(Transform parent)
        {
            var panel = NewPanel(parent, "Level Select Panel");

            UIFactory.CreateText(panel.transform, "SELECT MISSION", 46,
                new Vector2(0, 200), new Vector2(900, 70), TextAnchor.MiddleCenter, FontStyle.Bold);

            UIFactory.CreateButton(panel.transform, "LEVEL 1", new Vector2(0, 105),
                () => SceneManager.LoadScene(SceneNames.Level1));

            BuildWeatherSelector(panel.transform, "LEVEL 1 WEATHER",
                () => GameManager.Instance != null ? GameManager.Instance.Level1Daytime : Daytime.Midday,
                d => GameManager.Instance?.SetLevel1Daytime(d));

            bool level2Unlocked = GameManager.Instance == null || GameManager.Instance.IsLevelUnlocked(2);
            UIFactory.CreateButton(panel.transform, level2Unlocked ? "LEVEL 2" : "LEVEL 2  (LOCKED)",
                new Vector2(0, -100),
                () => SceneManager.LoadScene(SceneNames.Level2),
                interactable: level2Unlocked);

            UIFactory.CreateButton(panel.transform, "BACK", new Vector2(0, -210),
                () => Show(_mainPanel), new Vector2(320, 78));
            return panel;
        }

        GameObject BuildCampaignPanel(Transform parent)
        {
            var panel = NewPanel(parent, "Campaign Panel");

            UIFactory.CreateText(panel.transform, "CAMPAIGN", 46,
                new Vector2(0, 200), new Vector2(900, 70), TextAnchor.MiddleCenter, FontStyle.Bold);

            UIFactory.CreateButton(panel.transform, "LEVEL 1", new Vector2(0, 105),
                () => SceneManager.LoadScene(SceneNames.CampaignLevel1));

            BuildWeatherSelector(panel.transform, "WEATHER",
                () => GameManager.Instance != null ? GameManager.Instance.CampaignDaytime : Daytime.Midday,
                d => GameManager.Instance?.SetCampaignDaytime(d));

            UIFactory.CreateButton(panel.transform, "BACK", new Vector2(0, -120),
                () => Show(_mainPanel), new Vector2(320, 78));
            return panel;
        }

        /// <summary>A weather row (caption plus one button per option, the chosen one lit warm)
        /// bound to a daytime slot in <see cref="GameManager"/> via the get/set pair.</summary>
        void BuildWeatherSelector(Transform parent, string caption,
            Func<Daytime> get, Action<Daytime> set)
        {
            UIFactory.CreateText(parent, caption, 22, new Vector2(0, 40),
                new Vector2(600, 30)).color = new Color(0.65f, 0.65f, 0.65f);

            var buttons = new Button[WeatherOptions.Length];
            for (int i = 0; i < WeatherOptions.Length; i++)
            {
                Daytime daytime = WeatherOptions[i].daytime;
                buttons[i] = UIFactory.CreateButton(parent, WeatherOptions[i].label,
                    new Vector2(-270 + i * 180, -10),
                    () =>
                    {
                        set(daytime);
                        RefreshWeatherSelector(buttons, get());
                    },
                    new Vector2(170, 52), fontSize: 24);
            }
            RefreshWeatherSelector(buttons, get());
        }

        static void RefreshWeatherSelector(Button[] buttons, Daytime selected)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                Color baseColor = WeatherOptions[i].daytime == selected ? OptionOnColor : OptionOffColor;
                var button = buttons[i];
                var colors = button.colors;
                colors.normalColor = baseColor;
                colors.selectedColor = baseColor;
                button.colors = colors;
                ((Image)button.targetGraphic).color = baseColor;
            }
        }

        static GameObject NewPanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, -100);
            rt.sizeDelta = new Vector2(600, 600);
            return go;
        }

        void Show(GameObject panel)
        {
            _mainPanel.SetActive(panel == _mainPanel);
            _levelPanel.SetActive(panel == _levelPanel);
            _campaignPanel.SetActive(panel == _campaignPanel);
        }
    }
}
