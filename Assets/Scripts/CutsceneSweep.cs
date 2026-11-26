using UnityEngine;

namespace MetalRaptors
{
    public static class CutsceneSweep
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            CinematicBars.Appeared -= Run;
            CinematicBars.Appeared += Run;
        }

        static void Run()
        {
            if (!CampaignCheckpoint.InCareer) return;

            PlaneWreck.FadeAll();
            EnemyController.VanishFalling();
            DuelPlane.VanishFalling();
        }
    }
}
