using System;
using UnityEngine;

namespace MetalRaptors
{
    public static class AudioOptions
    {
        public const int Steps = 20;

        const string PrefMaster = "mr_master_volume";
        const string PrefMusic = "mr_music_volume";
        const string PrefSfx = "mr_sfx_volume";

        public static event Action Changed;

        static bool _loaded;
        static float _master = 1f;
        static float _music = 1f;
        static float _sfx = 1f;

        public static float Master { get { Load(); return _master; } }

        public static float Music { get { Load(); return _music; } }

        public static float Sfx { get { Load(); return _sfx; } }

        public static void SetMaster(float value)
        {
            Load();
            _master = Snap(value);
            AudioListener.volume = _master;
            Store(PrefMaster, _master);
        }

        public static void SetMusic(float value)
        {
            Load();
            _music = Snap(value);
            Store(PrefMusic, _music);
        }

        public static void SetSfx(float value)
        {
            Load();
            _sfx = Snap(value);
            Store(PrefSfx, _sfx);
        }

        public static void Apply()
        {
            Load();
            AudioListener.volume = _master;
        }

        public static int ToStep(float value) => Mathf.RoundToInt(Mathf.Clamp01(value) * Steps);

        public static float FromStep(int step) => Mathf.Clamp(step, 0, Steps) / (float)Steps;

        public static string Percent(float value) =>
            Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";

        static float Snap(float value) => FromStep(ToStep(value));

        static void Store(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _master = Snap(PlayerPrefs.GetFloat(PrefMaster, 1f));
            _music = Snap(PlayerPrefs.GetFloat(PrefMusic, 1f));
            _sfx = Snap(PlayerPrefs.GetFloat(PrefSfx, 1f));
        }
    }
}
