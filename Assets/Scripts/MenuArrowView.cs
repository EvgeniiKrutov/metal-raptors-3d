using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// One step control of a <see cref="MenuSelectorRow"/>: a triangle that clicks the value
    /// one place along, and greys out once there is nothing left that way.
    /// See docs/main-menu.md.
    /// </summary>
    public class MenuArrowView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public event Action Clicked;
        public event Action Hovered;

        Image _image;
        bool _interactable = true;
        bool _focused;

        public static MenuArrowView Create(Transform parent, bool pointsLeft, Vector2 anchoredPos)
        {
            Image img = UIFactory.CreateTriangle(parent, pointsLeft, MenuTheme.SelectorArrowSize,
                anchoredPos, MenuTheme.Colors.Fg);

            var view = img.gameObject.AddComponent<MenuArrowView>();
            view._image = img;
            return view;
        }

        public void SetState(bool interactable, bool focused)
        {
            _interactable = interactable;
            _focused = focused;

            MenuPalette palette = MenuTheme.Colors;
            _image.color = !interactable ? palette.Muted : focused ? palette.Accent : palette.Fg;
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_interactable) Clicked?.Invoke();
        }
    }
}
