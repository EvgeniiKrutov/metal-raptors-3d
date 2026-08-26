using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class TouchStick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        RectTransform _rect;
        RectTransform _thumb;
        Vector2 _centre;
        Vector2 _offset;
        Vector2 _shown;
        int _pointer = -1;

        public bool Steering { get; private set; }

        public float Angle { get; private set; }

        public static TouchStick Create(Transform parent)
        {
            var go = new GameObject("Touch Stick", typeof(Image), typeof(TouchStick));
            go.transform.SetParent(parent, false);

            var pad = go.GetComponent<Image>();
            pad.sprite = UIFactory.SolidSprite();
            pad.color = Color.clear;
            pad.raycastTarget = true;

            var rt = pad.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = HudTheme.StickGrabSize;
            rt.anchoredPosition = new Vector2(-MenuTheme.SafeRight, MenuTheme.SafeBottom);

            var stick = go.GetComponent<TouchStick>();
            stick.Build(rt);
            return stick;
        }

        public void SetVisible(bool value)
        {
            if (gameObject.activeSelf != value) gameObject.SetActive(value);
        }

        void Build(RectTransform rect)
        {
            _rect = rect;
            _centre = new Vector2(-HudTheme.StickInsetRight, HudTheme.StickInsetBottom);

            var go = new GameObject("Thumb", typeof(Image));
            go.transform.SetParent(rect, false);

            var img = go.GetComponent<Image>();
            img.sprite = UIFactory.RingSprite(HudTheme.StickThumbStroke / HudTheme.StickThumbSize);
            img.color = HudTheme.Stick;
            img.raycastTarget = false;

            _thumb = img.rectTransform;
            _thumb.anchorMin = new Vector2(1f, 0f);
            _thumb.anchorMax = new Vector2(1f, 0f);
            _thumb.pivot = new Vector2(0.5f, 0.5f);
            _thumb.sizeDelta = new Vector2(HudTheme.StickThumbSize, HudTheme.StickThumbSize);
            _thumb.anchoredPosition = _centre;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
            => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointer != -1) return;
            _pointer = eventData.pointerId;
            Track(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointer) return;
            Track(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointer) return;
            Release();
        }

        void OnDisable()
        {
            Release();
            _shown = Vector2.zero;
            if (_thumb != null) _thumb.anchoredPosition = _centre;
        }

        void Release()
        {
            _pointer = -1;
            _offset = Vector2.zero;
            Steering = false;
        }

        void Track(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            float radius = HudTheme.StickClampRadius;
            Vector2 pull = local - _centre;
            _offset = pull.magnitude > radius ? pull.normalized * radius : pull;

            if (radius > 0f && _offset.magnitude / radius > HudTheme.StickDeadzone)
            {
                Angle = Mathf.Atan2(_offset.y, _offset.x);
                Steering = true;
            }
        }

        void LateUpdate()
        {
            if (_pointer != -1)
            {
                _shown = _offset;
            }
            else if (_shown != Vector2.zero)
            {
                float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / HudTheme.StickReturn);
                _shown = Vector2.Lerp(_shown, Vector2.zero, t);
                if (_shown.sqrMagnitude < 0.25f) _shown = Vector2.zero;
            }

            _thumb.anchoredPosition = _centre + _shown;
        }
    }
}
