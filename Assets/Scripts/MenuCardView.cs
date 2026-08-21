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

        RectTransform _rt;
        Image _frame;
        Text _title;
        PlaneEmblem _emblem;
        bool _focused;

        public bool Interactable { get; private set; }

        public static MenuCardView Create(Transform parent, string title, string years,
            bool interactable, EraEmblem emblem)
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

            view._emblem = PlaneEmblem.Create(rt, "Emblem");
            RectTransform art = view._emblem.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = new Vector2(MenuTheme.CardPad, MenuTheme.CardArtBottom);
            art.offsetMax = new Vector2(-MenuTheme.CardPad, -MenuTheme.CardPad);
            view._emblem.SetEmblem(emblem);

            view._title = UIFactory.CreateBottomLabel(rt, title, MenuTheme.CardTitleSize,
                MenuTheme.CardPad + MenuTheme.CardYearsRowHeight + MenuTheme.CardTitleToYears,
                MenuTheme.CardTitleRowHeight, MenuTheme.CardPad, MenuTheme.Colors.Fg, UIFactory.BoldFont);

            UIFactory.CreateBottomLabel(rt, years, MenuTheme.CardYearsSize, MenuTheme.CardPad,
                MenuTheme.CardYearsRowHeight, MenuTheme.CardPad, MenuTheme.Colors.Muted, UIFactory.MediumFont);

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
            _emblem.SetTint(mark, Color.Lerp(mark, MenuTheme.CardFace, 0.62f));
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => Activate();
    }
}
