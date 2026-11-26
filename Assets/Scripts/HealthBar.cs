using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class HealthBar
    {
        static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);
        static readonly Vector2 OutlineDistance = new Vector2(1.6f, -1.6f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Vector2 ShadowDistance = new Vector2(2.5f, -2.5f);
        const float RowGap = 10f;

        readonly GameObject _root;
        readonly float _width;
        readonly Image _fill;

        public HealthBar(Transform parent, string caption, Color fill, bool outlined = false)
        {
            _width = HudTheme.SlimWidth;
            float height = HudTheme.SlimHeight;
            float row = HudTheme.SlimLabelRow;

            _root = new GameObject($"Health Bar ({caption})", typeof(RectTransform));
            _root.transform.SetParent(parent, false);

            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(_width, row + height);
            rt.anchoredPosition = new Vector2(0f, -HudTheme.ColumnTop);

            Text label = UIFactory.CreateText(_root.transform, caption ?? string.Empty,
                HudTheme.SlimLabelSize, Vector2.zero, new Vector2(_width, row),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            label.color = HudTheme.Fill;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0.5f, 1f);
            labelRt.anchorMax = new Vector2(0.5f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            if (outlined) Darken(label.gameObject);

            Sprite rounded = UIFactory.RoundedSprite(height * 0.5f);

            var trackGo = new GameObject("Track", typeof(Image));
            trackGo.transform.SetParent(_root.transform, false);
            var track = trackGo.GetComponent<Image>();
            track.sprite = rounded;
            track.type = Image.Type.Sliced;
            track.color = HudTheme.Track;
            track.raycastTarget = false;
            var trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(0f, 0f);
            trackRt.anchorMax = new Vector2(1f, 0f);
            trackRt.pivot = new Vector2(0.5f, 0f);
            trackRt.sizeDelta = new Vector2(0f, height);
            trackRt.anchoredPosition = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            _fill = fillGo.GetComponent<Image>();
            _fill.sprite = rounded;
            _fill.type = Image.Type.Sliced;
            _fill.color = fill;
            _fill.raycastTarget = false;
            var fillRt = _fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(_width, 0f);
        }

        public void Set(float current, float max)
        {
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            var size = _fill.rectTransform.sizeDelta;
            size.x = _width * frac;
            _fill.rectTransform.sizeDelta = size;
        }

        public void SetRow(int row)
        {
            var rt = (RectTransform)_root.transform;
            float step = HudTheme.SlimLabelRow + HudTheme.SlimHeight + RowGap;
            rt.anchoredPosition = new Vector2(0f, -HudTheme.ColumnTop - Mathf.Max(0, row) * step);
        }

        public void Remove() => Object.Destroy(_root);

        static void Darken(GameObject target)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = ShadowDistance;

            var outline = target.AddComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = OutlineDistance;
        }
    }
}
