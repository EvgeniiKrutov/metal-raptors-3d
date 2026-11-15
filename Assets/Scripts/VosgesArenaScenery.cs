using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class VosgesArenaScenery
    {
        const float U = PlaneModelConfig.UnitsPerMeter;

        const float SideReach = 1400f;
        const float SpruceZMin = 450f, SpruceZMax = 1290f;
        const int GrovesPerSide = 5;
        const float GroveRadiusXMin = 22f * U, GroveRadiusXMax = 34f * U;
        const float GroveRadiusZMin = 12f * U, GroveRadiusZMax = 20f * U;
        const int GroveTreesMin = 18, GroveTreesMax = 26;
        const int LoneTreesMin = 6, LoneTreesMax = 10;
        const float TreeGap = 4.2f * U;
        const float TreeSizeJitter = 0.12f;
        const float FootProbe = 1f * U;
        const int TreeTries = 12;
        const int LeftSalt = 0x3E71, RightSalt = 0x5B2C;

        static readonly List<GameObject> Sources = new List<GameObject>();

        public static GameObject Build(VosgesArena arena, CampaignTerrain land, bool day, int seed)
        {
            if (arena == null) return null;

            var root = new GameObject("Vosges Arena");
            root.layer = BattlefieldProps.Layer;

            Transform village = PlaceVillage(root.transform, arena);
            if (day) VillagePeople.Begin(root.transform, village, arena.Graph, seed);

            PlantSide(root.transform, arena, land, seed, -1);
            PlantSide(root.transform, arena, land, seed, 1);
            return root;
        }

        static Transform PlaceVillage(Transform parent, VosgesArena arena)
        {
            var site = new GameObject("Village Arena");
            site.layer = BattlefieldProps.Layer;
            site.transform.SetParent(parent, false);
            site.transform.SetPositionAndRotation(arena.Origin, arena.Rotation);
            site.transform.localScale = Vector3.one * arena.Scale;

            var view = Object.Instantiate(arena.Prefab, site.transform, false);
            view.name = arena.Prefab.name;
            NightReceivers.Adopt(view);
            NightLights.MarkCaster(view);

            Collapse(site.transform, "Village");
            return site.transform;
        }

        static void PlantSide(Transform parent, VosgesArena arena, CampaignTerrain land, int seed, int side)
        {
            var rng = new System.Random(seed ^ (side > 0 ? RightSalt : LeftSalt));
            float near = side > 0
                ? arena.Footprint.xMax + VosgesArena.HillMargin
                : arena.Footprint.xMin - VosgesArena.HillMargin;
            float far = near + side * SideReach;
            var trees = new List<VosgesLayout.TreeSpot>();

            for (int g = 0; g < GrovesPerSide; g++)
            {
                float rx = Range(rng, GroveRadiusXMin, GroveRadiusXMax);
                float rz = Range(rng, GroveRadiusZMin, GroveRadiusZMax);
                float x = Mathf.Lerp(near, far, (g + Range(rng, 0.2f, 0.8f)) / GrovesPerSide);
                float z = Range(rng, SpruceZMin + rz, SpruceZMax - rz);
                int count = rng.Next(GroveTreesMin, GroveTreesMax + 1);

                for (int i = 0; i < count; i++)
                    for (int tries = 0; tries < TreeTries; tries++)
                    {
                        float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float reach = Mathf.Sqrt((float)rng.NextDouble());
                        float tx = x + Mathf.Cos(angle) * reach * rx;
                        float tz = z + Mathf.Sin(angle) * reach * rz;
                        if (TryTree(trees, land, rng, tx, tz)) break;
                    }
            }

            int lone = rng.Next(LoneTreesMin, LoneTreesMax + 1);
            for (int i = 0; i < lone; i++)
                for (int tries = 0; tries < TreeTries; tries++)
                {
                    float tx = Mathf.Lerp(near, far, (float)rng.NextDouble());
                    float tz = Range(rng, SpruceZMin, SpruceZMax);
                    if (TryTree(trees, land, rng, tx, tz)) break;
                }

            if (trees.Count == 0) return;

            var site = new GameObject(side > 0 ? "Spruces Right" : "Spruces Left");
            site.layer = BattlefieldProps.Layer;
            site.transform.SetParent(parent, false);
            foreach (var tree in trees) VosgesSpruce.Place(site.transform, tree);
            Collapse(site.transform, "Spruces");
        }

        static bool TryTree(List<VosgesLayout.TreeSpot> trees, CampaignTerrain land, System.Random rng,
            float x, float z)
        {
            foreach (var t in trees)
            {
                float dx = t.position.x - x, dz = t.position.z - z;
                if (dx * dx + dz * dz < TreeGap * TreeGap) return false;
            }

            if (!Foot(land, x, z, out float y)) return false;

            trees.Add(new VosgesLayout.TreeSpot
            {
                position = new Vector3(x, y, z),
                yaw = (float)rng.NextDouble() * 360f,
                model = rng.Next(VosgesSpruce.Models),
                fore = false,
                scale = 1f + Range(rng, -TreeSizeJitter, TreeSizeJitter),
            });
            return true;
        }

        static bool Foot(CampaignTerrain land, float x, float z, out float y)
        {
            y = float.MaxValue;
            if (land == null) return false;

            for (int i = 0; i < 5; i++)
            {
                float px = x + (i == 1 ? FootProbe : i == 2 ? -FootProbe : 0f);
                float pz = z + (i == 3 ? FootProbe : i == 4 ? -FootProbe : 0f);
                if (!land.SampleHeight(px, pz, out float h)) return false;
                y = Mathf.Min(y, h);
            }
            return true;
        }

        static void Collapse(Transform container, string name)
        {
            Sources.Clear();
            for (int i = 0; i < container.childCount; i++) Sources.Add(container.GetChild(i).gameObject);
            if (Sources.Count > 0) MeshMerge.Collapse(container, Sources, name);
            Sources.Clear();
        }

        static float Range(System.Random rng, float min, float max) =>
            Mathf.Lerp(min, max, (float)rng.NextDouble());
    }
}
