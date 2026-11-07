using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuAvatarView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, IMenuFocusable
    {
        public event Action Activated;
        public event Action<IMenuFocusable> Hovered;

        public RectTransform RectTransform => (RectTransform)transform;

        public static MenuAvatarView Create(Transform parent, float size, Sprite picture)
        {
            var go = new GameObject("Avatar", typeof(RectTransform), typeof(Image), typeof(MenuAvatarView));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = Vector2.one * size;

            var image = go.GetComponent<Image>();
            image.sprite = picture;
            image.preserveAspect = true;
            image.color = picture != null ? Color.white : Color.clear;

            return go.GetComponent<MenuAvatarView>();
        }

        public void SetFocused(bool focused) =>
            transform.localScale = Vector3.one * (focused ? MenuTheme.AvatarFocusScale : 1f);

        public void Activate() => Activated?.Invoke();

        public bool Adjust(int delta) => false;

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => SetFocused(false);

        public void OnPointerClick(PointerEventData eventData) => Activate();
    }
}
