using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class HeadingArrow
    {
        const float ArmAngle = 45f;

        readonly RectTransform _canvas;
        readonly RectTransform _rect;
        readonly GameObject _go;

        public HeadingArrow(Transform parent)
        {
            _canvas = parent as RectTransform;

            _go = new GameObject("Heading Arrow", typeof(RectTransform));
            _go.transform.SetParent(parent, false);

            _rect = (RectTransform)_go.transform;
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = Vector2.zero;

            Arm(ArmAngle);
            Arm(-ArmAngle);
        }

        void Arm(float degrees)
        {
            var go = new GameObject("Arm", typeof(Image));
            go.transform.SetParent(_rect, false);

            var img = go.GetComponent<Image>();
            img.sprite = UIFactory.SolidSprite();
            img.color = HudTheme.Idle;
            img.raycastTarget = false;

            float arm = HudTheme.ArrowArm;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(arm, HudTheme.ArrowStroke);
            rt.anchoredPosition = new Vector2(arm * 0.5f * Mathf.Cos(ArmAngle * Mathf.Deg2Rad), 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        public void Tick(Camera cam, Vector3 world, float headingRad, bool visible)
        {
            if (!visible || cam == null || _canvas == null)
            {
                Hide();
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                Hide();
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas, screen, null, out Vector2 local);

            var dir = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
            _rect.anchoredPosition = local + dir * HudTheme.ArrowOrbit;
            _rect.localRotation = Quaternion.Euler(0f, 0f, headingRad * Mathf.Rad2Deg);

            if (!_go.activeSelf) _go.SetActive(true);
        }

        void Hide()
        {
            if (_go.activeSelf) _go.SetActive(false);
        }
    }
}
