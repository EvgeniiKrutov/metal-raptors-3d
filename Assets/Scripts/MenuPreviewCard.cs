using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuPreviewCard
    {
        readonly Text _title;
        readonly Text _status;
        readonly Image _frame;
        readonly Image _art;
        bool _focused;

        public GameObject Root { get; }
        public RectTransform RectTransform { get; }
        public float Size { get; }

        static float ArtBottom =>
            MenuTheme.CardPad + MenuTheme.CardTitleRowHeight + MenuTheme.CardTitleToArt;

        public MenuPreviewCard(Transform parent, string title, Vector2 anchoredPos)
            : this(parent, title, anchoredPos, 0f)
        {
        }

        public MenuPreviewCard(Transform parent, string title, Vector2 anchoredPos, float anchorX)
        {
            var go = new GameObject($"Preview Card ({title})", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Root = go;
            Size = MenuTheme.PreviewCardSize;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(anchorX, 1f);
            rt.anchorMax = new Vector2(anchorX, 1f);
            rt.pivot = new Vector2(anchorX, 1f);
            rt.sizeDelta = new Vector2(Size, Size);
            rt.anchoredPosition = anchoredPos;
            RectTransform = rt;

            _frame = MenuCardView.CreateFrame(rt, MenuTheme.CardBorder);
            CreateFace(rt);
            _art = CreateArt(rt);

            _status = UIFactory.CreateBottomLabel(rt, string.Empty, MenuTheme.StatValueSize, ArtBottom,
                Size - ArtBottom - MenuTheme.CardPad, MenuTheme.CardPad, MenuTheme.Colors.Muted,
                UIFactory.MediumFont);
            _status.alignment = TextAnchor.MiddleCenter;

            _title = UIFactory.CreateBottomLabel(rt, title, MenuTheme.CardTitleSize, MenuTheme.CardPad,
                MenuTheme.CardTitleRowHeight, MenuTheme.CardPad, MenuTheme.Colors.Fg, UIFactory.BoldFont);

            Apply();
        }

        public void SetTitle(string title) => _title.text = title;

        public void SetArt(Sprite sprite)
        {
            _art.sprite = sprite;
            _art.enabled = sprite != null;
        }

        public void SetStatus(string status)
        {
            if (_status.text != status) _status.text = status;
        }

        public void SetFocused(bool focused)
        {
            _focused = focused;
            Apply();
        }

        void Apply()
        {
            MenuPalette palette = MenuTheme.Colors;
            _frame.color = palette.Accent;
            _frame.enabled = _focused;
            _title.color = _focused ? palette.Accent : palette.Fg;
        }

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

        static Image CreateArt(RectTransform parent)
        {
            var go = new GameObject("Art", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(MenuTheme.CardPad, ArtBottom);
            rt.offsetMax = new Vector2(-MenuTheme.CardPad, -MenuTheme.CardPad);
            return img;
        }
    }
}
