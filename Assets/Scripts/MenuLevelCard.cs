using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuLevelCard : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public event Action Activated;
        public event Action<MenuLevelCard> Hovered;

        RectTransform _rt;
        Image _frame;
        Text _title;
        TerrainSilhouette _art;
        bool _completed;
        bool _focused;

        public bool Interactable { get; private set; }

        public static MenuLevelCard Create(Transform parent, CampaignLevelEntry level,
            bool unlocked, bool completed, CardMetrics metrics)
        {
            var go = new GameObject($"Level Card ({level.Number})",
                typeof(RectTransform), typeof(MenuLevelCard));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<MenuLevelCard>();
            view.Interactable = unlocked;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(metrics.Size, metrics.Size);
            view._rt = rt;

            view._frame = MenuCardView.CreateFrame(rt, metrics.Border);
            MenuCardView.CreateFace(rt);

            view._art = TerrainSilhouette.Create(rt, "Art");
            RectTransform art = view._art.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = new Vector2(0f, metrics.ArtBottom);
            art.offsetMax = new Vector2(0f, -metrics.Pad);
            view._art.SetProfile(level.Terrain, level.Seed);

            view._title = UIFactory.CreateBottomWrapLabel(rt, level.Title, metrics.TitleSize,
                metrics.Pad, metrics.TitleRowHeight, metrics.Pad,
                MenuTheme.Colors.Fg, UIFactory.BoldFont);

            view._completed = unlocked && completed;
            view.Apply();
            return view;
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
            bool done = _completed && !_focused;

            Color mark = done ? MenuTheme.CardDone
                : Interactable ? palette.Accent : palette.Muted;

            _title.color = done ? MenuTheme.CardDoneInk
                : Interactable ? (_focused ? palette.Accent : palette.Fg) : palette.Muted;
            _frame.color = mark;
            _frame.enabled = _focused || _completed;

            _art.SetTint(mark, Color.Lerp(mark, MenuTheme.CardFace, 0.62f));
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => Activate();
    }
}
