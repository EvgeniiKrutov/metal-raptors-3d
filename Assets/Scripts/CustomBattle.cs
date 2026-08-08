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
        };

        public static string[] Names()
        {
            var names = new string[All.Length];
            for (int i = 0; i < All.Length; i++) names[i] = All[i].Name;
            return names;
        }
    }

    public static class CustomBattle
    {
        public static bool Requested { get; private set; }
        public static BattleMap Map { get; private set; } = BattleMaps.All[0];
        public static Daytime Daytime { get; private set; } = Daytime.Morning;

        public static void Request(BattleMap map, Daytime daytime)
        {
            Requested = true;
            Map = map;
            Daytime = daytime;
        }

        public static void Clear() => Requested = false;
    }
}
