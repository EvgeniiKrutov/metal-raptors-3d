using System.Collections;
using UnityEngine;

namespace MetalRaptors
{
    public static class NightWarmup
    {
        const int Frames = 4;
        const int GunSeed = 7;

        public static IEnumerator Run(Rigidbody player, CampaignDefinition level, float groundY, float worldTop,
            Vector3 cam, float halfWidth, float z, float cruise, float planeLength)
        {
            if (player == null) yield break;

            var enemies = new CampaignEnemies(player, groundY, worldTop, level)
            {
                ModelScale = PlaneModelConfig.LevelScale,
            };
            EnemyController plane = enemies.SpawnAt(PlaneModels.Albatros, new Vector2(cam.x, cam.y));
            PatrolLamp.Mount(plane, cruise, halfWidth, null, 0, 0f);
            AaGun gun = AaGun.Place(cam.x, z, player.transform, cruise, planeLength, GunSeed);

            for (int i = 0; i < Frames; i++) yield return null;

            enemies.Dismiss(plane);
            if (gun != null) Object.Destroy(gun.gameObject);
        }
    }
}
