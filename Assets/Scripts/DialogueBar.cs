using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class DialogueBar
    {
        const float StripeHeight = 176f;
        const float PadSide = 180f;
        const float PadTop = 22f;
        const float PadBottom = 24f;
        const float EdgeHeight = 2f;
        const float NameRow = 32f;
        const float NameGap = 8f;
        const int NameSize = 24;
        const int TextSize = 30;
        const float TextLineSpacing = 1.15f;

        static readonly Color Stripe = new Color(0.04f, 0.05f, 0.07f, 0.84f);
        static readonly Color Edge = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color Body = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color PlayerAccent = new Color(0.53f, 0.79f, 0.93f);
        static readonly Color NpcAccent = new Color(0.92f, 0.70f, 0.36f);

        readonly GameObject _root;
        readonly Text _name;
        readonly Text _text;
        readonly GameObject _hidden;

        public DialogueBar(Transform parent, GameObject hiddenWhileTalking = null)
        {
            _hidden = hiddenWhileTalking;

            _root = new GameObject("Dialogue Bar", typeof(RectTransform));
            _root.transform.SetParent(parent, false);

            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, StripeHeight);
            rt.anchoredPosition = Vector2.zero;

            UIFactory.CreateBackground(_root.transform, Stripe);
            BuildEdge();

            _name = BuildText(NameSize, UIFactory.BoldFont, TextAnchor.LowerLeft,
                NameRow, -PadTop, false);
            _text = BuildText(TextSize, UIFactory.MediumFont, TextAnchor.UpperLeft,
                StripeHeight - PadTop - PadBottom - NameRow - NameGap,
                -(PadTop + NameRow + NameGap), true);

            _root.SetActive(false);
        }

        public void Show(CampaignSpeaker speaker, string message)
        {
            if (speaker == null) return;

            _name.text = speaker.Name;
            _name.color = speaker.IsPlayer ? PlayerAccent : NpcAccent;
            _text.text = message;

            if (_hidden != null) _hidden.SetActive(false);
            _root.SetActive(true);
        }

        public void Hide()
        {
            _root.SetActive(false);
            if (_hidden != null) _hidden.SetActive(true);
        }

        void BuildEdge()
        {
            var go = new GameObject("Edge", typeof(Image));
            go.transform.SetParent(_root.transform, false);

            var image = go.GetComponent<Image>();
            image.color = Edge;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, EdgeHeight);
            rt.anchoredPosition = Vector2.zero;
        }

        Text BuildText(int fontSize, Font font, TextAnchor alignment, float height, float top, bool wrap)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(_root.transform, false);

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
