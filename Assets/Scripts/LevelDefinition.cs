using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Which atmosphere a terrain level flies under. Each value maps to one self-contained
    /// static sky class (<see cref="MorningSky"/>, <see cref="MiddaySky"/>); the level's
    /// definition picks a value, and <see cref="LevelController"/> applies the matching sky
    /// while <see cref="ProceduralTerrain"/> builds the fog to match it.
    /// </summary>
    public enum Daytime { Morning, Midday }

    /// <summary>
    /// The weather a level is flown in. Only <see cref="Calm"/> exists so far, and it changes
    /// nothing — but the value is threaded through the whole composition (sky, fog, terrain)
    /// so that when the first real weather arrives (storm, mist, rain...) it plugs into those
    /// seams as data instead of a new code path.
    /// </summary>
    public enum Weather { Calm }

    /// <summary>Which ground a level is built on.</summary>
    public enum TerrainKind
    {
        /// <summary>The flat placeholder slab with the shadow-catching backdrop wall.</summary>
        FlatSlab,

        /// <summary>The procedural Verdun-style land (see <see cref="ProceduralTerrain"/>).</summary>
        Verdun,
    }

    /// <summary>
    /// The ground a level flies over. <see cref="seed"/> only matters for
    /// <see cref="TerrainKind.Verdun"/> (the same seed always builds the same land);
    /// <see cref="width"/> is the playable width in metres for either kind.
    /// </summary>
    public class TerrainPart
    {
        public TerrainKind kind;
        public int seed;
        public float width = 1500f;
    }

    /// <summary>
    /// One group of identical enemy fighters: which aircraft and how many. A level's roster is
    /// a list of these, so mixed formations are one more entry rather than a code change.
    /// Behaviour/stats still come from the shared EnemyConfig asset.
    /// </summary>
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

    /// <summary>
    /// Everything that makes one air-fight level, as composable parts: the terrain to build,
    /// the daytime sky to fly under, the weather (see <see cref="Weather"/> — calm-only for
    /// now), and the enemy roster to spawn. <see cref="LevelController"/> is the composer: it
    /// looks its definition up in <see cref="Levels"/> by scene level number and assembles the
    /// level from these parts at runtime. The daytime/weather atmosphere currently applies to
    /// <see cref="TerrainKind.Verdun"/> levels; the flat slab keeps the default sky.
    /// </summary>
    public class LevelDefinition
    {
        public TerrainPart terrain;
        public Daytime daytime;
        public Weather weather;
        public EnemyGroup[] enemies;
    }

    /// <summary>
    /// Registry of the game's level definitions, in code like <see cref="PlaneModels"/>: a new
    /// level (or a variant of an old one) is a new entry composed from the same parts, not an
    /// edit to the controller. The scene's LevelController carries only its level number and
    /// resolves the rest from here.
    /// </summary>
    public static class Levels
    {
        /// <summary>Level 1: a lone Fokker over the midday Verdun battlefield.</summary>
        public static readonly LevelDefinition Level1 = new LevelDefinition
        {
            terrain = new TerrainPart { kind = TerrainKind.Verdun, seed = 1916, width = 2000f },
            daytime = Daytime.Midday,
            weather = Weather.Calm,
            enemies = new[] { new EnemyGroup(PlaneModels.Fokker, 1) },
        };

        /// <summary>Level 2: a lone Fokker over the flat placeholder slab.</summary>
        public static readonly LevelDefinition Level2 = new LevelDefinition
        {
            terrain = new TerrainPart { kind = TerrainKind.FlatSlab, width = 1500f },
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            enemies = new[] { new EnemyGroup(PlaneModels.Fokker, 1) },
        };

        /// <summary>The definition for scene level <paramref name="number"/>.</summary>
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
