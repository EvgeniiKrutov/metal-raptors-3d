using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class HudCurtain : MonoBehaviour
    {
        readonly List<GameObject> _hidden = new List<GameObject>();
        bool _open = true;

        public static HudCurtain Attach(GameObject hud) => hud.AddComponent<HudCurtain>();

        public void Adopt(GameObject child)
        {
            if (_open || child == null) return;

            child.SetActive(false);
            _hidden.Add(child);
        }

        public void Set(bool visible)
        {
            if (visible == _open) return;
            _open = visible;

            if (visible) Show();
            else Hide();
        }

        void Hide()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (!child.activeSelf || child.GetComponent<CinematicBars>() != null) continue;

                child.SetActive(false);
                _hidden.Add(child);
            }
        }

        void Show()
        {
            for (int i = 0; i < _hidden.Count; i++)
                if (_hidden[i] != null) _hidden[i].SetActive(true);

            _hidden.Clear();
        }
    }
}
