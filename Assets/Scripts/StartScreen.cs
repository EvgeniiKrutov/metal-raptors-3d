using System.Collections;
using UnityEngine;

namespace MetalRaptors
{
    public static class StartScreen
    {
        const string Title = "METAL RAPTORS";
        const float MinSec = 2f;

        static bool _shown;

        public static void Open()
        {
            if (_shown) return;

            _shown = true;
            ScreenFade.BeginTitle(Title, Preload(Time.realtimeSinceStartup + MinSec));
        }

        static IEnumerator Preload(float until)
        {
            foreach (MusicTrackInfo track in MusicTracks.All)
            {
                MusicTracks.Cover(track.Id);
                yield return null;
            }

            while (MusicPlayer.IsBaking(MusicTracks.Selected) || Time.realtimeSinceStartup < until)
                yield return null;
        }
    }
}
