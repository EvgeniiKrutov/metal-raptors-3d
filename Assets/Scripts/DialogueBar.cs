using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class DialogueBar
    {
        public const float LeadInSec = 0.55f;

        const float PadSide = 180f;
        const float PadTop = 20f;
        const float PadBottom = 18f;
        const float NameRow = 28f;
        const float NameGap = 6f;
        const int NameSize = 24;
        const int TextSize = 28;
        const float TextLineSpacing = 1.15f;
        const float CharsPerSecond = 55f;
        const string HiddenOpen = "<color=#00000000>";
        const string HiddenClose = "</color>";

        static readonly Color Body = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color PlayerAccent = new Color(0.53f, 0.79f, 0.93f);
        static readonly Color NpcAccent = new Color(0.92f, 0.70f, 0.36f);

        readonly CinematicBars _bars;
        readonly GameObject _row;
        readonly Text _name;
        readonly Text _text;
        readonly GameObject _hidden;

        string _line = string.Empty;
        float _shown;
        bool _open;

        public DialogueBar(Transform parent, GameObject hiddenWhileTalking = null)
        {
            _hidden = hiddenWhileTalking;
            _bars = CinematicBars.Create(parent);

            _row = new GameObject("Radio Line", typeof(RectTransform));
            _row.transform.SetParent(_bars.Bottom, false);

            var rt = (RectTransform)_row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, CinematicBars.Height);
            rt.anchoredPosition = Vector2.zero;

            _name = BuildText(NameSize, UIFactory.BoldFont, TextAnchor.LowerLeft,
                NameRow, -PadTop, false);
            _text = BuildText(TextSize, UIFactory.MediumFont, TextAnchor.UpperLeft,
                CinematicBars.Height - PadTop - PadBottom - NameRow - NameGap,
                -(PadTop + NameRow + NameGap), true);

            _row.SetActive(false);
        }

        public bool IsOpen => _open;

        public bool IsReady => _bars.IsIn;

        public bool IsRevealing => _shown < _line.Length;

        public void Open()
        {
            _open = true;
            _bars.Raise();
            if (_hidden != null) _hidden.SetActive(false);
        }

        public void Show(CampaignSpeaker speaker, string message)
        {
            if (speaker == null) return;

            _name.text = speaker.Name;
            _name.color = speaker.IsPlayer ? PlayerAccent : NpcAccent;

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
            _bars.Lower();
            if (_hidden != null) _hidden.SetActive(true);
        }

        void Paint(int count)
        {
            _text.text = count >= _line.Length
                ? _line
                : _line.Substring(0, count) + HiddenOpen + _line.Substring(count) + HiddenClose;
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
