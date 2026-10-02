using UnityEngine;

namespace MetalRaptors
{
    public static class CutsceneMix
    {
        public const float Duck = 0.4f;

        const float FadeSec = 0.5f;

        static float _level = 1f;
        static int _frame = -1;

        public static float Level
        {
            get
            {
                if (_frame == Time.frameCount) return _level;

                _frame = Time.frameCount;
                float target = CinematicBars.AnyShowing ? Duck : 1f;
                _level = Mathf.MoveTowards(_level, target, Time.unscaledDeltaTime / FadeSec);
                return _level;
            }
        }
    }
}
