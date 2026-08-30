using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class DialogueBar
    {
        public const float LeadInSec = 0.55f;

        const float PadSide = 180f;
        const float PadLeft = 40f;
        const float PadTop = 20f;
        const float PadBottom = 18f;
        const float AvatarGap = 22f;
        const float NameRow = 28f;
        const float NameGap = 6f;
        const int NameSize = 24;
        const int TextSize = 28;
        const float TextLineSpacing = 1.15f;
        const float CharsPerSecond = 55f;
        const string HiddenOpen = "<color=#00000000>";
        const string HiddenClose = "</color>";

        const float AvatarSize = 200f;
        const float Box = CinematicBars.BottomHeight - PadTop - PadBottom;
        const float MaxRoom = Box - NameRow - NameGap;

        static readonly char[] Space = { ' ' };
        static readonly char[] SentenceEnd = { '.', '!', '?', '…' };

        static readonly Color Body = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color PlayerAccent = new Color(0.53f, 0.79f, 0.93f);
        static readonly Color NpcAccent = new Color(0.92f, 0.70f, 0.36f);

        readonly CinematicBars _bars;
        readonly GameObject _row;
        readonly Image _avatar;
        readonly Text _name;
        readonly Text _text;

        string _line = string.Empty;
        float _shown;
        float _indent = -1f;
        bool _open;

        public DialogueBar(Transform parent)
        {
            _bars = CinematicBars.Create(parent);

            _row = new GameObject("Radio Line", typeof(RectTransform));
            _row.transform.SetParent(_bars.Bottom, false);

            var rt = (RectTransform)_row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, CinematicBars.BottomHeight);
            rt.anchoredPosition = Vector2.zero;

            _avatar = BuildAvatar();
            _name = BuildText(NameSize, UIFactory.BoldFont, TextAnchor.LowerLeft,
                NameRow, -PadTop, false);
            _text = BuildText(TextSize, UIFactory.MediumFont, TextAnchor.UpperLeft,
                MaxRoom, -(PadTop + NameRow + NameGap), true);

            Indent(0f);
            _row.SetActive(false);
        }

        public bool IsOpen => _open;

        public bool IsReady => _bars.IsIn;

        public bool IsRevealing => _shown < _line.Length;

        public void Open()
        {
            _open = true;
            _bars.Raise();
        }

        public List<string> Split(CampaignSpeaker speaker, string message)
        {
            Dress(speaker);

            _text.text = string.Empty;
            _row.SetActive(true);
            return Wrap(message);
        }

        public void Show(CampaignSpeaker speaker, string message)
        {
            if (speaker == null) return;

            Dress(speaker);
            Stack(message);

            _line = message ?? string.Empty;
            _shown = 0f;
            Paint(0);

            _row.SetActive(true);
        }

        public void Reveal(float deltaTime)
        {
            if (!IsRevealing) return;

            int was = Mathf.FloorToInt(_shown);
            _shown = Mathf.Min(_shown + deltaTime * CharsPerSecond, _line.Length);

            int now = Mathf.FloorToInt(_shown);
            if (now != was) Paint(now);
        }

        public void ClearLine()
        {
            _line = string.Empty;
            _shown = 0f;
            _row.SetActive(false);
        }

        public void Hide()
        {
            ClearLine();
            _open = false;
            CutsceneBlur.Clear();
            CutscenePause.Release();
            _bars.Lower();
        }

        void Dress(CampaignSpeaker speaker)
        {
            if (speaker == null) return;

            _name.text = speaker.Name;
            _name.color = speaker.IsPlayer ? PlayerAccent : NpcAccent;

            Sprite face = CampaignAvatars.For(speaker);
            _avatar.sprite = face;
            _avatar.enabled = face != null;

            Indent(face != null ? AvatarSize + AvatarGap : 0f);
        }

        void Stack(string message)
        {
            float used = Mathf.Min(Height(message ?? string.Empty), MaxRoom);
            float top = -(PadTop + (Box - NameRow - NameGap - used) * 0.5f);

            Lift(_name, top);
            Lift(_text, top - NameRow - NameGap);
        }

        static void Lift(Text text, float top)
        {
            var rt = text.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, top);
        }

        void Indent(float gutter)
        {
            float left = (gutter > 0f ? PadLeft : PadSide) + gutter;
            if (Mathf.Approximately(_indent, left)) return;

            _indent = left;
            Place(_name, left);
            Place(_text, left);
        }

        static void Place(Text text, float left)
        {
            var rt = text.rectTransform;
            rt.sizeDelta = new Vector2(-(left + PadSide), rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2((left - PadSide) * 0.5f, rt.anchoredPosition.y);
        }

        List<string> Wrap(string message)
        {
            var parts = new List<string>();
            string text = (message ?? string.Empty).Trim();
            float room = _text.rectTransform.rect.height;

            if (text.Length == 0 || room <= 0f || Fits(text, room))
            {
                parts.Add(text);
                return parts;
            }

            string held = string.Empty;
            foreach (string sentence in Sentences(text))
            {
                string joined = held.Length == 0 ? sentence : held + " " + sentence;
                if (Fits(joined, room)) { held = joined; continue; }

                if (held.Length > 0) parts.Add(held);
                held = Fits(sentence, room) ? sentence : PackWords(sentence, room, parts);
            }

            if (held.Length > 0) parts.Add(held);
            if (parts.Count == 0) parts.Add(text);
            return parts;
        }

        string PackWords(string sentence, float room, List<string> parts)
        {
            string held = string.Empty;
            foreach (string word in sentence.Split(Space, StringSplitOptions.RemoveEmptyEntries))
            {
                string joined = held.Length == 0 ? word : held + " " + word;
                if (held.Length == 0 || Fits(joined, room)) { held = joined; continue; }

                parts.Add(held);
                held = word;
            }
            return held;
        }

        static IEnumerable<string> Sentences(string text)
        {
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (Array.IndexOf(SentenceEnd, text[i]) < 0) continue;

                int end = i;
                while (end + 1 < text.Length && Array.IndexOf(SentenceEnd, text[end + 1]) >= 0) end++;
                while (end + 1 < text.Length && (text[end + 1] == '"' || text[end + 1] == '\'')) end++;
                if (end + 1 < text.Length && text[end + 1] != ' ') { i = end; continue; }

                string sentence = text.Substring(start, end - start + 1).Trim();
                start = end + 1;
                i = end;
                if (sentence.Length > 0) yield return sentence;
            }

            string tail = start < text.Length ? text.Substring(start).Trim() : string.Empty;
            if (tail.Length > 0) yield return tail;
        }

        bool Fits(string content, float room) => Height(content) <= room;

        float Height(string content)
        {
            TextGenerationSettings settings =
                _text.GetGenerationSettings(new Vector2(_text.GetPixelAdjustedRect().size.x, 0f));

            return _text.cachedTextGeneratorForLayout.GetPreferredHeight(content, settings)
                   / _text.pixelsPerUnit;
        }

        void Paint(int count)
        {
            _text.text = count >= _line.Length
                ? _line
                : _line.Substring(0, count) + HiddenOpen + _line.Substring(count) + HiddenClose;
        }

        Image BuildAvatar()
        {
            var go = new GameObject("Avatar", typeof(Image));
            go.transform.SetParent(_row.transform, false);

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;

            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(AvatarSize, AvatarSize);
            rt.anchoredPosition = new Vector2(PadLeft, PadBottom);
            return image;
        }

        Text BuildText(int fontSize, Font font, TextAnchor alignment, float height, float top, bool wrap)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(_row.transform, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = alignment;
            text.color = Body;
            text.raycastTarget = false;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (wrap) text.lineSpacing = TextLineSpacing;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-2f * PadSide, height);
            rt.anchoredPosition = new Vector2(0f, top);
            return text;
        }
    }
}
