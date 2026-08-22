using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace MetalRaptors
{
    public static class AudioOutput
    {
        static bool _watching;

#if UNITY_IOS && !UNITY_EDITOR
        const bool AllowMixing = true;

        static int _routeChannels = -1;

        [DllImport("__Internal")]
        static extern int MetalRaptorsConfigureAudioSession(int allowMixing);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void EnsureStereo()
        {
            if (!_watching)
            {
                _watching = true;
                AudioSettings.OnAudioConfigurationChanged += OnConfigurationChanged;
            }
            Apply();
        }

        static void OnConfigurationChanged(bool deviceChanged)
        {
            if (deviceChanged) Apply();
        }

        static void Apply()
        {
            bool routeChanged = ConfigureRoute();

            AudioConfiguration config = AudioSettings.GetConfiguration();
            bool wrongMode = config.speakerMode != AudioSpeakerMode.Stereo;
            if (!routeChanged && !wrongMode) return;

            if (wrongMode) Debug.Log($"Audio mixer came up as {config.speakerMode}; resetting to stereo.");

            config.speakerMode = AudioSpeakerMode.Stereo;
            AudioSettings.Reset(config);
        }

        static bool ConfigureRoute()
        {
#if UNITY_IOS && !UNITY_EDITOR
            int channels = MetalRaptorsConfigureAudioSession(AllowMixing ? 1 : 0);
            if (channels < 2) channels = MetalRaptorsConfigureAudioSession(0);

            if (channels == _routeChannels) return false;

            Debug.Log($"iOS audio route: {channels} output channel(s).");
            _routeChannels = channels;
            return true;
#else
            return false;
#endif
        }
    }
}
