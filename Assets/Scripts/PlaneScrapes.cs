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

        public static void SetGroundCollisions(bool enabled)
        {
            Physics.IgnoreLayerCollision(PlaneFactory.PlaneLayer, ProceduralTerrain.GroundLayer,
                !enabled);
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
                    if (!ModelsOverlap(a, b, reachSq)) continue;

                    a.Scrape();
                    b.Scrape();
                }
        }

        static bool ModelsOverlap(EnemyController a, EnemyController b, float coreReachSq)
        {
            float broad = (a.ModelSize + b.ModelSize) * 0.5f;
            float gapSq = ((Vector2)a.transform.position - (Vector2)b.transform.position).sqrMagnitude;
            if (gapSq > broad * broad) return false;

            Collider ca = a.Hitbox;
            Collider cb = b.Hitbox;
            if (ca == null || cb == null || !ca.enabled || !cb.enabled) return gapSq <= coreReachSq;

            Transform ta = ca.transform;
            Transform tb = cb.transform;
            return Physics.ComputePenetration(ca, ta.position, ta.rotation,
                cb, tb.position, tb.rotation, out _, out _);
        }
    }
}
