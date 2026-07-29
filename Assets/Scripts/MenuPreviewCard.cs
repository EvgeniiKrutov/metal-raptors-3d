using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// A card that shows rather than commands: the same white square as a career era card,
    /// carrying the picked map's name in its foot and holding the space a map screenshot will
    /// fill. Nothing focuses or clicks it. See docs/main-menu.md.
    /// </summary>
    public class MenuPreviewCard
    {
        readonly Text _title;

        public GameObject Root { get; }

        public MenuPreviewCard(Transform parent, string title, Vector2 anchoredPos)
        {
            var go = new GameObject($"Preview Card ({title})", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Root = go;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(MenuTheme.CardSize, MenuTheme.CardSize);
            rt.anchoredPosition = anchoredPos;

            CreateFace(rt);

            _title = UIFactory.CreateBottomLabel(rt, title, MenuTheme.CardTitleSize, MenuTheme.CardPad,
                MenuTheme.CardTitleRowHeight, MenuTheme.CardPad, MenuTheme.Colors.Fg, UIFactory.BoldFont);
        }

        public void SetTitle(string title) => _title.text = title;

        static void CreateFace(RectTransform parent)
        {
            var go = new GameObject("Face", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = MenuTheme.CardFace;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
