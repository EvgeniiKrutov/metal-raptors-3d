using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class ScreenFade : MonoBehaviour
    {
        public const float FadeSec = 0.22f;

        const float MaxStep = 0.05f;
        const int SortingOrder = 1000;

        static ScreenFade _rig;
        static bool _busy;

        public static bool IsBusy => _busy;

        Image _sheet;

        public static void Swap(Action change) => Swap(change, FadeSec);

        public static void Swap(Action change, float outSec)
        {
            if (change == null) return;

            if (_busy)
            {
                change();
                return;
            }

            Rig().Begin(change, null, outSec, 0f, FadeSec);
        }

        public static void Load(string scene, Action atBlack = null) =>
            Load(scene, atBlack, FadeSec, 0f, FadeSec);

        public static void Load(string scene, Action atBlack, float outSec, float holdSec,
            float inSec)
        {
            if (string.IsNullOrEmpty(scene)) return;

            if (_busy)
            {
                atBlack?.Invoke();
                SceneManager.LoadScene(scene);
                return;
            }

            Rig().Begin(atBlack, scene, outSec, holdSec, inSec);
        }

        void Begin(Action atBlack, string scene, float outSec, float holdSec, float inSec)
        {
            _busy = true;
            _sheet.raycastTarget = true;
            StartCoroutine(Run(atBlack, scene, Mathf.Max(0.01f, outSec), Mathf.Max(0f, holdSec),
                Mathf.Max(0.01f, inSec)));
        }

        IEnumerator Run(Action atBlack, string scene, float outSec, float holdSec, float inSec)
        {
            yield return Ramp(0f, 1f, outSec);

            atBlack?.Invoke();

            if (scene != null)
            {
                SceneManager.LoadScene(scene);
                yield return null;
            }

            for (float t = 0f; t < holdSec; t += Time.unscaledDeltaTime) yield return null;

            yield return Ramp(1f, 0f, inSec);

            _sheet.raycastTarget = false;
            _busy = false;
        }

        IEnumerator Ramp(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, MaxStep))
            {
                SetAlpha(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }

            SetAlpha(to);
        }

        void SetAlpha(float alpha) => _sheet.color = new Color(0f, 0f, 0f, alpha);

        static ScreenFade Rig()
        {
            if (_rig != null) return _rig;

            Canvas canvas = UIFactory.CreateCanvas("Screen Fade");
            canvas.sortingOrder = SortingOrder;
            DontDestroyOnLoad(canvas.gameObject);

            _rig = canvas.gameObject.AddComponent<ScreenFade>();
            _rig._sheet = UIFactory.CreateBackground(canvas.transform, new Color(0f, 0f, 0f, 0f));
            return _rig;
        }

        void OnDestroy()
        {
            if (_rig != this) return;
            _rig = null;
            _busy = false;
        }
    }
}
