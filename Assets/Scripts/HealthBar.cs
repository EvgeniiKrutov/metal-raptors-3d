using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class HealthBar
    {
        readonly float _width;
        readonly Image _fill;
        readonly Text _text;

        public HealthBar(Transform parent, Vector2 topLeft)
        {
            _width = HudTheme.BarWidth;
            float height = HudTheme.BarHeight;

            var go = new GameObject("HealthBar", typeof(Image));
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
            _fill.color = HudTheme.Fill;
            _fill.raycastTarget = false;
            var fillRt = _fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(_width, 0f);

            _text = UIFactory.CreateText(go.transform, "", HudTheme.BarTextSize, Vector2.zero,
                new Vector2(_width, height), TextAnchor.MiddleCenter, FontStyle.Bold);
            _text.color = HudTheme.Ink;
        }

        public void Set(float current, float max)
        {
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            var size = _fill.rectTransform.sizeDelta;
            size.x = _width * frac;
            _fill.rectTransform.sizeDelta = size;

            _text.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
