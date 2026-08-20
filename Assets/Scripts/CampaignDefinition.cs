using UnityEngine;

namespace MetalRaptors
{
    public class CampaignDefinition
    {
        public int seed;
        public TerrainKind terrain = TerrainKind.Verdun;
        public Daytime daytime;
        public Weather weather;
        public CloudsPart clouds;
        public string script;
        public string title;
        public string dateline;
        public string lore;
        public bool companion;
        public float enemyHealth;
        public float enemyRotationSpeed;
        public int supplyDrops;
        public float supplyHealthFraction = 0.3f;
        public float supplyHeal = 50f;
        public PlaneModelConfig companionPlane = PlaneModels.Sopwith;
        public PlaneModelConfig companionFoe = PlaneModels.Fokker;
    }

    public static class CampaignLevels
    {
        const string Lore1 =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor "
            + "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud "
            + "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.\n\n"
            + "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu "
            + "fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in "
            + "culpa qui officia deserunt mollit anim id est laborum.";

        const string Lore2 =
            "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium "
            + "doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore "
            + "veritatis et quasi architecto beatae vitae dicta sunt explicabo.\n\n"
            + "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed "
            + "quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt.";

        public static CampaignDefinition Level1 => new CampaignDefinition
        {
            seed = 1917,
            terrain = TerrainKind.Verdun,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level1",
            companion = true,
            enemyHealth = 50f,
            enemyRotationSpeed = 84f,
            supplyDrops = 1,
            title = "FIRST LIGHT",
            dateline = "14 April 1916 — Verdun sector — dawn",
            lore = Lore1,
        };

        public static CampaignDefinition Level2 => new CampaignDefinition
        {
            seed = 1918,
            terrain = TerrainKind.Flanders,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            title = "THE NUMBERS",
            dateline = "22 June 1916 — Somme sector — morning",
            lore = Lore2,
        };

        public static CampaignDefinition Custom(BattleMap map, Daytime daytime) => new CampaignDefinition
        {
            seed = map.Seed,
            terrain = map.Terrain,
            daytime = daytime,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
        };

        public static CampaignDefinition ForNumber(int number)
        {
            switch (number)
            {
                case 1: return Level1;
                case 2: return Level2;
                default:
                    Debug.LogError($"CampaignLevels: no definition for level {number}; flying Level 1's.");
                    return Level1;
            }
        }
    }
}
