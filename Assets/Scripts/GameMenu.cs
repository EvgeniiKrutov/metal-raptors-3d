using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetalRaptors
{
    public enum GameMenuKind { Pause, Failed, Completed }

    public class GameMenu : MonoBehaviour
    {
        public static GameMenu Current { get; private set; }
        public static bool IsOpen => Current != null || _pending;

        static readonly Color Scrim = new Color(0f, 0f, 0f, 0.6f);

        static bool _pending;

        MenuPanel _panel;
        GameObject _hud;
        bool _closable;
        int _openedFrame;

        public static void Open(GameMenuKind kind, string subtitle, GameObject hud,
            string nextScene = null, Action beforeNext = null)
        {
            if (IsOpen) return;

            if (kind == GameMenuKind.Pause)
            {
                Create(kind, subtitle, hud, nextScene, beforeNext);
                return;
            }

            _pending = true;
            ScreenFade.Swap(() =>
            {
                _pending = false;
                Create(kind, subtitle, hud, nextScene, beforeNext);
            });
        }

        static void Create(GameMenuKind kind, string subtitle, GameObject hud, string nextScene,
            Action beforeNext)
        {
            Canvas canvas = UIFactory.CreateCanvas("Game Menu");
            canvas.sortingOrder = 200;

            var menu = canvas.gameObject.AddComponent<GameMenu>();
            Current = menu;
            menu.Build(canvas, kind, subtitle, hud, nextScene, beforeNext);
        }

        void Build(Canvas canvas, GameMenuKind kind, string subtitle, GameObject hud, string nextScene,
            Action beforeNext)
        {
            _hud = hud;
            _closable = kind == GameMenuKind.Pause;
            _openedFrame = Time.frameCount;

            if (_hud != null) _hud.SetActive(false);
            Time.timeScale = 0f;

            MenuLayout.CreateBand(canvas.transform, "Menu Band", 0f, MenuTheme.ColumnFraction,
                MenuTheme.Colors.Bg);
            MenuLayout.CreateBand(canvas.transform, "Scrim Band", MenuTheme.ColumnFraction, 1f, Scrim);

            Transform column = MenuLayout.CreatePage(canvas.transform, "Menu Column", MenuTheme.ColumnFraction);
            MenuLayout.BuildTitle(column, TitleFor(kind));

            _panel = new MenuPanel(column, "Menu Panel", MenuTheme.ListTop);

            _panel.AddNav(subtitle, null, interactable: false);

            if (kind == GameMenuKind.Pause) _panel.AddNav("resume", Close);

            _panel.AddNav("restart", Restart);

            if (kind == GameMenuKind.Completed)
            {
                bool hasNext = !string.IsNullOrEmpty(nextScene);
                _panel.AddNav("next level", hasNext
                    ? (Action)(() => { beforeNext?.Invoke(); Load(nextScene); })
                    : null, hasNext);
            }

            _panel.AddNav("options", null, interactable: false);

            _panel.AddGap(MenuTheme.SectionGap);
            _panel.AddNav("quit to menu", () => Load(SceneNames.MainMenu));

            _panel.FocusFirst();
        }

        static string TitleFor(GameMenuKind kind)
        {
            switch (kind)
            {
                case GameMenuKind.Failed: return "LEVEL FAILED";
                case GameMenuKind.Completed: return "LEVEL COMPLETED";
                default: return "PAUSE";
            }
        }

        void Update()
        {
            if (_panel == null || ScreenFade.IsBusy) return;

            int step = MenuInput.ReadStep();
            if (step != 0) _panel.MoveFocus(step);

            int adjust = MenuInput.ReadAdjust();
            if (adjust != 0) _panel.Adjust(adjust);

            if (MenuInput.ReadSubmit()) _panel.ActivateFocused();

            if (_closable && Time.frameCount != _openedFrame && MenuInput.ReadCancel()) Close();
        }

        public void Close()
        {
            Release();
            if (_hud != null) _hud.SetActive(true);
            Destroy(gameObject);
        }

        void Restart() => Load(SceneManager.GetActiveScene().name);

        void Load(string scene) => ScreenFade.Load(scene, Release);

        void Release()
        {
            if (Current == this) Current = null;
            _pending = false;
            Time.timeScale = 1f;
        }

        void OnDestroy() => Release();
    }
}
