using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class PlaneScrapes
    {
        public const float HitboxRadius = 15f;

        const float DepthBand = EnemyDepthDodge.ClearDepth;

        public static void DisablePlanePlaneCollisions()
        {
            Physics.IgnoreLayerCollision(PlaneFactory.PlaneLayer, PlaneFactory.PlaneLayer, true);
        }

        public static void SetGroundCollisions(bool enabled)
        {
            Physics.IgnoreLayerCollision(PlaneFactory.PlaneLayer, ProceduralTerrain.GroundLayer,
                !enabled);
        }

        public static void Check(CubeController player, Transform playerTr,
            IReadOnlyList<EnemyController> enemies, IReadOnlyList<GroundVehicle> vehicles,
            float radius = HitboxRadius)
        {
            if (player == null || playerTr == null
                || player.CurrentHealth <= 0f || player.Evading) return;

            Vector3 playerPos = playerTr.position;
            CheckPlanes(player, playerPos, enemies, radius);
            CheckVehicles(player, playerPos, vehicles, radius);
        }

        static void CheckPlanes(CubeController player, Vector3 playerPos,
            IReadOnlyList<EnemyController> enemies, float radius)
        {
            if (enemies == null) return;

            float reach = radius * 2f;
            float reachSq = reach * reach;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || enemy.OffPlane) continue;

                Vector3 pos = enemy.transform.position;
                if (Mathf.Abs(pos.z - playerPos.z) > DepthBand) continue;
                if (((Vector2)pos - (Vector2)playerPos).sqrMagnitude > reachSq) continue;

                player.Brush();
            }
        }

        static void CheckVehicles(CubeController player, Vector3 playerPos,
            IReadOnlyList<GroundVehicle> vehicles, float radius)
        {
            if (vehicles == null) return;

            for (int i = 0; i < vehicles.Count; i++)
            {
                GroundVehicle vehicle = vehicles[i];
                if (vehicle == null || !vehicle.Touches(playerPos, radius)) continue;

                player.Scrape();
                vehicle.Scrape();
            }
        }
    }
}
