using System;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class SkillDial
    {
        const float SweepInset = 0.07f;
        const float SweepStroke = 0.1f;
        const float IconShare = 0.52f;
        const float HitAlpha = 0.1f;

        readonly RectTransform _rect;
        readonly Image _rim;
        readonly Image _sweep;
        readonly Image _icon;
        readonly HudPressRelay _relay;

        public bool Held => _relay != null && _relay.Held;

        public GameObject Root => _rect.gameObject;

        public SkillDial(Transform parent, Vector2 anchor, HudIcon icon, float size,
            Action onPress = null, bool holdable = false)
        {
            var go = new GameObject($"Skill Dial ({icon})", typeof(Image));
            go.transform.SetParent(parent, false);

            var face = go.GetComponent<Image>();
            face.sprite = UIFactory.RingSprite(0.5f);
            face.color = HudTheme.Face;
            face.raycastTarget = false;

            _rect = face.rectTransform;
            _rect.anchorMin = anchor;
            _rect.anchorMax = anchor;
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(size, size);

            _rim = Stretch(go.transform, "Rim", 0f);
            _rim.sprite = UIFactory.RingSprite(HudTheme.SkillRim / size);

            _sweep = Stretch(go.transform, "Sweep", size * SweepInset);
            _sweep.sprite = UIFactory.RingSprite(SweepStroke);
            _sweep.type = Image.Type.Filled;
            _sweep.fillMethod = Image.FillMethod.Radial360;
            _sweep.fillOrigin = (int)Image.Origin360.Top;
            _sweep.fillClockwise = true;
            _sweep.color = HudTheme.Idle;

            _icon = Stretch(go.transform, "Icon", size * (1f - IconShare) * 0.5f);
            _icon.sprite = HudIcons.Get(icon);

            if (onPress != null || holdable) _relay = AddHitArea(go, onPress);

            Set(0f, true);
        }

        public void Place(Vector2 centre) => _rect.anchoredPosition = centre;

        public void Set(float charge, bool ready)
        {
            _sweep.fillAmount = ready ? 0f : Mathf.Clamp01(charge);
            _rim.color = ready ? HudTheme.Fill : HudTheme.Track;
            _icon.color = ready ? HudTheme.Fill : HudTheme.Charge;

            float scale = Held ? HudTheme.PressScale : 1f;
            if (!Mathf.Approximately(_rect.localScale.x, scale))
                _rect.localScale = new Vector3(scale, scale, 1f);
        }

        static HudPressRelay AddHitArea(GameObject dial, Action onPress)
        {
            var relay = dial.AddComponent<HudPressRelay>();
            if (onPress != null) relay.OnPressed += onPress;

            Image area = Stretch(dial.transform, "Hit Area", -HudTheme.SkillHitPad);
            area.sprite = UIFactory.RingSprite(0.5f);
            area.color = Color.clear;
            area.raycastTarget = true;
            area.alphaHitTestMinimumThreshold = HitAlpha;
            return relay;
        }

        static Image Stretch(Transform parent, string name, float inset)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return image;
        }
    }
}
