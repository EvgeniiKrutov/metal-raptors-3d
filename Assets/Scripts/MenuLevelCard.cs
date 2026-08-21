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
        Text _number;
        Text _title;
        Text _map;
        Text _status;
        TerrainSilhouette _art;
        bool _completed;
        bool _focused;

        public bool Interactable { get; private set; }

        public static MenuLevelCard Create(Transform parent, CampaignLevelEntry level,
            bool unlocked, bool completed)
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
            rt.sizeDelta = new Vector2(MenuTheme.CardSize, MenuTheme.CardSize);
            view._rt = rt;

            view._frame = MenuCardView.CreateFrame(rt);
            MenuCardView.CreateFace(rt);

            view._art = TerrainSilhouette.Create(rt, "Art");
            RectTransform art = view._art.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = new Vector2(0f, MenuTheme.LevelCardArtBottom);
            art.offsetMax = new Vector2(0f, -MenuTheme.LevelCardArtTop);
            view._art.SetProfile(level.Terrain, level.Seed);

            view._number = UIFactory.CreateTopLabel(rt, $"{level.Number:00}", MenuTheme.LevelNumberSize,
                -MenuTheme.CardPad, MenuTheme.LevelNumberRowHeight, MenuTheme.CardPad,
                MenuTheme.Colors.Border, UIFactory.BoldFont);

            view._title = UIFactory.CreateBottomWrapLabel(rt, level.Title, MenuTheme.CardTitleSize,
                MenuTheme.CardPad + MenuTheme.CardYearsRowHeight + MenuTheme.CardTitleToYears,
                MenuTheme.LevelCardTitleRowHeight, MenuTheme.CardPad, MenuTheme.Colors.Fg,
                UIFactory.BoldFont);

            view._map = UIFactory.CreateBottomLabel(rt, level.MapName, MenuTheme.CardYearsSize,
                MenuTheme.CardPad, MenuTheme.CardYearsRowHeight, MenuTheme.CardPad,
                MenuTheme.Colors.Muted, UIFactory.MediumFont);

            string status = !unlocked ? "locked" : completed ? "completed" : null;
            if (status != null)
            {
                var pos = new Vector2(MenuTheme.CardPad + view._map.preferredWidth + MenuTheme.TagGap,
                    MenuTheme.CardPad);
                view._status = UIFactory.CreateBottomInlineLabel(rt, status.ToUpperInvariant(),
                    MenuTheme.CaptionSize, pos, MenuTheme.CardYearsRowHeight,
                    MenuTheme.Colors.Muted, UIFactory.BoldFont);
            }

            view._completed = completed;
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
            Color mark = Interactable ? palette.Accent : palette.Muted;

            _title.color = Interactable ? (_focused ? palette.Accent : palette.Fg) : palette.Muted;
            _number.color = Interactable && _focused ? palette.Accent : palette.Border;
            _frame.color = mark;
            _frame.enabled = _focused;

            if (_status != null) _status.color = _completed ? palette.Accent : palette.Muted;

            _art.SetTint(mark, Color.Lerp(mark, MenuTheme.CardFace, 0.62f));
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => Activate();
    }
}
