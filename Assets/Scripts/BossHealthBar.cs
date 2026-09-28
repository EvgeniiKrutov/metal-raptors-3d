using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class BossHealthBar
    {
        static readonly Color Fill = new Color(0.84f, 0.24f, 0.20f, 1f);
        static readonly Color Label = Color.white;

        readonly float _width;
        readonly Image _fill;

        public BossHealthBar(Transform parent, Vector2 topLeft, string name)
        {
            _width = HudTheme.BarWidth;
            float height = HudTheme.BarHeight;

            var go = new GameObject("Boss Health", typeof(Image));
            go.transform.SetParent(parent, false);

            Sprite rounded = UIFactory.RoundedSprite(HudTheme.BarRadius);

            var track = go.GetComponent<Image>();
            track.sprite = rounded;
            track.type = Image.Type.Sliced;
            track.color = HudTheme.Track;
            track.raycastTarget = false;

            var rt = track.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(_width, height);
            rt.anchoredPosition = topLeft;

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            _fill = fillGo.GetComponent<Image>();
            _fill.sprite = rounded;
            _fill.type = Image.Type.Sliced;
            _fill.color = Fill;
            _fill.raycastTarget = false;

            var fillRt = _fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(_width, 0f);

            Text text = UIFactory.CreateText(go.transform, name ?? string.Empty,
                HudTheme.BarTextSize, Vector2.zero, new Vector2(_width, height),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            text.color = Label;
        }

        public void Set(float current, float max)
        {
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            var size = _fill.rectTransform.sizeDelta;
            size.x = _width * frac;
            _fill.rectTransform.sizeDelta = size;
        }
    }
}
