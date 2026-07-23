using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Builds the main menu at runtime: black background, white "METAL RAPTORS" title,
    /// and three buttons — AIR FIGHTS, BATTLEFIELD, GARAGE.
    ///
    ///   * AIR FIGHTS   swaps to a sub-panel to pick Level 1 / Level 2.
    ///   * BATTLEFIELD  loads the Battlefield scene.
    ///   * GARAGE       loads the Garage scene.
    ///
    /// Level 2 stays locked (via GameManager progress) until Level 1 is completed. The level
    /// select also carries Level 1's weather selector (see docs/atmospheres.md).
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
        Button[] _weatherButtons;

        void Start()
        {
            var canvas = UIFactory.CreateCanvas("MainMenu Canvas");
            UIFactory.CreateBackground(canvas.transform, Color.black);

            UIFactory.CreateText(canvas.transform, "METAL RAPTORS", 110,
                new Vector2(0, 300), new Vector2(1600, 200), TextAnchor.MiddleCenter, FontStyle.Bold);

            _mainPanel = BuildMainPanel(canvas.transform);
            _levelPanel = BuildLevelPanel(canvas.transform);

            ShowMain();
        }

        GameObject BuildMainPanel(Transform parent)
        {
            var panel = NewPanel(parent, "Main Panel");
            UIFactory.CreateButton(panel.transform, "AIR FIGHTS", new Vector2(0, 40), ShowLevels);
            UIFactory.CreateButton(panel.transform, "BATTLEFIELD", new Vector2(0, -70),
                () => SceneManager.LoadScene(SceneNames.Battlefield));
            UIFactory.CreateButton(panel.transform, "GARAGE", new Vector2(0, -180),
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

            BuildWeatherSelector(panel.transform);

            bool level2Unlocked = GameManager.Instance == null || GameManager.Instance.IsLevelUnlocked(2);
            UIFactory.CreateButton(panel.transform, level2Unlocked ? "LEVEL 2" : "LEVEL 2  (LOCKED)",
                new Vector2(0, -100),
                () => SceneManager.LoadScene(SceneNames.Level2),
                interactable: level2Unlocked);

            UIFactory.CreateButton(panel.transform, "BACK", new Vector2(0, -210), ShowMain,
                new Vector2(320, 78));
            return panel;
        }

        /// <summary>Level 1's weather row: caption plus one button per option, the chosen one
        /// lit warm. The pick lands in <see cref="GameManager.Level1Daytime"/>.</summary>
        void BuildWeatherSelector(Transform parent)
        {
            UIFactory.CreateText(parent, "LEVEL 1 WEATHER", 22, new Vector2(0, 40),
                new Vector2(600, 30)).color = new Color(0.65f, 0.65f, 0.65f);

            _weatherButtons = new Button[WeatherOptions.Length];
            for (int i = 0; i < WeatherOptions.Length; i++)
            {
                Daytime daytime = WeatherOptions[i].daytime;
                _weatherButtons[i] = UIFactory.CreateButton(parent, WeatherOptions[i].label,
                    new Vector2(-270 + i * 180, -10),
                    () =>
                    {
                        if (GameManager.Instance != null)
                            GameManager.Instance.SetLevel1Daytime(daytime);
                        RefreshWeatherSelector();
                    },
                    new Vector2(170, 52), fontSize: 24);
            }
            RefreshWeatherSelector();
        }

        void RefreshWeatherSelector()
        {
            Daytime selected = GameManager.Instance != null
                ? GameManager.Instance.Level1Daytime : Daytime.Midday;

            for (int i = 0; i < _weatherButtons.Length; i++)
            {
                Color baseColor = WeatherOptions[i].daytime == selected ? OptionOnColor : OptionOffColor;
                var button = _weatherButtons[i];
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

        void ShowMain()
        {
            _mainPanel.SetActive(true);
            _levelPanel.SetActive(false);
        }

        void ShowLevels()
        {
            _mainPanel.SetActive(false);
            _levelPanel.SetActive(true);
        }
    }
}
