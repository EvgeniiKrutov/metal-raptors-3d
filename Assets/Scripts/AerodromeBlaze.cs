using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class AerodromeBlaze : MonoBehaviour
    {
        const float SpreadSec = 2.5f;

        const float RoofSpacing = 90f;
        const int RoofFiresMax = 3;
        const float RoofRadiusFactor = 0.8f;
        const float RoofRadiusMin = 20f, RoofRadiusMax = 60f;
        const float RoofLevel = 0.45f;
        const float RoofInset = 0.55f;
        const float RoofJitter = 0.2f;

        const float AirframeRadius = 28f;
        const float AirframeLevel = 0.1f;

        const float SmokeLift = 0.35f;
        const float SmokePerRadius = 0.03f;
        const float SmokeMin = 0.8f, SmokeMax = 1.8f;

        struct Target
        {
            public Vector3[] points;
            public float radius;
            public float delay;
        }

        int _seed;

        public static AerodromeBlaze Begin(List<Bounds> buildings, List<Bounds> aircraft, int seed)
        {
            var blaze = new GameObject("Aerodrome Blaze").AddComponent<AerodromeBlaze>();
            blaze._seed = seed;

            var rng = new System.Random(seed ^ 0x0B1A);
            var targets = new List<Target>();
            foreach (Bounds building in buildings) targets.Add(OnRoof(building, rng));
            foreach (Bounds plane in aircraft) targets.Add(OnAirframe(plane, rng));
            targets.Sort((a, b) => a.delay.CompareTo(b.delay));

            blaze.StartCoroutine(blaze.Spread(targets));
            return blaze;
        }

        static Target OnRoof(Bounds bounds, System.Random rng)
        {
            bool alongX = bounds.size.x >= bounds.size.z;
            float length = alongX ? bounds.size.x : bounds.size.z;
            int count = Mathf.Clamp(Mathf.CeilToInt(length / RoofSpacing), 1, RoofFiresMax);
            float y = bounds.min.y + bounds.size.y * RoofLevel;

            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float along = count == 1 ? 0f : Mathf.Lerp(-RoofInset, RoofInset, i / (count - 1f));
                float across = Range(rng, -RoofJitter, RoofJitter);
                Vector3 offset = alongX
                    ? new Vector3(along * bounds.extents.x, 0f, across * bounds.extents.z)
                    : new Vector3(across * bounds.extents.x, 0f, along * bounds.extents.z);
                points[i] = new Vector3(bounds.center.x, y, bounds.center.z) + offset;
            }

            return new Target
            {
                points = points,
                radius = Mathf.Clamp(bounds.size.y * RoofRadiusFactor, RoofRadiusMin, RoofRadiusMax),
                delay = Range(rng, 0f, SpreadSec),
            };
        }

        static Target OnAirframe(Bounds bounds, System.Random rng) => new Target
        {
            points = new[]
            {
                new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * AirframeLevel,
                    bounds.center.z),
            },
            radius = AirframeRadius,
            delay = Range(rng, 0f, SpreadSec),
        };

        IEnumerator Spread(List<Target> targets)
        {
            float elapsed = 0f;
            foreach (Target target in targets)
            {
                if (target.delay > elapsed)
                {
                    yield return new WaitForSeconds(target.delay - elapsed);
                    elapsed = target.delay;
                }

                Ignite(target);
            }
        }

        void Ignite(Target target)
        {
            Vector3 centre = Vector3.zero;
            foreach (Vector3 point in target.points)
            {
                WreckFire.Begin(transform, point, target.radius, _seed++, smoke: false);
                centre += point;
            }
            centre /= target.points.Length;

            SmokeColumn.Begin(transform, centre + Vector3.up * (target.radius * SmokeLift),
                _seed++, Mathf.Clamp(target.radius * SmokePerRadius, SmokeMin, SmokeMax),
                prewarm: false).Thicken();
        }

        static float Range(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
