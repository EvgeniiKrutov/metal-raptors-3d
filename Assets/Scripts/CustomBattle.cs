using UnityEngine;

namespace MetalRaptors
{
    public class BattleMap
    {
        public readonly string Name;
        public readonly int Seed;
        public readonly TerrainKind Terrain;

        public BattleMap(string name, int seed, TerrainKind terrain)
        {
            Name = name;
            Seed = seed;
            Terrain = terrain;
        }
    }

    public static class BattleMaps
    {
        public static readonly BattleMap[] All =
        {
            new BattleMap(TerrainNames.Verdun, 1917, TerrainKind.Verdun),
            new BattleMap(TerrainNames.Flanders, 1918, TerrainKind.Flanders),
            new BattleMap(TerrainNames.Dolomites, 1915, TerrainKind.Dolomites),
        };

        public static string[] Names()
        {
            var names = new string[All.Length];
            for (int i = 0; i < All.Length; i++) names[i] = All[i].Name;
            return names;
        }
    }

    public enum BattleShape { Story, Battle }

    public static class BattleShapeNames
    {
        public static readonly string[] All = { "story", "battle" };

        public static string For(BattleShape shape) =>
            All[Mathf.Clamp((int)shape, 0, All.Length - 1)];
    }

    public static class CustomBattle
    {
        public static bool Requested { get; private set; }
        public static BattleMap Map { get; private set; } = BattleMaps.All[0];
        public static Daytime Daytime { get; private set; } = Daytime.Morning;
        public static BattleShape Shape { get; private set; } = BattleShape.Story;

        public static void Request(BattleMap map, Daytime daytime, BattleShape shape)
        {
            Requested = true;
            Map = map;
            Daytime = daytime;
            Shape = shape;
        }

        public static void Clear() => Requested = false;
    }
}
