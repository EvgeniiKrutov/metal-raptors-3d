using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class CinematicBars : MonoBehaviour
    {
        public const float Height = 150f;
        public const float SlideSec = 0.5f;

        const int SortingOrder = 150;

        static readonly Color Ink = new Color(0f, 0f, 0f, 1f);

        static readonly List<CinematicBars> Live = new List<CinematicBars>();

        RectTransform _top;
        RectTransform _bottom;
        float _slide;
        float _target;

        public static CinematicBars Create(Transform parent)
        {
            var go = new GameObject("Cinematic Bars", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            var bars = go.AddComponent<CinematicBars>();
            bars._top = Bar(go.transform, "Top Bar", 1f);
            bars._bottom = Bar(go.transform, "Bottom Bar", 0f);
            bars.Apply();
            return bars;
        }

        public static bool AnyShowing
        {
            get
            {
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    CinematicBars bars = Live[i];
                    if (bars == null) { Live.RemoveAt(i); continue; }
                    if (bars._slide > 0f || bars._target > 0f) return true;
                }
                return false;
            }
        }

        public RectTransform Bottom => _bottom;

        public bool IsIn => _slide >= 1f;

        public void Raise() => _target = 1f;

        public void Lower() => _target = 0f;

        void OnEnable() => Live.Add(this);

        void OnDisable() => Live.Remove(this);

        void Update()
        {
            if (_slide == _target) return;

            _slide = Mathf.MoveTowards(_slide, _target, Time.deltaTime / SlideSec);
            Apply();
        }

        void Apply()
        {
            var size = new Vector2(0f, Height * Mathf.SmoothStep(0f, 1f, _slide));
            _top.sizeDelta = size;
            _bottom.sizeDelta = size;
        }

        static RectTransform Bar(Transform parent, string name, float edge)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = Ink;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, edge);
            rt.anchorMax = new Vector2(1f, edge);
            rt.pivot = new Vector2(0.5f, edge);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }
    }
}
