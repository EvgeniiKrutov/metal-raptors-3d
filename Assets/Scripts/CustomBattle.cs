namespace MetalRaptors
{
    /// <summary>One pickable map: the name the menu shows and the land it grows.</summary>
    public class BattleMap
    {
        public readonly string Name;
        public readonly int Seed;

        public BattleMap(string name, int seed)
        {
            Name = name;
            Seed = seed;
        }
    }

    /// <summary>The maps a custom battle can be flown on. Verdun is the only one so far.</summary>
    public static class BattleMaps
    {
        public static readonly BattleMap[] All = { new BattleMap("verdun", 1917) };

        public static string[] Names()
        {
            var names = new string[All.Length];
            for (int i = 0; i < All.Length; i++) names[i] = All[i].Name;
            return names;
        }
    }

    /// <summary>
    /// What the menu's custom battle screen asked for, read by
    /// <see cref="CampaignLevelController"/> when the endless scene loads. Deliberately memory
    /// only — a custom battle's picks last until the game is closed, unlike the settings
    /// <see cref="GameManager"/> persists. See docs/main-menu.md.
    /// </summary>
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

        /// <summary>Called by every other way into the endless scene, so career keeps its own
        /// authored atmosphere after a custom battle has been flown.</summary>
        public static void Clear() => Requested = false;
    }
}
