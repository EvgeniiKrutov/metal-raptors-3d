using System;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class CooldownSquare
    {
        readonly Image _wedge;
        readonly Image _outline;
        readonly Image _arc;
        readonly Text _label;
        readonly HudPressRelay _relay;

        public bool Held => _relay != null && _relay.Held;

        public CooldownSquare(Transform parent, Vector2 corner, string label, Action onPress = null,
            bool holdable = false, bool fromRight = false)
        {
            int size = Mathf.RoundToInt(HudTheme.SquareSize);
            float radius = HudTheme.SquareRadius;
            float inset = HudTheme.WedgeInset;

            var go = new GameObject($"CooldownSquare ({label})", typeof(Image));
            go.transform.SetParent(parent, false);

            var track = go.GetComponent<Image>();
            track.sprite = UIFactory.RoundedSprite(radius);
            track.type = Image.Type.Sliced;
            track.color = HudTheme.Track;
            track.raycastTarget = false;

            float side = fromRight ? 1f : 0f;
            var rt = track.rectTransform;
            rt.anchorMin = new Vector2(side, 1f);
            rt.anchorMax = new Vector2(side, 1f);
            rt.pivot = new Vector2(side, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = corner;

            _wedge = Stretch(go.transform, "Wedge");
            _wedge.rectTransform.offsetMin = new Vector2(inset, inset);
            _wedge.rectTransform.offsetMax = new Vector2(-inset, -inset);
            Clock(_wedge, UIFactory.RoundedSprite(Mathf.RoundToInt(size - inset * 2f),
                radius - inset, 0f));
            _wedge.color = HudTheme.Charge;

            _outline = Stretch(go.transform, "Outline");
            _outline.sprite = UIFactory.RoundedSprite(radius, HudTheme.SquareOutline);
            _outline.type = Image.Type.Sliced;

            _arc = Stretch(go.transform, "Arc");
            Clock(_arc, UIFactory.RoundedSprite(size, radius, HudTheme.SquareOutline));
            _arc.color = HudTheme.Idle;

            _label = UIFactory.CreateText(go.transform, label, HudTheme.SquareLabelSize,
                Vector2.zero, new Vector2(size, size), TextAnchor.MiddleCenter, FontStyle.Bold);

            if (onPress != null || holdable) _relay = AddHitArea(go, onPress);

            Set(0f, true);
        }

        public void Set(float charge, bool ready)
        {
            float turn = ready ? 0f : Mathf.Clamp01(charge);
            _wedge.fillAmount = turn;
            _arc.fillAmount = turn;

            _outline.color = ready ? HudTheme.Fill : HudTheme.Track;
            _label.color = ready ? HudTheme.Fill : HudTheme.Idle;
        }

        static void Clock(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
        }

        static HudPressRelay AddHitArea(GameObject square, Action onPress)
        {
            var relay = square.AddComponent<HudPressRelay>();
            if (onPress != null) relay.OnPressed += onPress;

            Image area = Stretch(square.transform, "Hit Box");
            area.color = Color.clear;
            area.raycastTarget = true;

            float pad = HudTheme.SquareHitPad;
            area.rectTransform.offsetMin = new Vector2(-pad, -pad);
            area.rectTransform.offsetMax = new Vector2(pad, pad);
            return relay;
        }

        static Image Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return image;
        }
    }
}
