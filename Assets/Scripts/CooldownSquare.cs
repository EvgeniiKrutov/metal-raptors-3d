using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class CooldownSquare
    {
        public const float Size = 56f;

        const float Border = 2f;
        const float SweepInset = 3f;
        const int LabelSize = 26;

        public static readonly Color BombTint = new Color(1f, 0.85f, 0.55f);
        public static readonly Color BoostTint = new Color(0.62f, 0.86f, 1f);

        static readonly Color Plate = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color Idle = new Color(0.55f, 0.55f, 0.62f);
        static readonly Color IdleSweep = new Color(0.24f, 0.25f, 0.30f);

        readonly Color _tint;
        readonly Color _readySweep;
        readonly Image _frame;
        readonly Image _sweep;
        readonly Text _text;

        public CooldownSquare(Transform parent, Vector2 anchoredPos, string label, Color tint)
        {
            _tint = tint;
            _readySweep = new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, 1f);

            var go = new GameObject($"CooldownSquare ({label})", typeof(Image));
            go.transform.SetParent(parent, false);

            _frame = go.GetComponent<Image>();
            _frame.raycastTarget = false;

            var rt = _frame.rectTransform;
            rt.sizeDelta = new Vector2(Size, Size);
            rt.anchoredPosition = anchoredPos;

            Image plate = Inset(go.transform, "Plate", Border);
            plate.color = Plate;

            _sweep = Inset(plate.transform, "Sweep", SweepInset);
            _sweep.sprite = UIFactory.SolidSprite();
            _sweep.type = Image.Type.Filled;
            _sweep.fillMethod = Image.FillMethod.Radial360;
            _sweep.fillOrigin = (int)Image.Origin360.Top;
            _sweep.fillClockwise = true;

            _text = UIFactory.CreateText(go.transform, label, LabelSize, Vector2.zero,
                new Vector2(Size, Size), TextAnchor.MiddleCenter, FontStyle.Bold);

            Set(1f, true);
        }

        public void Set(float charge, bool ready)
        {
            _sweep.fillAmount = ready ? 1f : Mathf.Clamp01(charge);
            _sweep.color = ready ? _readySweep : IdleSweep;
            _frame.color = ready ? _tint : Idle;
            _text.color = ready ? _tint : Idle;
        }

        static Image Inset(Transform parent, string name, float inset)
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
