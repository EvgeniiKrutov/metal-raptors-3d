using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuLevelRow : MonoBehaviour, IMenuFocusGroup
    {
        public event Action<int> FocusChanged;
        public event Action ViewChanged;

        readonly List<MenuLevelCard> _cards = new List<MenuLevelCard>();
        RectTransform _track;
        int _focus = -1;
        int _offset;
        float _velocity;

        public int Count => _cards.Count;

        static float Pitch => MenuTheme.CardSize + MenuTheme.CardGap;

        static float TrackX(int offset) => MenuTheme.CardBorder - offset * Pitch;

        int MaxOffset => Mathf.Max(0, _cards.Count - MenuTheme.LevelVisibleCards);

        public static MenuLevelRow Create(Transform parent, string name, float top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D), typeof(MenuLevelRow));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<MenuLevelRow>();

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(MenuTheme.LevelRowWidth + 2f * MenuTheme.CardBorder,
                MenuTheme.CardSize + 2f * MenuTheme.CardBorder);
            rt.anchoredPosition = new Vector2(-MenuTheme.CardBorder, top + MenuTheme.CardBorder);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(go.transform, false);

            view._track = (RectTransform)track.transform;
            view._track.anchorMin = new Vector2(0f, 0f);
            view._track.anchorMax = new Vector2(0f, 1f);
            view._track.pivot = new Vector2(0f, 1f);
            view._track.sizeDelta = Vector2.zero;
            view._track.anchoredPosition = new Vector2(TrackX(0), -MenuTheme.CardBorder);
            return view;
        }

        public MenuLevelCard AddCard(CampaignLevelEntry level, bool unlocked, bool completed,
            Action onActivate)
        {
            MenuLevelCard card = MenuLevelCard.Create(_track, level, unlocked, completed);
            if (unlocked && onActivate != null) card.Activated += onActivate;
            card.Hovered += Focus;

            _cards.Add(card);
            return card;
        }

        public void Layout()
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetX(i * Pitch);
        }

        public bool CanSlide(int delta) =>
            delta < 0 ? _offset > 0 : _offset < MaxOffset;

        public void Slide(int delta)
        {
            int wanted = Mathf.Clamp(_offset + delta, 0, MaxOffset);
            if (wanted == _offset) return;

            _offset = wanted;
            if (_focus >= 0) FocusIndex(Mathf.Clamp(_focus, _offset,
                _offset + MenuTheme.LevelVisibleCards - 1), false);

            ViewChanged?.Invoke();
        }

        public void MoveFocus(int delta)
        {
            if (_cards.Count == 0) return;
            FocusIndex(_focus < 0 ? 0 : Mathf.Clamp(_focus + delta, 0, _cards.Count - 1), true);
        }

        public void Adjust(int delta) => MoveFocus(delta);

        public void ActivateFocused()
        {
            if (_focus >= 0 && _focus < _cards.Count) _cards[_focus].Activate();
        }

        public void FocusOn(int index) => FocusIndex(index, true);

        void Focus(MenuLevelCard card)
        {
            int index = _cards.IndexOf(card);
            if (index >= 0) FocusIndex(index, true);
        }

        void FocusIndex(int index, bool follow)
        {
            if (_cards.Count == 0) return;

            _focus = Mathf.Clamp(index, 0, _cards.Count - 1);
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetFocused(i == _focus);

            if (follow) Reveal(_focus);
            FocusChanged?.Invoke(_focus);
        }

        void Reveal(int index)
        {
            int wanted = Mathf.Clamp(_offset, index - MenuTheme.LevelVisibleCards + 1, index);
            wanted = Mathf.Clamp(wanted, 0, MaxOffset);
            if (wanted == _offset) return;

            _offset = wanted;
            ViewChanged?.Invoke();
        }

        void Update()
        {
            float target = TrackX(_offset);
            Vector2 pos = _track.anchoredPosition;
            if (Mathf.Approximately(pos.x, target))
            {
                _velocity = 0f;
                return;
            }

            pos.x = Mathf.SmoothDamp(pos.x, target, ref _velocity, MenuTheme.LevelRowSlide,
                Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(pos.x - target) < 0.5f) pos.x = target;

            _track.anchoredPosition = pos;
        }
    }
}
