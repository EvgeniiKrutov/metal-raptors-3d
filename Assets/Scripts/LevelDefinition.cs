using UnityEngine;

namespace MetalRaptors
{
    public enum Daytime { Morning, Midday, Evening, Night }

    public static class DaytimeNames
    {
        public static readonly string[] All = { "morning", "midday", "evening", "night" };

        public static string For(Daytime daytime) =>
            All[Mathf.Clamp((int)daytime, 0, All.Length - 1)];
    }

    public enum Weather { Calm }

    public enum CloudLevel { Low, Medium, High }

    public class CloudsPart
    {
        public CloudLevel speed = CloudLevel.Medium;
        public CloudLevel frequency = CloudLevel.Medium;
        public CloudLevel size = CloudLevel.Medium;
    }

    public enum TerrainKind
    {
        FlatSlab,
        Verdun,
        Flanders,
        Dolomites,
    }

    public class TerrainPart
    {
        public TerrainKind kind;
        public int seed;
        public float width = 1500f;
    }

    public static class TerrainNames
    {
        public const string Verdun = "verdun";
        public const string Flanders = "flanders";
        public const string FlandersFull = "flanders coast";
        public const string Dolomites = "dolomites";
        public const string FlatSlab = "flat slab";

        public static string For(TerrainKind kind)
        {
            switch (kind)
            {
                case TerrainKind.Verdun: return Verdun;
                case TerrainKind.Flanders: return Flanders;
                case TerrainKind.Dolomites: return Dolomites;
                default: return FlatSlab;
            }
        }
    }

    public class EnemyGroup
    {
        public readonly PlaneModelConfig plane;
        public readonly int count;

        public EnemyGroup(PlaneModelConfig plane, int count)
        {
            this.plane = plane;
            this.count = count;
        }
    }

    public class LevelDefinition
    {
        public TerrainPart terrain;
        public Daytime daytime;
        public Weather weather;
        public CloudsPart clouds;
        public EnemyGroup[] enemies;
        public float flak = 1f;
    }

    public static class Levels
    {
        public static LevelDefinition Level1 => new LevelDefinition
        {
            terrain = new TerrainPart { kind = TerrainKind.Verdun, seed = 1916, width = 2000f },
            daytime = GameManager.Instance != null
                ? GameManager.Instance.Level1Daytime : Daytime.Midday,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            enemies = new[] { new EnemyGroup(PlaneModels.Albatros, 1) },
        };

        public static readonly LevelDefinition Level2 = new LevelDefinition
        {
            terrain = new TerrainPart { kind = TerrainKind.Verdun, seed = 1916, width = 2000f },
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            enemies = new[] { new EnemyGroup(PlaneModels.Albatros, 1) },
        };

        public static LevelDefinition ForNumber(int number)
        {
            switch (number)
            {
                case 1: return Level1;
                case 2: return Level2;
                default:
                    Debug.LogError($"Levels: no definition for level {number}; flying Level 1's.");
                    return Level1;
            }
        }
    }
}
