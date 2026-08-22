using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public static class MenuLayout
    {
        public static Transform CreatePage(Transform parent, string name, float widthFraction) =>
            CreateRegion(parent, name, 0f, widthFraction, MenuTheme.PageInsetLeft,
                MenuTheme.PageInsetRight);

        public static Transform CreateRegion(Transform parent, string name, float xMin, float xMax,
            float padLeft) =>
            CreateRegion(parent, name, xMin, xMax, padLeft, MenuTheme.PadRight);

        public static Transform CreateRegion(Transform parent, string name, float xMin, float xMax,
            float padLeft, float padRight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f - MenuTheme.PadTopFraction);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(padLeft, 0f);
            rt.offsetMax = new Vector2(-padRight, 0f);
            return go.transform;
        }

        public static Transform CreateScreen(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        public static Image CreateBand(Transform parent, string name, float xMin, float xMax, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        public static Text BuildTitle(Transform page, string title)
        {
            Text text = UIFactory.CreateLabel(page, title, MenuTheme.TitleSize, 0f,
                MenuTheme.TitleRowHeight, MenuTheme.Colors.Fg, UIFactory.BoldFont);

            UIFactory.CreateRule(page, -(MenuTheme.TitleRowHeight + MenuTheme.TitleToBar),
                new Vector2(MenuTheme.BarWidth, MenuTheme.BarHeight), MenuTheme.Colors.Accent);
            return text;
        }
    }
}
