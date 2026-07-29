using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class SearchlightIndicator
    {
        const float Width = 150f;
        const float Height = 30f;

        static readonly Color OnColor = new Color(1f, 0.85f, 0.45f);
        static readonly Color OffColor = new Color(0.55f, 0.55f, 0.62f);

        readonly Text _text;

        public SearchlightIndicator(Transform parent, Vector2 anchoredPos)
        {
            var plate = new GameObject("SearchlightIndicator", typeof(Image));
            plate.transform.SetParent(parent, false);
            var plateImg = plate.GetComponent<Image>();
            plateImg.color = new Color(0f, 0f, 0f, 0.55f);
            plateImg.raycastTarget = false;
            var rt = plateImg.rectTransform;
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = anchoredPos;

            _text = UIFactory.CreateText(plate.transform, "LIGHT  T", 20, Vector2.zero,
                new Vector2(Width, Height), TextAnchor.MiddleCenter, FontStyle.Bold);
            Set(false);
        }

        public void Set(bool on) => _text.color = on ? OnColor : OffColor;
    }
}
