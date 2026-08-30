using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetalRaptors
{
    public static class CutscenePause
    {
        public const float FreezeSec = 0.55f;
        public const float ThawSec = 0.4f;

        static float _scale = 1f;

        public static float Delta => Menu || ScreenFade.IsBusy ? 0f : Time.unscaledDeltaTime;

        static bool Menu => GameMenu.IsOpen || LevelBriefing.IsOpen || LevelOutro.IsOpen;

        public static void Hold(float scale)
        {
            _scale = Mathf.Clamp01(scale);
            if (!Menu) Time.timeScale = _scale;
        }

        public static void Release()
        {
            _scale = 1f;
            if (!Menu) Time.timeScale = 1f;
        }

        public static void Restore() => Time.timeScale = _scale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() => SceneManager.sceneLoaded += Reset;

        static void Reset(Scene scene, LoadSceneMode mode)
        {
            _scale = 1f;
            Time.timeScale = 1f;
        }
    }
}
