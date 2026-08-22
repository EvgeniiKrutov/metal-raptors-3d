using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MetalRaptors
{
    public class HudPressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action OnPressed;

        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            Held = true;
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => Held = false;

        void OnDisable() => Held = false;
    }
}
