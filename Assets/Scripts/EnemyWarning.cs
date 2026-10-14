using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class EnemyWarning : MonoBehaviour
    {
        const float AppearSec = 0.3f;
        const float HoldSec = 1.8f;
        const float SupplyHoldSec = 0.7f;
        const float FadeSec = 0.5f;

        public const float Seconds = AppearSec + HoldSec + FadeSec;

        public const string ZeppelinText = "ZEPPELIN INCOMING";

        const string SupplyText = "SUPPLY CRATE INCOMING";

        const string SirenPath = "Sounds/enemy_siren";
        const float SirenVolume = 0.099f;
        const float WailSec = Seconds / 2f;
        const float RiseSec = 0.3f;
        const float FallFromSec = 0.95f;
        const float FallSec = 0.3f;

        const float PulseHz = 2.2f;
        const float PulseFloor = 0.40f;
        const float BorderPulseFloor = 0.65f;

        const int LabelSize = 46;
        const float PadSide = 52f;
        const float RowHeight = 104f;
        const float BorderWidth = 3f;
        const float RowY = 210f;
        const float RiseFrom = 24f;
        const float ScaleFrom = 0.94f;

        readonly struct Look
        {
            public readonly Color Plate;
            public readonly Color Border;
            public readonly Color Label;
            public readonly float Hold;
            public readonly bool Alarm;

            public Look(Color plate, Color border, Color label, float hold, bool alarm)
            {
                Plate = plate;
                Border = border;
                Label = label;
                Hold = hold;
                Alarm = alarm;
            }
        }

        static readonly Look AlarmLook = new Look(new Color(0.16f, 0.02f, 0.02f, 0.55f),
            new Color(0.93f, 0.17f, 0.13f), new Color(1f, 0.32f, 0.26f), HoldSec, true);

        static readonly Look SupplyLook = new Look(new Color(0.02f, 0.14f, 0.04f, 0.55f),
            new Color(0.22f, 0.82f, 0.34f), new Color(0.42f, 0.95f, 0.5f), SupplyHoldSec, false);

        static readonly string[] Numbers =
            { "NO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT" };

        static readonly List<EnemyWarning> Alarms = new List<EnemyWarning>();

        static EnemyWarning _supply;

        Look _look;
        CanvasGroup _group;
        RectTransform _rt;
        Text _label;
        Image[] _border;
        AudioSource _siren;
        bool _sirenHeld;
        float _life;

        static AudioClip _sirenClip;

        public static bool Alarming => Alarms.Count > 0;

        public float Duration => AppearSec + _look.Hold + FadeSec;

        public static EnemyWarning Show(Transform parent, string text)
        {
            if (_supply != null) Destroy(_supply.gameObject);

            EnemyWarning warning = Create(parent, "Enemy Warning", text, AlarmLook);
            Alarms.Add(warning);
            return warning;
        }

        public static EnemyWarning ShowSupply(Transform parent)
        {
            _supply = Create(parent, "Supply Warning", SupplyText, SupplyLook);
            return _supply;
        }

        static EnemyWarning Create(Transform parent, string name, string text, Look look)
        {
            var go = new GameObject(name, typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var warning = go.AddComponent<EnemyWarning>();
            warning._look = look;
            warning.Build(text);
            return warning;
        }

        public static string TextFor(int planes)
        {
            if (planes <= 1) return "ENEMY PLANE IS INCOMING";

            string count = planes < Numbers.Length ? Numbers[planes] : planes.ToString();
            return $"{count} ENEMY PLANES INCOMING";
        }

        void Build(string text)
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var plate = GetComponent<Image>();
            plate.color = _look.Plate;
            plate.raycastTarget = false;

            _rt = plate.rectTransform;
            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);

            BuildLabel(text);
            _border = new[]
            {
                Edge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, BorderWidth)),
                Edge(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, BorderWidth)),
                Edge(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(BorderWidth, 0f)),
                Edge(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(BorderWidth, 0f)),
            };

            _rt.sizeDelta = new Vector2(_label.preferredWidth + PadSide * 2f, RowHeight);
            if (_look.Alarm) BuildSiren();
            Apply();
        }

        void BuildSiren()
        {
            if (_sirenClip == null) _sirenClip = Resources.Load<AudioClip>(SirenPath);
            if (_sirenClip == null) return;

            _siren = gameObject.AddComponent<AudioSource>();
            _siren.playOnAwake = false;
            _siren.loop = false;
            _siren.spatialBlend = 0f;
            _siren.clip = _sirenClip;
        }

        void Start()
        {
            if (!_look.Alarm) SupplyCrate.PlayChime(transform.position);
            if (_siren == null) return;

            _siren.volume = SirenVolume * AudioOptions.Scene;
            _siren.Play();
        }

        void BuildLabel(string text)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(transform, false);

            _label = go.GetComponent<Text>();
            _label.font = UIFactory.BoldFont;
            _label.text = text;
            _label.fontSize = LabelSize;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = _look.Label;
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = _label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Image Edge(Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject("Edge", typeof(Image));
            go.transform.SetParent(transform, false);

            var image = go.GetComponent<Image>();
            image.color = _look.Border;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = (anchorMin + anchorMax) * 0.5f;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return image;
        }

        void Update()
        {
            HoldSiren(Time.timeScale <= 0f);
            _life += Time.deltaTime;
            Apply();

            if (_life >= Duration) Destroy(gameObject);
        }

        void OnDestroy() => Alarms.Remove(this);

        void HoldSiren(bool hold)
        {
            if (_siren == null || hold == _sirenHeld) return;

            _sirenHeld = hold;
            if (hold) _siren.Pause();
            else _siren.UnPause();
        }

        void Apply()
        {
            float appear = Mathf.Clamp01(_life / AppearSec);
            float fade = Mathf.Clamp01((_life - AppearSec - _look.Hold) / FadeSec);
            float ease = 1f - (1f - appear) * (1f - appear);

            _group.alpha = ease * (1f - fade);
            _rt.anchoredPosition = new Vector2(0f, RowY - RiseFrom * (1f - ease));
            _rt.localScale = Vector3.one * Mathf.Lerp(ScaleFrom, 1f, ease);

            float pulse = _look.Alarm
                ? Wail(_life)
                : 0.5f + 0.5f * Mathf.Cos(_life * PulseHz * 2f * Mathf.PI);
            _label.color = Fade(_look.Label, Mathf.Lerp(PulseFloor, 1f, pulse));
            Color border = Fade(_look.Border, Mathf.Lerp(BorderPulseFloor, 1f, pulse));
            foreach (Image edge in _border) edge.color = border;
        }

        static float Wail(float t)
        {
            float u = Mathf.Repeat(Mathf.Max(0f, t), WailSec);
            if (u < RiseSec) return Mathf.SmoothStep(0f, 1f, u / RiseSec);
            return 1f - Mathf.SmoothStep(0f, 1f, (u - FallFromSec) / FallSec);
        }

        static Color Fade(Color color, float alpha) =>
            new Color(color.r, color.g, color.b, alpha);
    }
}
