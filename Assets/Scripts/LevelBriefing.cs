using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LevelBriefing : MonoBehaviour
    {
        public const string KeyPrompt = "Press any key to continue...";
        public const string TouchPrompt = "Tap anywhere to continue...";

        public static string Prompt => MenuInput.IsTouchPlatform ? TouchPrompt : KeyPrompt;

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

        const float CharsPerSecond = 55f;
        const float LoreSpeedFactor = 2.2f;
        const float LineGapSec = 0.35f;
        const float RuleGapSec = 0.5f;
        const float PromptDelaySec = 2f;
        const float PromptFadeSec = 0.35f;

        const string HiddenOpen = "<color=#00000000>";
        const string HiddenClose = "</color>";

        static readonly Vector2 RuleSize = new Vector2(96f, 4f);

        public static bool IsOpen => Current != null;

        static LevelBriefing Current;

        struct Printed
        {
            public Text text;
            public string full;
            public float speed;
            public float gapAfter;
        }

        readonly List<Printed> _lines = new List<Printed>();

        GameObject _hud;
        Action _onDismissed;
        Image _rule;
        CanvasGroup _prompt;
        Image _caret;
        float _blink;
        int _openedFrame;
        bool _closing;

        int _line;
        int _ruleAfter;
        float _shown;
        float _gap;
        float _hold = PromptDelaySec;
        float _promptFade;
        bool _printed;

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

            Print(Centered(page, caption, CaptionSize, CaptionY, colors.Muted, UIFactory.MediumFont),
                caption, 1f, LineGapSec);
            Print(Centered(page, title, TitleSize, TitleY, colors.Fg, UIFactory.BoldFont),
                title, 1f, LineGapSec);
            Print(Centered(page, dateline, DatelineSize, DatelineY, colors.Muted, UIFactory.RegularFont),
                dateline, 1f, RuleGapSec);

            _ruleAfter = _lines.Count;
            _rule = BuildRule(page, colors.Accent);
            _rule.enabled = false;

            Text body = BuildLore(page, lore, colors.Fg, out float loreBottom);
            Print(body, lore, LoreSpeedFactor, 0f);

            BuildPrompt(page, PromptRow(loreBottom), colors.Muted, colors.Accent);
        }

        void Print(Text text, string content, float speed, float gapAfter)
        {
            _lines.Add(new Printed
            {
                text = text,
                full = content ?? string.Empty,
                speed = speed,
                gapAfter = gapAfter,
            });
            Paint(text, content, 0);
        }

        static void Paint(Text text, string full, int count)
        {
            if (text == null) return;

            full = full ?? string.Empty;
            text.text = count >= full.Length
                ? full
                : full.Substring(0, count) + HiddenOpen + full.Substring(count) + HiddenClose;
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

        static Image BuildRule(Transform parent, Color color)
        {
            var go = new GameObject("Rule", typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.sizeDelta = RuleSize;
            rt.anchoredPosition = new Vector2(0f, RuleY);
            return image;
        }

        static Text BuildLore(Transform parent, string lore, Color color, out float bottom)
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

            bottom = LoreTop - text.preferredHeight;
            return text;
        }

        void BuildPrompt(Transform parent, float y, Color color, Color caretColor)
        {
            var row = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup));
            row.transform.SetParent(parent, false);

            var rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = Vector2.zero;
            rowRt.anchoredPosition = Vector2.zero;

            _prompt = row.GetComponent<CanvasGroup>();
            _prompt.alpha = 0f;
            _prompt.blocksRaycasts = false;
            _prompt.interactable = false;

            Text text = Centered(row.transform, Prompt, PromptSize, y, color, UIFactory.MediumFont);

            var go = new GameObject("Caret", typeof(Image));
            go.transform.SetParent(row.transform, false);

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
            float deltaTime = Time.unscaledDeltaTime;

            if (!_printed)
            {
                if (ScreenFade.IsBusy) return;
                if (MenuInput.ReadSkip()) FinishPrinting();
                else Advance(deltaTime);
                return;
            }

            FadeInPrompt(deltaTime);
            Blink(deltaTime);

            if (_closing || ScreenFade.IsBusy || Time.frameCount == _openedFrame) return;
            if (MenuInput.ReadAnyKey()) Close();
        }

        void Advance(float deltaTime)
        {
            if (_gap > 0f)
            {
                _gap -= deltaTime;
                return;
            }

            if (_line < _lines.Count)
            {
                Printed line = _lines[_line];
                int was = Mathf.FloorToInt(_shown);
                _shown = Mathf.Min(_shown + deltaTime * CharsPerSecond * line.speed, line.full.Length);

                int now = Mathf.FloorToInt(_shown);
                if (now != was) Paint(line.text, line.full, now);

                if (_shown < line.full.Length) return;

                Paint(line.text, line.full, line.full.Length);
                _gap = line.gapAfter;
                _shown = 0f;
                _line++;
                if (_line == _ruleAfter && _rule != null) _rule.enabled = true;
                return;
            }

            _hold -= deltaTime;
            if (_hold <= 0f) _printed = true;
        }

        void FinishPrinting()
        {
            for (int i = _line; i < _lines.Count; i++)
                Paint(_lines[i].text, _lines[i].full, _lines[i].full.Length);

            _line = _lines.Count;
            if (_rule != null) _rule.enabled = true;
            _hold = 0f;
            _printed = true;
        }

        void FadeInPrompt(float deltaTime)
        {
            if (_prompt == null || _promptFade >= PromptFadeSec) return;

            _promptFade = Mathf.Min(_promptFade + deltaTime, PromptFadeSec);
            _prompt.alpha = _promptFade / PromptFadeSec;
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
