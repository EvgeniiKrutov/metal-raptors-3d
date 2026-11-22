using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class SpeedDial
    {
        const float Sweep = 0.75f;
        const float SweepDegrees = 270f;
        const float StartTilt = -45f;
        const float StartAngle = -135f;

        const float ArcShare = 0.76f;
        const float ArcStroke = 0.09f;
        const float TrendShare = 0.45f;
        const float NeedleLength = 0.27f;
        const float NeedleWidth = 0.04f;
        const float HubShare = 0.12f;

        const float RedZoneAlpha = 0.55f;
        const float BoostZoneAlpha = 0.45f;

        const float TickGap = 2f;
        const float TickShort = 5f;
        const float TickLong = 9f;
        const float TickWidth = 1.5f;
        const float TickLongWidth = 2f;
        const float TickShortAlpha = 0.6f;

        const float Spring = 200f;
        const float Damping = 10f;
        const float Overswing = 0.04f;
        const float MaxStep = 0.05f;

        const float PulseRate = 4.5f;
        const float PulseTint = 0.5f;
        const float AlarmFaceAlpha = 0.62f;

        const float GlowPad = 8f;
        const float GlowStroke = 8f;
        const float GlowAlpha = 0.35f;
        const float GlowRate = 8f;
        const float GlowVisible = 0.05f;

        const float TrendAhead = 0.8f;
        const float TrendRate = 6f;
        const float TrendMinRate = 0.03f;
        const float TrendGain = 1.2f;
        const float TrendMaxAlpha = 0.6f;

        readonly RectTransform _root;
        readonly Image _face;
        readonly Image _glow;
        readonly Image _rim;
        readonly Image _fill;
        readonly Image _trend;
        readonly Image _needle;
        readonly Image _hub;
        readonly Color _alarmFace;
        readonly float _min;
        readonly float _stall;
        readonly float _top;

        bool _primed;
        float _shown;
        float _velocity;
        float _last;
        float _rate;
        float _glowLevel;

        public SpeedDial(Transform parent, Vector2 centre, float min, float stall, float dive, float top)
        {
            _min = min;
            _stall = stall;
            _top = Mathf.Max(min + 0.01f, top);

            float size = HudTheme.DialSize;
            _alarmFace = Fade(HudTheme.Alarm, AlarmFaceAlpha);

            var go = new GameObject("Speed Dial", typeof(Image));
            go.transform.SetParent(parent, false);

            _face = go.GetComponent<Image>();
            _face.sprite = UIFactory.RingSprite(0.5f);
            _face.color = HudTheme.Face;
            _face.raycastTarget = false;

            _root = _face.rectTransform;
            _root.anchorMin = new Vector2(1f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(size, size);
            _root.anchoredPosition = centre;

            float glowSize = size + GlowPad;
            _glow = Layer("Glow", glowSize, UIFactory.RingSprite(GlowStroke / glowSize));
            _glow.color = Fade(HudTheme.Amber, 0f);

            _rim = Layer("Rim", size, UIFactory.RingSprite(HudTheme.DialRim / size));
            _rim.color = HudTheme.Idle;

            float arcSize = size * ArcShare;
            float stroke = arcSize * ArcStroke;
            Sprite band = UIFactory.RingSprite(ArcStroke);
            Band("Track", arcSize, band, 0f, 1f).color = HudTheme.Track;
            Band("Red Zone", arcSize, band, 0f, Fraction(stall)).color =
                Fade(HudTheme.Alarm, RedZoneAlpha);
            Band("Boost Zone", arcSize, band, Fraction(dive), 1f).color =
                Fade(HudTheme.Amber, BoostZoneAlpha);

            float tickRadius = arcSize * 0.5f + TickGap;
            Tick(tickRadius, 0f, false);
            Tick(tickRadius, Fraction(1f), true);
            Tick(tickRadius, Fraction(dive), false);
            Tick(tickRadius, 1f, false);

            _fill = Band("Fill", arcSize, band, 0f, 0f);

            float mid = arcSize * 0.5f - stroke * 0.5f;
            float thin = stroke * TrendShare;
            float trendSize = 2f * (mid + thin * 0.5f);
            _trend = Band("Trend", trendSize, UIFactory.RingSprite(thin / trendSize), 0f, 0f);

            float width = size * NeedleWidth;
            _needle = Layer("Needle", width, UIFactory.RoundedSprite(width * 0.5f));
            _needle.type = Image.Type.Sliced;
            _needle.rectTransform.pivot = new Vector2(0.5f, 0f);
            _needle.rectTransform.sizeDelta = new Vector2(width, size * NeedleLength);

            _hub = Layer("Hub", size * HubShare, UIFactory.RingSprite(0.5f));
        }

        public void Set(float speed, bool boosting)
        {
            float raw = Time.unscaledDeltaTime;
            float dt = Mathf.Min(raw, MaxStep);

            if (!_primed)
            {
                _primed = true;
                _shown = speed;
                _last = speed;
            }
            else if (raw > 0f)
            {
                _rate += ((speed - _last) / raw - _rate) * (1f - Mathf.Exp(-TrendRate * dt));
            }
            _last = speed;

            _velocity += ((speed - _shown) * Spring - _velocity * Damping) * dt;
            _shown = Mathf.Clamp(_shown + _velocity * dt, Value(-Overswing), Value(1f + Overswing));
            _glowLevel = Mathf.Lerp(_glowLevel, boosting ? 1f : 0f, 1f - Mathf.Exp(-GlowRate * dt));

            bool low = speed < _stall;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseRate) : 0f;
            Color tone = boosting ? HudTheme.Amber : low ? HudTheme.Alarm : HudTheme.Fill;

            float shown = Fraction(_shown);
            float now = Mathf.Clamp01(shown);

            _fill.fillAmount = Sweep * now;
            _fill.color = tone;
            _needle.color = tone;
            _hub.color = tone;
            _needle.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, SweepDegrees * (0.5f - shown));

            float strength = Mathf.Abs(_rate);
            if (strength < TrendMinRate)
            {
                _trend.fillAmount = 0f;
            }
            else
            {
                float ahead = Mathf.Clamp01(Fraction(speed + _rate * TrendAhead));
                _trend.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, StartTilt - SweepDegrees * Mathf.Min(now, ahead));
                _trend.fillAmount = Sweep * Mathf.Abs(ahead - now);
                _trend.color = Fade(tone, Mathf.Min(TrendMaxAlpha, strength * TrendGain));
            }

            _face.color = Color.Lerp(HudTheme.Face, _alarmFace, pulse * PulseTint);
            _rim.color = _glowLevel > GlowVisible
                ? Color.Lerp(HudTheme.Idle, HudTheme.Amber, _glowLevel)
                : Color.Lerp(HudTheme.Idle, HudTheme.Alarm, pulse);
            _glow.color = Fade(HudTheme.Amber, GlowAlpha * _glowLevel);
        }

        float Fraction(float speed) => (speed - _min) / (_top - _min);

        float Value(float fraction) => _min + fraction * (_top - _min);

        void Tick(float radius, float fraction, bool major)
        {
            float angle = StartAngle + SweepDegrees * fraction;
            float rad = angle * Mathf.Deg2Rad;

            Image tick = Layer("Tick", 1f, UIFactory.SolidSprite());
            tick.color = major ? HudTheme.Idle : Fade(HudTheme.Idle, HudTheme.Idle.a * TickShortAlpha);

            var rt = tick.rectTransform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(major ? TickLongWidth : TickWidth, major ? TickLong : TickShort);
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
            rt.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        Image Band(string name, float size, Sprite sprite, float from, float to)
        {
            Image image = Layer(name, size, sprite);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Bottom;
            image.fillClockwise = true;
            image.fillAmount = Sweep * Mathf.Clamp01(to - from);
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, StartTilt - SweepDegrees * from);
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
