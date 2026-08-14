using System;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LevelBriefing : MonoBehaviour
    {
        public const string Prompt = "Press any key to continue...";

        const int SortingOrder = 300;

        const int CaptionSize = 22;
        const float CaptionY = 452f;
        const int TitleSize = 62;
        const float TitleY = 376f;
        const int DatelineSize = 22;
        const float DatelineY = 310f;
        const float RuleY = 266f;
        const int LoreSize = 26;
        const float LoreTop = 214f;
        const float LoreWidth = 1120f;
        const float LoreHeight = 520f;
        const float LoreLineSpacing = 1.5f;
        const int PromptSize = 24;
        const float PromptY = -400f;
        const float PromptGap = 56f;
        const float PromptFloor = -470f;

        const float CaretGap = 12f;
        const float CaretWidth = 12f;
        const float CaretHeight = 26f;
        const float CaretDrop = 2f;
        const float CaretBlinkSec = 0.5f;

        static readonly Vector2 RuleSize = new Vector2(96f, 4f);

        public static bool IsOpen => Current != null;

        static LevelBriefing Current;

        GameObject _hud;
        Action _onDismissed;
        Image _caret;
        float _blink;
        int _openedFrame;
        bool _closing;

        public static void Open(string caption, string title, string dateline, string lore,
            GameObject hud, Action onDismissed = null)
        {
            if (IsOpen || string.IsNullOrEmpty(title))
            {
                onDismissed?.Invoke();
                return;
            }

            Canvas canvas = UIFactory.CreateCanvas("Level Briefing");
            canvas.sortingOrder = SortingOrder;

            var briefing = canvas.gameObject.AddComponent<LevelBriefing>();
            Current = briefing;
            briefing._onDismissed = onDismissed;
            briefing.Build(canvas, caption, title, dateline, lore, hud);
        }

        void Build(Canvas canvas, string caption, string title, string dateline, string lore, GameObject hud)
        {
            _hud = hud;
            _openedFrame = Time.frameCount;

            if (_hud != null) _hud.SetActive(false);
            Time.timeScale = 0f;

            MenuPalette colors = MenuTheme.Colors;
            Transform page = canvas.transform;

            UIFactory.CreateBackground(page, colors.Bg);

            Centered(page, caption, CaptionSize, CaptionY, colors.Muted, UIFactory.MediumFont);
            Centered(page, title, TitleSize, TitleY, colors.Fg, UIFactory.BoldFont);
            Centered(page, dateline, DatelineSize, DatelineY, colors.Muted, UIFactory.RegularFont);
            BuildRule(page, colors.Accent);
            float loreBottom = BuildLore(page, lore, colors.Fg);
            BuildPrompt(page, PromptRow(loreBottom), colors.Muted, colors.Accent);
        }

        static float PromptRow(float loreBottom) => Mathf.Max(PromptFloor,
            Mathf.Min(PromptY, loreBottom - PromptGap));

        static Text Centered(Transform parent, string content, int fontSize, float y, Color color, Font font)
        {
            Text text = UIFactory.CreateText(parent, content, fontSize, new Vector2(0f, y),
                new Vector2(LoreWidth, fontSize * 1.6f));
            text.color = color;
            text.font = font;
            return text;
        }

        static void BuildRule(Transform parent, Color color)
        {
            var go = new GameObject("Rule", typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.sizeDelta = RuleSize;
            rt.anchoredPosition = new Vector2(0f, RuleY);
        }

        static float BuildLore(Transform parent, string lore, Color color)
        {
            Text text = UIFactory.CreateText(parent, lore, LoreSize, Vector2.zero,
                new Vector2(LoreWidth, LoreHeight), TextAnchor.UpperCenter);
            text.color = color;
            text.font = UIFactory.RegularFont;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = LoreLineSpacing;

            var rt = text.rectTransform;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, LoreTop);

            return LoreTop - text.preferredHeight;
        }

        void BuildPrompt(Transform parent, float y, Color color, Color caretColor)
        {
            Text text = Centered(parent, Prompt, PromptSize, y, color, UIFactory.MediumFont);

            var go = new GameObject("Caret", typeof(Image));
            go.transform.SetParent(parent, false);

            _caret = go.GetComponent<Image>();
            _caret.color = caretColor;
            _caret.raycastTarget = false;

            var rt = _caret.rectTransform;
            rt.sizeDelta = new Vector2(CaretWidth, CaretHeight);
            rt.anchoredPosition = new Vector2(
                text.preferredWidth * 0.5f + CaretGap + CaretWidth * 0.5f, y - CaretDrop);
        }

        void Update()
        {
            Blink(Time.unscaledDeltaTime);

            if (_closing || ScreenFade.IsBusy || Time.frameCount == _openedFrame) return;
            if (MenuInput.ReadAnyKey()) Close();
        }

        void Blink(float deltaTime)
        {
            if (_caret == null) return;

            _blink += deltaTime;
            while (_blink >= CaretBlinkSec)
            {
                _blink -= CaretBlinkSec;
                _caret.enabled = !_caret.enabled;
            }
        }

        void Close()
        {
            _closing = true;
            ScreenFade.Swap(() =>
            {
                Release();
                if (_hud != null) _hud.SetActive(true);
                _onDismissed?.Invoke();
                Destroy(gameObject);
            });
        }

        void Release()
        {
            if (Current != this) return;
            Current = null;
            Time.timeScale = 1f;
        }

        void OnDestroy() => Release();
    }
}
