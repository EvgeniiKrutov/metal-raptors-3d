using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// The player's HUD health readout: a dark plate, a fill that shrinks leftward and shades
    /// green to red as damage comes in, and the number on top. Shared by the level controllers.
    /// </summary>
    public class HealthBar
    {
        const float Width = 400f;
        const float Height = 38f;
        const float Padding = 4f;

        readonly Image _fill;
        readonly Text _text;

        public HealthBar(Transform parent, Vector2 anchoredPos)
        {
            var plate = new GameObject("HealthBar", typeof(Image));
            plate.transform.SetParent(parent, false);
            var plateImg = plate.GetComponent<Image>();
            plateImg.color = new Color(0f, 0f, 0f, 0.55f);
            plateImg.raycastTarget = false;
            var rt = plateImg.rectTransform;
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = anchoredPos;

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(plate.transform, false);
            _fill = fillGo.GetComponent<Image>();
            _fill.raycastTarget = false;
            var fillRt = _fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0.5f); // pinned left so the bar drains right-to-left
            fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = new Vector2(Padding, 0f);
            fillRt.sizeDelta = new Vector2(Width - Padding * 2f, Height - Padding * 2f);

            _text = UIFactory.CreateText(plate.transform, "", 24, Vector2.zero,
                new Vector2(Width, Height), TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        public void Set(float current, float max)
        {
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            var size = _fill.rectTransform.sizeDelta;
            size.x = (Width - Padding * 2f) * frac;
            _fill.rectTransform.sizeDelta = size;
            _fill.color = Color.Lerp(
                new Color(0.9f, 0.25f, 0.15f), new Color(0.35f, 0.85f, 0.3f), frac);

            _text.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
