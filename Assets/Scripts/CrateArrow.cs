using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class CrateArrow
    {
        const float OrbitGap = 40f;
        const float Size = 34f;
        const float NearShare = 1.15f;

        static readonly Color Green = new Color(0.22f, 0.82f, 0.34f, 0.92f);

        readonly RectTransform _canvas;
        readonly RectTransform _rect;
        readonly GameObject _go;

        static float Orbit => HudTheme.ArrowOrbit + OrbitGap;

        public CrateArrow(Transform parent)
        {
            _canvas = parent as RectTransform;

            _go = new GameObject("Crate Arrow", typeof(RectTransform), typeof(Image));
            _go.transform.SetParent(parent, false);

            var img = _go.GetComponent<Image>();
            img.sprite = HudIcons.Get(HudIcon.Pointer);
            img.color = Green;
            img.raycastTarget = false;

            _rect = img.rectTransform;
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(Size, Size);

            _go.SetActive(false);
        }

        public void Tick(Camera cam, Vector3 plane, Vector3 crate, bool visible)
        {
            if (!visible || cam == null || _canvas == null)
            {
                Hide();
                return;
            }

            Vector3 from = cam.WorldToScreenPoint(plane);
            Vector3 to = cam.WorldToScreenPoint(crate);
            if (from.z <= 0f || to.z <= 0f)
            {
                Hide();
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, from, null, out Vector2 a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, to, null, out Vector2 b);

            Vector2 dir = b - a;
            float distance = dir.magnitude;
            if (distance < Orbit * NearShare)
            {
                Hide();
                return;
            }

            dir /= distance;
            _rect.anchoredPosition = a + dir * Orbit;
            _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            if (!_go.activeSelf) _go.SetActive(true);
        }

        void Hide()
        {
            if (_go.activeSelf) _go.SetActive(false);
        }
    }
}
