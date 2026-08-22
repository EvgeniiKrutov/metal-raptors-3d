using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LevelTask : MonoBehaviour
    {
        const float AppearSec = 0.25f;
        const float TickSec = 0.20f;
        const float StrikeSec = 0.30f;
        const float HoldSec = 0.25f;
        const float FadeSec = 0.45f;

        public const float CompleteSeconds = TickSec + StrikeSec + HoldSec + FadeSec;

        const float SlideIn = 18f;
        const float SlideOut = 22f;

        const float RowHeight = 46f;
        const float PadSide = 14f;
        const float BoxSize = 22f;
        const float BoxFrame = 1.5f;
        const float TickSpan = 0.85f;
        const float TickThickness = 2.6f;
        const float LabelGap = 14f;
        const int LabelSize = 26;
        const float StrikeHeight = 4f;
        const float StrikeDrop = 1f;

        static readonly Color Plate = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color Frame = new Color(1f, 1f, 1f, 0.78f);
        static readonly Color BoxFill = new Color(1f, 1f, 1f, 0.07f);
        static readonly Color Label = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color LabelDone = new Color(0.62f, 0.64f, 0.62f);
        static readonly Color Done = new Color(0.40f, 0.88f, 0.38f);

        RectTransform _rt;
        CanvasGroup _group;
        Image _frame;
        RectTransform _tick;
        Text _label;
        RectTransform _strike;

        Vector2 _home;
        float _labelWidth;
        float _appear;
        float _done;
        bool _completing;

        public bool IsCompleting => _completing;

        public static LevelTask Create(Transform parent, Vector2 topLeft, string text)
        {
            var go = new GameObject("Level Task", typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var task = go.AddComponent<LevelTask>();
            task.Build(topLeft, text);
            return task;
        }

        public float Complete()
        {
            if (_completing) return 0f;

            _completing = true;
            _done = 0f;
            return CompleteSeconds;
        }

        void Build(Vector2 topLeft, string text)
        {
            _home = topLeft;
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var plate = GetComponent<Image>();
            plate.color = Plate;
            plate.raycastTarget = false;

            _rt = plate.rectTransform;
            _rt.anchorMin = new Vector2(0f, 1f);
            _rt.anchorMax = new Vector2(0f, 1f);
            _rt.pivot = new Vector2(0f, 1f);

            float labelX = PadSide + BoxSize + LabelGap;
            BuildBox();
            BuildLabel(text, labelX);
            BuildStrike(labelX);

            _rt.sizeDelta = new Vector2(labelX + _labelWidth + PadSide, RowHeight);
            Apply();
        }

        void BuildBox()
        {
            var boxGo = new GameObject("Box", typeof(RectTransform));
            boxGo.transform.SetParent(transform, false);

            var box = (RectTransform)boxGo.transform;
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(BoxSize, BoxSize);
            box.anchoredPosition = new Vector2(PadSide, 0f);

            _frame = Disc(box, Frame, BoxSize, BoxFrame / BoxSize);
            Disc(box, BoxFill, BoxSize - BoxFrame * 2f, 0.5f);

            var holder = new GameObject("Tick", typeof(RectTransform));
            holder.transform.SetParent(box, false);

            _tick = (RectTransform)holder.transform;
            _tick.anchorMin = new Vector2(0.5f, 0.5f);
            _tick.anchorMax = new Vector2(0.5f, 0.5f);
            _tick.pivot = new Vector2(0.5f, 0.5f);
            _tick.sizeDelta = new Vector2(BoxSize, BoxSize);
            _tick.anchoredPosition = Vector2.zero;
            _tick.localScale = Vector3.zero;

            float span = BoxSize * TickSpan;
            Arm(new Vector2(-0.175f, -0.083f) * span, 0.35f * span, -51.8f);
            Arm(new Vector2(0.092f, 0.017f) * span, 0.60f * span, 51.1f);
        }

        void Arm(Vector2 centre, float length, float angle)
        {
            Image bar = Block(_tick, Done, new Vector2(length, TickThickness), centre, centred: true);
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        static Image Disc(Transform parent, Color color, float diameter, float thickness)
        {
            Image image = Block(parent, color, new Vector2(diameter, diameter), Vector2.zero,
                centred: true);
            image.sprite = UIFactory.RingSprite(thickness);
            return image;
        }

        void BuildLabel(string text, float labelX)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(transform, false);

            _label = go.GetComponent<Text>();
            _label.font = UIFactory.BoldFont;
            _label.text = text;
            _label.fontSize = LabelSize;
            _label.fontStyle = FontStyle.Normal;
            _label.alignment = TextAnchor.MiddleLeft;
            _label.color = Label;
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            _labelWidth = _label.preferredWidth;

            var rt = _label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(_labelWidth, RowHeight);
            rt.anchoredPosition = new Vector2(labelX, 0f);
        }

        void BuildStrike(float labelX)
        {
            _strike = Block(transform, Done, new Vector2(0f, StrikeHeight),
                new Vector2(labelX, -StrikeDrop)).rectTransform;
        }

        static Image Block(Transform parent, Color color, Vector2 size, Vector2 position,
            bool centred = false)
        {
            var go = new GameObject("Block", typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(centred ? 0.5f : 0f, 0.5f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(centred ? 0.5f : 0f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return image;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (_appear < AppearSec) _appear = Mathf.Min(_appear + dt, AppearSec);
            if (_completing) _done += dt;

            Apply();

            if (_completing && _done >= CompleteSeconds) Destroy(gameObject);
        }

        void Apply()
        {
            float appear = EaseOut(Mathf.Clamp01(_appear / AppearSec));
            float alpha = appear;
            float dx = Mathf.Lerp(-SlideIn, 0f, appear);

            if (_completing)
            {
                float tick = Mathf.Clamp01(_done / TickSec);
                float strike = Mathf.Clamp01((_done - TickSec) / StrikeSec);
                float fade = Mathf.Clamp01((_done - TickSec - StrikeSec - HoldSec) / FadeSec);

                _tick.localScale = Vector3.one * OutBack(tick);
                _frame.color = Color.Lerp(Frame, Done, tick);

                _strike.sizeDelta = new Vector2(_labelWidth * EaseOut(strike), StrikeHeight);
                _label.color = Color.Lerp(Label, LabelDone, strike);

                alpha *= 1f - fade;
                dx += SlideOut * EaseOut(fade);
            }

            _group.alpha = alpha;
            _rt.anchoredPosition = _home + new Vector2(dx, 0f);
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        static float OutBack(float t)
        {
            const float c1 = 1.9f;
            float u = t - 1f;
            return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
        }
    }
}
