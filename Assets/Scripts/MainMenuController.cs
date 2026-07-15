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
    /// Level 2 stays locked (via GameManager progress) until Level 1 is completed.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        GameObject _mainPanel;
        GameObject _levelPanel;

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

            UIFactory.CreateButton(panel.transform, "LEVEL 1", new Vector2(0, 80),
                () => SceneManager.LoadScene(SceneNames.Level1));

            bool level2Unlocked = GameManager.Instance == null || GameManager.Instance.IsLevelUnlocked(2);
            UIFactory.CreateButton(panel.transform, level2Unlocked ? "LEVEL 2" : "LEVEL 2  (LOCKED)",
                new Vector2(0, -30),
                () => SceneManager.LoadScene(SceneNames.Level2),
                interactable: level2Unlocked);

            UIFactory.CreateButton(panel.transform, "BACK", new Vector2(0, -170), ShowMain,
                new Vector2(320, 78));
            return panel;
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
