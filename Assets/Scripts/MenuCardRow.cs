using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class MenuCardRow : IMenuFocusGroup
    {
        public event Action<int> FocusChanged;

        readonly List<MenuCardView> _cards = new List<MenuCardView>();
        readonly RectTransform _root;
        readonly CardMetrics _metrics;
        int _focus = -1;

        public MenuCardRow(Transform parent, string name, float top, int count)
        {
            _metrics = new CardMetrics(count, 1, top);

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            _root = (RectTransform)go.transform;
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, top);
            _root.sizeDelta = Vector2.zero;
        }

        public MenuCardView AddCard(string title, bool interactable, EraEmblem emblem,
            Action onActivate)
        {
            MenuCardView card = MenuCardView.Create(_root, title, interactable, emblem, _metrics);
            if (interactable && onActivate != null) card.Activated += onActivate;
            card.Hovered += Focus;

            _cards.Add(card);
            return card;
        }

        public void Layout()
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetX(i * _metrics.Pitch);
        }

        public void MoveFocus(int delta)
        {
            if (_cards.Count == 0) return;
            FocusIndex(_focus < 0 ? 0 : (_focus + delta + _cards.Count) % _cards.Count);
        }

        public void Adjust(int delta) => MoveFocus(delta);

        public void ActivateFocused()
        {
            if (_focus >= 0 && _focus < _cards.Count) _cards[_focus].Activate();
        }

        public void FocusFirst() => FocusIndex(0);

        void Focus(MenuCardView card)
        {
            int index = _cards.IndexOf(card);
            if (index >= 0) FocusIndex(index);
        }

        void FocusIndex(int index)
        {
            if (_cards.Count == 0) return;

            _focus = Mathf.Clamp(index, 0, _cards.Count - 1);
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetFocused(i == _focus);
            FocusChanged?.Invoke(_focus);
        }
    }
}
