using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class MenuArrowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public event Action Clicked;
        public event Action Hovered;
        public event Action Exited;

        Image _image;
        bool _interactable = true;
        bool _focused;

        public static MenuArrowView Create(Transform parent, bool pointsLeft, Vector2 anchoredPos) =>
            Create(parent, pointsLeft, anchoredPos, MenuTheme.SelectorArrowSize);

        public static MenuArrowView Create(Transform parent, bool pointsLeft, Vector2 anchoredPos,
            Vector2 size)
        {
            Image img = UIFactory.CreateTriangle(parent, pointsLeft, size, anchoredPos,
                MenuTheme.Colors.Fg);

            var view = img.gameObject.AddComponent<MenuArrowView>();
            view._image = img;
            return view;
        }

        public RectTransform RectTransform => _image.rectTransform;

        public void SetState(bool interactable, bool focused)
        {
            _interactable = interactable;
            _focused = focused;

            MenuPalette palette = MenuTheme.Colors;
            _image.color = !interactable ? palette.Muted : focused ? palette.Accent : palette.Fg;
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_interactable) Clicked?.Invoke();
        }
    }
}
