using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    public enum GameMenuKind { Pause, Failed, Completed }

    public class GameMenu : MonoBehaviour
    {
        public static GameMenu Current { get; private set; }
        public static bool IsOpen => Current != null || _pending;

        static readonly Color Scrim = new Color(0f, 0f, 0f, 0.6f);

        const float ExpandSec = 0.28f;
        const float MaxStep = 0.05f;

        static bool _pending;

        MenuPanel _panel;
        MenuItemView _optionsItem;
        GameObject _hud;
        Image _band;
        Image _scrim;
        CanvasGroup _columnGroup;
        OptionsPage _options;
        CanvasGroup _optionsGroup;
        bool _optionsOpen;
        bool _sliding;
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

            _band = MenuLayout.CreateBand(canvas.transform, "Menu Band", 0f, MenuTheme.ColumnFraction,
                MenuTheme.Colors.Bg);
            _scrim = MenuLayout.CreateBand(canvas.transform, "Scrim Band", MenuTheme.ColumnFraction, 1f, Scrim);

            Transform column = MenuLayout.CreatePage(canvas.transform, "Menu Column", MenuTheme.ColumnFraction);
            _columnGroup = column.gameObject.AddComponent<CanvasGroup>();
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

            _optionsItem = _panel.AddNav("options", OpenOptions);

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

        void OpenOptions()
        {
            if (_sliding || _optionsOpen) return;

            if (_options == null)
            {
                _options = new OptionsPage(transform, CloseOptions);
                _optionsGroup = _options.Root.AddComponent<CanvasGroup>();
            }

            _optionsOpen = true;
            _options.SetActive(true);
            StartCoroutine(Slide(MenuTheme.ColumnFraction, 1f, _columnGroup, _optionsGroup));
        }

        void CloseOptions()
        {
            if (_sliding || !_optionsOpen) return;

            _optionsOpen = false;
            StartCoroutine(Slide(1f, MenuTheme.ColumnFraction, _optionsGroup, _columnGroup));
        }

        IEnumerator Slide(float from, float to, CanvasGroup fadeOut, CanvasGroup fadeIn)
        {
            _sliding = true;

            fadeIn.gameObject.SetActive(true);
            fadeIn.alpha = 0f;
            fadeIn.blocksRaycasts = false;
            fadeOut.blocksRaycasts = false;

            for (float t = 0f; t < ExpandSec; t += Mathf.Min(Time.unscaledDeltaTime, MaxStep))
            {
                float k = Ease(Mathf.Clamp01(t / ExpandSec));
                SetSplit(Mathf.Lerp(from, to, k));
                fadeOut.alpha = Mathf.Clamp01(1f - 2f * k);
                fadeIn.alpha = Mathf.Clamp01(2f * k - 1f);
                yield return null;
            }

            SetSplit(to);
            fadeOut.alpha = 0f;
            fadeOut.gameObject.SetActive(false);
            fadeIn.alpha = 1f;
            fadeIn.blocksRaycasts = true;
            _sliding = false;

            if (_optionsOpen) _options.Enter();
            else _panel.Focus(_optionsItem);
        }

        void SetSplit(float x)
        {
            _band.rectTransform.anchorMax = new Vector2(x, 1f);
            _scrim.rectTransform.anchorMin = new Vector2(x, 0f);
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        void UpdateOptions()
        {
            int step = MenuInput.ReadStep();
            if (step != 0) _options.MoveFocus(step);

            int adjust = MenuInput.ReadAdjust();
            if (adjust != 0) _options.Adjust(adjust);

            if (MenuInput.ReadSubmit()) _options.ActivateFocused();
            if (MenuInput.ReadCancel() && !_options.Cancel()) CloseOptions();
        }

        void Update()
        {
            if (_panel == null || ScreenFade.IsBusy || _sliding) return;

            if (_optionsOpen)
            {
                UpdateOptions();
                return;
            }

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
