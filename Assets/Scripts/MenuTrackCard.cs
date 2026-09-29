using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuTrackCard : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IMenuOptionRow
    {
        public event Action<IMenuFocusable> Hovered;
        public event Action<IMenuOptionRow> Engaged;

        const string LoadingText = "loading...";

        Action<int> _onChanged;
        Action _onSubmit;
        MusicTrackInfo[] _tracks;
        MenuPreviewCard _card;
        MenuArrowView _left;
        MenuArrowView _right;
        int _index;
        bool _focused;
        bool _live;

        public string TrackId => _tracks.Length > 0 ? _tracks[_index].Id : string.Empty;

        public float Height => _card.Size;

        public static MenuTrackCard Create(Transform parent, MusicTrackInfo[] tracks, int index,
            Action<int> onChanged, Action onSubmit)
        {
            var go = new GameObject("Track Card", typeof(RectTransform), typeof(MenuTrackCard));
            go.transform.SetParent(parent, false);

            float size = MenuTheme.PreviewCardSize;
            float lane = MenuTheme.GarageArrowSize.x + MenuTheme.ArrowToCards;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(size + 2f * lane, size);
            rt.anchoredPosition = Vector2.zero;

            var view = go.GetComponent<MenuTrackCard>();
            view._tracks = tracks ?? new MusicTrackInfo[0];
            view._onChanged = onChanged;
            view._onSubmit = onSubmit;
            view._index = view.ClampIndex(index);

            CreateHitBox(rt);
            view.Build(rt);
            view.Show();
            view.Apply();
            return view;
        }

        void Build(RectTransform rt)
        {
            _card = new MenuPreviewCard(rt, "Track", Vector2.zero, 0.5f);
            _left = CreateArrow(rt, true);
            _right = CreateArrow(rt, false);

            _left.Clicked += () => Step(-1);
            _right.Clicked += () => Step(1);
            _left.Hovered += RaiseHovered;
            _right.Hovered += RaiseHovered;
        }

        static MenuArrowView CreateArrow(RectTransform parent, bool pointsLeft)
        {
            MenuArrowView view = MenuArrowView.Create(parent, pointsLeft, Vector2.zero,
                MenuTheme.GarageArrowSize);

            float anchorX = pointsLeft ? 0f : 1f;
            RectTransform rt = view.RectTransform;
            rt.anchorMin = new Vector2(anchorX, 0.5f);
            rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(anchorX, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return view;
        }

        static void CreateHitBox(RectTransform parent)
        {
            var go = new GameObject("Hit Box", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = Color.clear;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        int ClampIndex(int index) =>
            _tracks.Length > 0 ? Mathf.Clamp(index, 0, _tracks.Length - 1) : 0;

        void Update()
        {
            string id = TrackId;
            _card.SetStatus(id.Length > 0 && !MusicPlayer.IsAudible(id) ? LoadingText : string.Empty);
        }

        public void SetIndex(int index)
        {
            _index = ClampIndex(index);
            Show();
        }

        public void SetLive(bool live)
        {
            _live = live;
            Apply();
        }

        public void SetFocused(bool focused)
        {
            _focused = focused;
            Apply();
        }

        public void Activate()
        {
            Engage();
            _onSubmit?.Invoke();
        }

        public bool Adjust(int delta)
        {
            Step(delta);
            return true;
        }

        void Step(int delta)
        {
            Engage();

            int count = _tracks.Length;
            if (count < 2) return;

            _index = ((_index + delta) % count + count) % count;
            Show();
            _onChanged?.Invoke(_index);
        }

        void Engage() => Engaged?.Invoke(this);

        void RaiseHovered()
        {
            if (_live) Hovered?.Invoke(this);
        }

        void Show()
        {
            _card.SetTitle(_tracks.Length > 0 ? _tracks[_index].Title : string.Empty);
            _card.SetArt(MusicTracks.Cover(TrackId));
        }

        void Apply()
        {
            bool active = _live && _focused;
            bool browsable = _tracks.Length > 1;

            _card.SetFocused(active);
            _left.SetState(browsable, active, !_live);
            _right.SetState(browsable, active, !_live);
        }

        public void OnPointerEnter(PointerEventData eventData) => RaiseHovered();

        public void OnPointerClick(PointerEventData eventData) => Engage();
    }
}
