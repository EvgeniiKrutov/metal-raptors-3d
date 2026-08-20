using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class EnemyWarning : MonoBehaviour
    {
        const float AppearSec = 0.3f;
        const float HoldSec = 1.8f;
        const float FadeSec = 0.5f;

        public const float Seconds = AppearSec + HoldSec + FadeSec;

        const float PulseHz = 2.2f;
        const float PulseFloor = 0.40f;
        const float BorderPulseFloor = 0.65f;

        const int LabelSize = 46;
        const float PadSide = 52f;
        const float RowHeight = 104f;
        const float BorderWidth = 3f;
        const float RowY = 210f;
        const float RiseFrom = 24f;
        const float ScaleFrom = 0.94f;

        static readonly Color Plate = new Color(0.16f, 0.02f, 0.02f, 0.55f);
        static readonly Color Border = new Color(0.93f, 0.17f, 0.13f);
        static readonly Color Label = new Color(1f, 0.32f, 0.26f);

        static readonly string[] Numbers =
            { "NO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT" };

        CanvasGroup _group;
        RectTransform _rt;
        Text _label;
        Image[] _border;
        float _life;

        public static EnemyWarning Show(Transform parent, int planes)
        {
            var go = new GameObject("Enemy Warning", typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var warning = go.AddComponent<EnemyWarning>();
            warning.Build(TextFor(planes));
            return warning;
        }

        public static string TextFor(int planes)
        {
            if (planes <= 1) return "ENEMY PLANE IS INCOMING";

            string count = planes < Numbers.Length ? Numbers[planes] : planes.ToString();
            return $"{count} ENEMY PLANES INCOMING";
        }

        void Build(string text)
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var plate = GetComponent<Image>();
            plate.color = Plate;
            plate.raycastTarget = false;

            _rt = plate.rectTransform;
            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);

            BuildLabel(text);
            _border = new[]
            {
                Edge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, BorderWidth)),
                Edge(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, BorderWidth)),
                Edge(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(BorderWidth, 0f)),
                Edge(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(BorderWidth, 0f)),
            };

            _rt.sizeDelta = new Vector2(_label.preferredWidth + PadSide * 2f, RowHeight);
            Apply();
        }

        void BuildLabel(string text)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(transform, false);

            _label = go.GetComponent<Text>();
            _label.font = UIFactory.BoldFont;
            _label.text = text;
            _label.fontSize = LabelSize;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Label;
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = _label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Image Edge(Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject("Edge", typeof(Image));
            go.transform.SetParent(transform, false);

            var image = go.GetComponent<Image>();
            image.color = Border;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = (anchorMin + anchorMax) * 0.5f;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return image;
        }

        void Update()
        {
            _life += Time.deltaTime;
            Apply();

            if (_life >= Seconds) Destroy(gameObject);
        }

        void Apply()
        {
            float appear = Mathf.Clamp01(_life / AppearSec);
            float fade = Mathf.Clamp01((_life - AppearSec - HoldSec) / FadeSec);
            float ease = 1f - (1f - appear) * (1f - appear);

            _group.alpha = ease * (1f - fade);
            _rt.anchoredPosition = new Vector2(0f, RowY - RiseFrom * (1f - ease));
            _rt.localScale = Vector3.one * Mathf.Lerp(ScaleFrom, 1f, ease);

            float pulse = 0.5f + 0.5f * Mathf.Cos(_life * PulseHz * 2f * Mathf.PI);
            _label.color = Fade(Label, Mathf.Lerp(PulseFloor, 1f, pulse));
            Color border = Fade(Border, Mathf.Lerp(BorderPulseFloor, 1f, pulse));
            foreach (Image edge in _border) edge.color = border;
        }

        static Color Fade(Color color, float alpha) =>
            new Color(color.r, color.g, color.b, alpha);
    }
}
