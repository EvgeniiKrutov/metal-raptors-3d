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
        public float flak = 1f;
        public float enemyHealth;
        public float enemyRotationSpeed;
        public int supplyDrops;
        public float supplyHealthFraction = 0.3f;
        public float supplyHeal = 50f;
        public PlaneModelConfig companionPlane = PlaneModels.Sopwith;
        public PlaneModelConfig companionFoe = PlaneModels.Albatros;
    }

    public static class CampaignLevels
    {
        public const int Count = 8;

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
            flak = 1.0f,
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
            terrain = TerrainKind.Verdun,
            daytime = Daytime.Midday,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level2",
            companion = true,
            flak = 1.1f,
            enemyHealth = 60f,
            enemyRotationSpeed = 88f,
            supplyDrops = 1,
            title = "THE NUMBERS",
            dateline = "22 June 1916 — Verdun sector — high midday",
            lore = Lore2,
        };

        public static CampaignDefinition Level3 => new CampaignDefinition
        {
            seed = 1702,
            terrain = TerrainKind.Verdun,
            daytime = Daytime.Evening,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level3",
            companion = true,
            flak = 0.9f,
            enemyHealth = 65f,
            enemyRotationSpeed = 92f,
            supplyDrops = 1,
            title = "FIXED GROUND",
            dateline = "12 February 1917 — Vaux-le-Bois — failing light",
            lore = Lore1,
        };

        public static CampaignDefinition Level4 => new CampaignDefinition
        {
            seed = 1604,
            terrain = TerrainKind.Flanders,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level4",
            flak = 1.2f,
            enemyHealth = 75f,
            enemyRotationSpeed = 104f,
            supplyDrops = 1,
            companionFoe = PlaneModels.Fokker,
            title = "THE RAVEN",
            dateline = "6 April 1917 — Flanders — hard spring light",
            lore = Lore2,
        };

        public static CampaignDefinition Level5 => new CampaignDefinition
        {
            seed = 1909,
            terrain = TerrainKind.Flanders,
            daytime = Daytime.Night,
            weather = Weather.Calm,
            clouds = new CloudsPart { frequency = CloudLevel.Low },
            script = "level5",
            flak = 1.35f,
            enemyHealth = 70f,
            enemyRotationSpeed = 96f,
            supplyDrops = 1,
            title = "NOTHING BURNS AT NIGHT",
            dateline = "19 September 1917 — Wulpendamme, behind the Flanders coast — night",
            lore = Lore1,
        };

        public static CampaignDefinition Level6 => new CampaignDefinition
        {
            seed = 1310,
            terrain = TerrainKind.Dolomites,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart { size = CloudLevel.High },
            script = "level6",
            companion = true,
            flak = 1.5f,
            enemyHealth = 80f,
            enemyRotationSpeed = 108f,
            supplyDrops = 1,
            title = "HOHRUPT",
            dateline = "3 October 1917 — Hohrupt, upper Fecht valley — low grey morning",
            lore = Lore2,
        };

        public static CampaignDefinition Level7 => new CampaignDefinition
        {
            seed = 1803,
            terrain = TerrainKind.Dolomites,
            daytime = Daytime.Midday,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level7",
            flak = 1.2f,
            enemyHealth = 90f,
            enemyRotationSpeed = 116f,
            supplyDrops = 2,
            title = "TWO FIRES",
            dateline = "24 March 1918 — Rimbach valley — morning into midday",
            lore = Lore1,
        };

        public static CampaignDefinition Level8 => new CampaignDefinition
        {
            seed = 1505,
            terrain = TerrainKind.Dolomites,
            daytime = Daytime.Evening,
            weather = Weather.Calm,
            clouds = new CloudsPart { size = CloudLevel.High },
            script = "level8",
            companion = true,
            flak = 1.4f,
            enemyHealth = 100f,
            enemyRotationSpeed = 124f,
            supplyDrops = 2,
            companionFoe = PlaneModels.Fokker,
            title = "IRON BIRDS OF PREY",
            dateline = "15 May 1918 — over the passes, towards the Belfort gap — last light",
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
                case 3: return Level3;
                case 4: return Level4;
                case 5: return Level5;
                case 6: return Level6;
                case 7: return Level7;
                case 8: return Level8;
                default:
                    Debug.LogError($"CampaignLevels: no definition for level {number}; flying Level 1's.");
                    return Level1;
            }
        }
    }
}
