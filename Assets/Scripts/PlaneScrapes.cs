using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class PlaneScrapes
    {
        public const float HitboxRadius = 15f;

        static readonly List<EnemyController> Scratch = new List<EnemyController>();

        public static void DisablePlanePlaneCollisions()
        {
            Physics.IgnoreLayerCollision(PlaneFactory.PlaneLayer, PlaneFactory.PlaneLayer, true);
        }

        public static void Check(CubeController player, Transform playerTr,
            IReadOnlyList<EnemyController> enemies)
        {
            if (enemies == null) return;

            float reach = HitboxRadius * 2f;
            float reachSq = reach * reach;

            Scratch.Clear();
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null) Scratch.Add(enemies[i]);

            if (player != null && playerTr != null && player.CurrentHealth > 0f)
            {
                Vector2 playerPos = playerTr.position;
                foreach (var enemy in Scratch)
                {
                    if (enemy == null) continue;
                    if (((Vector2)enemy.transform.position - playerPos).sqrMagnitude > reachSq) continue;

                    player.Scrape();
                    enemy.Scrape();
                }
            }

            for (int i = 0; i < Scratch.Count; i++)
                for (int j = i + 1; j < Scratch.Count; j++)
                {
                    var a = Scratch[i];
                    var b = Scratch[j];
                    if (a == null || b == null) continue;
                    if (((Vector2)a.transform.position - (Vector2)b.transform.position).sqrMagnitude > reachSq)
                        continue;

                    a.Scrape();
                    b.Scrape();
                }
        }
    }
}
