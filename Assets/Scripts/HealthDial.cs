using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class HealthDial
    {
        const float Sweep = 0.75f;
        const float SweepDegrees = 270f;
        const float StartTilt = -45f;

        const float ArcShare = 0.76f;
        const float ArcStroke = 0.09f;
        const float NeedleLength = 0.27f;
        const float NeedleWidth = 0.04f;
        const float HubShare = 0.12f;

        const float AmberBelow = 0.5f;
        const float RedBelow = 0.3f;
        const float RedZoneAlpha = 0.55f;

        const float Spring = 200f;
        const float Damping = 10f;
        const float Overswing = 0.04f;
        const float MaxStep = 0.05f;

        const float FlashSec = 0.3f;
        const float FlashPunch = 0.1f;
        const float FlashTint = 0.7f;
        const float PulseRate = 4.5f;
        const float PulseTint = 0.5f;
        const float AlarmFaceAlpha = 0.62f;

        readonly RectTransform _root;
        readonly Image _face;
        readonly Image _rim;
        readonly Image _arc;
        readonly Image _needle;
        readonly Image _hub;
        readonly Color _alarmFace;

        float _target = -1f;
        float _shown;
        float _speed;
        float _flash;

        public HealthDial(Transform parent, Vector2 centre)
        {
            float size = HudTheme.DialSize;
            _alarmFace = Fade(HudTheme.Alarm, AlarmFaceAlpha);

            var go = new GameObject("Health Dial", typeof(Image));
            go.transform.SetParent(parent, false);

            _face = go.GetComponent<Image>();
            _face.sprite = UIFactory.RingSprite(0.5f);
            _face.color = HudTheme.Face;
            _face.raycastTarget = false;

            _root = _face.rectTransform;
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(size, size);
            _root.anchoredPosition = centre;

            _rim = Layer("Rim", size, UIFactory.RingSprite(HudTheme.DialRim / size));
            _rim.color = HudTheme.Idle;

            float arcSize = size * ArcShare;
            Sprite band = UIFactory.RingSprite(ArcStroke);
            Band("Track", arcSize, band, Sweep).color = HudTheme.Track;
            Band("Red Zone", arcSize, band, Sweep * RedBelow).color =
                Fade(HudTheme.Alarm, RedZoneAlpha);
            _arc = Band("Fill", arcSize, band, 0f);

            float width = size * NeedleWidth;
            _needle = Layer("Needle", width, UIFactory.RoundedSprite(width * 0.5f));
            _needle.type = Image.Type.Sliced;
            _needle.rectTransform.pivot = new Vector2(0.5f, 0f);
            _needle.rectTransform.sizeDelta = new Vector2(width, size * NeedleLength);

            _hub = Layer("Hub", size * HubShare, UIFactory.RingSprite(0.5f));
        }

        public void Set(float current, float max)
        {
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (_target < 0f) _shown = frac;
            else if (frac < _target - 0.0001f) _flash = 1f;
            _target = frac;

            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            _speed += ((_target - _shown) * Spring - _speed * Damping) * dt;
            _shown = Mathf.Clamp(_shown + _speed * dt, -Overswing, 1f + Overswing);
            _flash = Mathf.MoveTowards(_flash, 0f, dt / FlashSec);

            bool low = frac < RedBelow;
            Color tone = low ? HudTheme.Alarm : frac <= AmberBelow ? HudTheme.Amber : HudTheme.Fill;
            float pulse = low && frac > 0f
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseRate)
                : 0f;

            _arc.fillAmount = Sweep * Mathf.Clamp01(_shown);
            _arc.color = tone;
            _needle.color = tone;
            _hub.color = tone;
            _needle.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, SweepDegrees * (0.5f - _shown));

            _face.color = Color.Lerp(HudTheme.Face, _alarmFace,
                Mathf.Max(_flash * FlashTint, pulse * PulseTint));
            _rim.color = Color.Lerp(Color.Lerp(HudTheme.Idle, HudTheme.Alarm, pulse),
                HudTheme.Fill, _flash);

            float scale = 1f + FlashPunch * _flash;
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        Image Band(string name, float size, Sprite sprite, float fill)
        {
            Image image = Layer(name, size, sprite);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Bottom;
            image.fillClockwise = true;
            image.fillAmount = fill;
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, StartTilt);
            return image;
        }

        Image Layer(string name, float size, Sprite sprite)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(_root, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            return image;
        }

        static Color Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
    }
}
