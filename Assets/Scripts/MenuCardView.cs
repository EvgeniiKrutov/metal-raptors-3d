using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuCardView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public event Action Activated;
        public event Action<MenuCardView> Hovered;

        const string EraArtFolder = "ui/era_";

        RectTransform _rt;
        Image _frame;
        Text _title;
        PlaneEmblem _emblem;
        Image _art;
        bool _focused;

        public bool Interactable { get; private set; }

        public static MenuCardView Create(Transform parent, string title, bool interactable,
            EraEmblem emblem)
        {
            var go = new GameObject($"Card ({title})", typeof(RectTransform), typeof(MenuCardView));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<MenuCardView>();
            view.Interactable = interactable;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(MenuTheme.CardSize, MenuTheme.CardSize);
            view._rt = rt;

            view._frame = CreateFrame(rt);
            CreateFace(rt);

            view.CreateArt(rt, emblem);

            view._title = UIFactory.CreateBottomLabel(rt, title, MenuTheme.CardTitleSize,
                MenuTheme.CardPad, MenuTheme.CardTitleRowHeight, MenuTheme.CardPad,
                MenuTheme.Colors.Fg, UIFactory.BoldFont);

            view.Apply();
            return view;
        }

        public static Image CreateFrame(RectTransform parent)
        {
            var go = new GameObject("Frame", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-MenuTheme.CardBorder, -MenuTheme.CardBorder);
            rt.offsetMax = new Vector2(MenuTheme.CardBorder, MenuTheme.CardBorder);
            return img;
        }

        void CreateArt(RectTransform parent, EraEmblem emblem)
        {
            var baked = Resources.Load<Sprite>(EraArtFolder + emblem.ToString().ToLowerInvariant());
            RectTransform art;

            if (baked != null)
            {
                var go = new GameObject("Art", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);

                _art = go.GetComponent<Image>();
                _art.sprite = baked;
                _art.preserveAspect = true;
                _art.raycastTarget = false;
                art = _art.rectTransform;
            }
            else
            {
                _emblem = PlaneEmblem.Create(parent, "Emblem");
                _emblem.SetEmblem(emblem);
                art = _emblem.rectTransform;
            }

            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = new Vector2(MenuTheme.CardPad, MenuTheme.CardArtBottom);
            art.offsetMax = new Vector2(-MenuTheme.CardPad, -MenuTheme.CardPad);
        }

        public static void CreateFace(RectTransform parent)
        {
            var go = new GameObject("Face", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = MenuTheme.CardFace;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void SetX(float x) => _rt.anchoredPosition = new Vector2(x, _rt.anchoredPosition.y);

        public void SetFocused(bool focused)
        {
            _focused = focused;
            Apply();
        }

        public void Activate()
        {
            if (Interactable) Activated?.Invoke();
        }

        void Apply()
        {
            MenuPalette palette = MenuTheme.Colors;
            Color mark = Interactable ? palette.Accent : palette.Muted;

            _title.color = Interactable ? (_focused ? palette.Accent : palette.Fg) : palette.Muted;
            _frame.color = mark;
            _frame.enabled = _focused;

            if (_emblem != null) _emblem.SetTint(mark, Color.Lerp(mark, MenuTheme.CardFace, 0.62f));
            if (_art != null) _art.color = Interactable ? Color.white : palette.Muted;
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => Activate();
    }
}
