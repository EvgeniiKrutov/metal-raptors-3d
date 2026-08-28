using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LevelOutro : MonoBehaviour
    {
        public const string JournalTitle = "JOURNAL OF É. VASSEUR";

        const int SortingOrder = 300;

        const float PadSide = 96f;
        const float PadTop = 84f;
        const float PadBottom = 72f;

        const int NameSize = 24;
        const float NameRow = 30f;
        const int BodySize = 30;
        const float BodyLineSpacing = 1.25f;
        const float NameGap = 4f;
        const float LineGap = 30f;
        const float AvatarSize = 148f;
        const float AvatarGap = 26f;

        const float RiseSec = 0.4f;
        const float RiseFrom = 26f;
        const float ScrollResponse = 9f;

        const int PromptSize = 24;
        const float PromptGap = 44f;
        const float PromptDelaySec = 2f;
        const float PromptFadeSec = 0.35f;

        const float CaretGap = 12f;
        const float CaretWidth = 12f;
        const float CaretHeight = 26f;
        const float CaretDrop = 2f;
        const float CaretBlinkSec = 0.5f;

        static readonly Color Ink = new Color(0f, 0f, 0f, 1f);
        static readonly Color Body = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color PlayerAccent = new Color(0.53f, 0.79f, 0.93f);
        static readonly Color NpcAccent = new Color(0.92f, 0.70f, 0.36f);
        static readonly Color Muted = new Color(0.62f, 0.62f, 0.60f);

        public static bool IsOpen => Current != null;

        static LevelOutro Current;

        class Spoken
        {
            public CanvasGroup group;
            public RectTransform rect;
            public float top;
            public float rise;
        }

        readonly List<Spoken> _said = new List<Spoken>();

        CampaignOutroLine[] _lines;
        Action _onDismissed;
        RectTransform _column;
        RectTransform _stack;
        CanvasGroup _prompt;
        Image _caret;

        float _stackHeight;
        float _scroll;
        float _next;
        int _line;
        float _hold = PromptDelaySec;
        float _promptFade;
        float _blink;
        int _openedFrame;
        bool _printed;
        bool _closing;

        public static void Open(CampaignOutroLine[] lines, Action onDismissed)
        {
            if (IsOpen || lines == null || lines.Length == 0)
            {
                onDismissed?.Invoke();
                return;
            }

            Canvas canvas = UIFactory.CreateCanvas("Level Outro");
            canvas.sortingOrder = SortingOrder;

            var outro = canvas.gameObject.AddComponent<LevelOutro>();
            Current = outro;
            outro._lines = lines;
            outro._onDismissed = onDismissed;
            outro.Build(canvas);
        }

        void Build(Canvas canvas)
        {
            _openedFrame = Time.frameCount;
            Time.timeScale = 0f;

            Transform root = canvas.transform;
            UIFactory.CreateBackground(root, Ink);

            _column = Column(root);

            var stack = new GameObject("Lines", typeof(RectTransform));
            stack.transform.SetParent(_column, false);

            _stack = (RectTransform)stack.transform;
            _stack.anchorMin = new Vector2(0f, 1f);
            _stack.anchorMax = new Vector2(1f, 1f);
            _stack.pivot = new Vector2(0.5f, 1f);
            _stack.sizeDelta = Vector2.zero;
            _stack.anchoredPosition = Vector2.zero;

            BuildPrompt(root);
        }

        static RectTransform Column(Transform parent)
        {
            var go = new GameObject("Column", typeof(RectTransform), typeof(RectMask2D));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(MenuTheme.SafeLeft + PadSide,
                MenuTheme.SafeBottom + PadBottom + PromptGap + PromptSize);
            rt.offsetMax = new Vector2(-(MenuTheme.SafeRight + PadSide),
                -(MenuTheme.SafeTop + PadTop));
            return rt;
        }

        void BuildPrompt(Transform parent)
        {
            var row = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup));
            row.transform.SetParent(parent, false);

            var rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = new Vector2(0.5f, 0f);
            rowRt.anchorMax = new Vector2(0.5f, 0f);
            rowRt.pivot = new Vector2(0.5f, 0f);
            rowRt.sizeDelta = Vector2.zero;
            rowRt.anchoredPosition = new Vector2(0f, MenuTheme.SafeBottom + PadBottom);

            _prompt = row.GetComponent<CanvasGroup>();
            _prompt.alpha = 0f;
            _prompt.blocksRaycasts = false;
            _prompt.interactable = false;

            Text text = UIFactory.CreateText(row.transform, LevelBriefing.Prompt, PromptSize,
                Vector2.zero, new Vector2(900f, PromptSize * 1.6f));
            text.color = Muted;
            text.font = UIFactory.MediumFont;

            var go = new GameObject("Caret", typeof(Image));
            go.transform.SetParent(row.transform, false);

            _caret = go.GetComponent<Image>();
            _caret.color = NpcAccent;
            _caret.raycastTarget = false;

            var rt = _caret.rectTransform;
            rt.sizeDelta = new Vector2(CaretWidth, CaretHeight);
            rt.anchoredPosition = new Vector2(
                text.preferredWidth * 0.5f + CaretGap + CaretWidth * 0.5f, -CaretDrop);
        }

        void Speak(CampaignOutroLine entry)
        {
            CampaignSpeaker speaker = CampaignSpeakers.For(entry.Speaker);
            string message = DialogueLines.For(entry.Line);

            var go = new GameObject("Said", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_stack, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            float indent = Portrait(rect, speaker) ? AvatarSize + AvatarGap : 0f;

            Text label = Label(rect, speaker.Name, NameSize, UIFactory.BoldFont,
                speaker.IsPlayer ? PlayerAccent : NpcAccent, NameRow, 0f, indent);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text text = Label(rect, message, BodySize, UIFactory.MediumFont, Body, 0f,
                -(NameRow + NameGap), indent);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = BodyLineSpacing;

            float bodyHeight = text.preferredHeight;
            text.rectTransform.sizeDelta = new Vector2(-indent, bodyHeight);

            float height = Mathf.Max(indent > 0f ? AvatarSize : 0f,
                NameRow + NameGap + bodyHeight);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -_stackHeight);

            var said = new Spoken
            {
                group = go.GetComponent<CanvasGroup>(),
                rect = rect,
                top = _stackHeight,
            };
            said.group.alpha = 0f;
            _said.Add(said);

            _stackHeight += height + LineGap;
            _next = CampaignScript.ReadingTime(message);
        }

        static bool Portrait(Transform parent, CampaignSpeaker speaker)
        {
            Sprite face = CampaignAvatars.For(speaker);
            if (face == null) return false;

            var go = new GameObject("Avatar", typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = face;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(AvatarSize, AvatarSize);
            rt.anchoredPosition = Vector2.zero;
            return true;
        }

        Text Label(Transform parent, string content, int fontSize, Font font, Color color,
            float height, float top, float indent)
        {
            Text text = UIFactory.CreateText(parent, content, fontSize, Vector2.zero,
                new Vector2(-indent, height), TextAnchor.UpperLeft);
            text.color = color;
            text.font = font;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(indent * 0.5f, top);
            return text;
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            Animate(deltaTime);
            Scroll(deltaTime);

            if (!_printed)
            {
                if (ScreenFade.IsBusy) return;
                Advance(deltaTime);
                return;
            }

            FadeInPrompt(deltaTime);
            Blink(deltaTime);

            if (_closing || ScreenFade.IsBusy || Time.frameCount == _openedFrame) return;
            if (MenuInput.ReadAnyKey()) Close();
        }

        void Advance(float deltaTime)
        {
            bool skipped = MenuInput.ReadSkip();
            if (skipped) FinishRise();

            if (_next > 0f)
            {
                _next = skipped ? 0f : _next - deltaTime;
                if (_next > 0f) return;
            }

            if (_line < _lines.Length)
            {
                Speak(_lines[_line]);
                _line++;
                return;
            }

            _hold = skipped ? 0f : _hold - deltaTime;
            if (_hold <= 0f) _printed = true;
        }

        void FinishRise()
        {
            foreach (Spoken said in _said)
            {
                said.rise = RiseSec;
                said.group.alpha = 1f;
                said.rect.anchoredPosition = new Vector2(0f, -said.top);
            }
            _scroll = ScrollTarget;
            _stack.anchoredPosition = new Vector2(0f, _scroll);
        }

        void Animate(float deltaTime)
        {
            foreach (Spoken said in _said)
            {
                if (said.rise >= RiseSec) continue;

                said.rise = Mathf.Min(said.rise + deltaTime, RiseSec);
                float k = Mathf.SmoothStep(0f, 1f, said.rise / RiseSec);
                said.group.alpha = k;
                said.rect.anchoredPosition =
                    new Vector2(0f, -said.top + RiseFrom * (1f - k));
            }
        }

        float ScrollTarget =>
            Mathf.Max(0f, _stackHeight - LineGap - _column.rect.height);

        void Scroll(float deltaTime)
        {
            float target = ScrollTarget;
            if (Mathf.Approximately(_scroll, target)) return;

            _scroll = Mathf.Lerp(_scroll, target, 1f - Mathf.Exp(-ScrollResponse * deltaTime));
            _stack.anchoredPosition = new Vector2(0f, _scroll);
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
