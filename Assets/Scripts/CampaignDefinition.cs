using UnityEngine;

namespace MetalRaptors
{
    public readonly struct CampaignOutroLine
    {
        public readonly string Speaker;
        public readonly string Line;

        public CampaignOutroLine(string speaker, string line)
        {
            Speaker = speaker;
            Line = line;
        }
    }

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
        public CampaignOutroLine[] outro;
        public string journal;
        public bool companion;
        public bool zeppelins;
        public float flak = 1f;
        public float enemyHealthScale = 1f;
        public float enemyRotationScale = 1f;
        public int supplyDrops;
        public float supplyHealthFraction = 0.3f;
        public float supplyHeal = 50f;
        public PlaneModelConfig companionPlane = PlaneModels.Sopwith;
        public PlaneModelConfig companionFoe = PlaneModels.Albatros;
    }

    public static class CampaignLevels
    {
        public const int Count = 9;

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

        const string Before1 =
            "They turned me down twice. Flat feet the first time. The second time a doctor in "
            + "Amiens listened to my chest for a long minute and wrote something down that I was "
            + "not allowed to read.\n\n"
            + "So I went to the aerodrome at Vaux-le-Bois as a mechanic, because a mechanic is "
            + "allowed to stand next to aeroplanes. I stood next to them for eleven months. In "
            + "March a capitaine named Roussel found me sitting in a cold Sopwith at two in the "
            + "morning, working the controls against nothing. He did not report me. He signed a "
            + "form instead.\n\n"
            + "This morning there is a machine standing on the field with my name written against "
            + "it, and at first light I am taking it up the line to look over the sector. Eleven "
            + "months of warming other men's engines and holding their wingtips out of the mud. I "
            + "did not sleep, and I do not care, because I have wanted this since I was fourteen "
            + "years old without ever once being able to say why.\n\n"
            + "— É. Vasseur";

        const string Journal1 =
            "I ate nothing. I sat down on the grass beside the machine and did not get up for a "
            + "long time.\n\n"
            + "There were forty-one holes in it. I know because I counted them, and then counted "
            + "them again, because the first number felt as though it belonged to somebody else's "
            + "aeroplane. Two through the tailplane. A line of them along the underside of the "
            + "port wing where the fabric had gone soft and grey. One through the seat back, six "
            + "inches above my right shoulder. I had not heard a single one of them arrive.\n\n"
            + "The fitters worked around me all evening. They had known me for eleven months as "
            + "the boy who warmed their engines and swept their shed, and not one of them said a "
            + "word about any of it, which I understood then and understand now to be the highest "
            + "thing they had to give. At some point it was dark. At some point after that a "
            + "sergeant put his hand on my shoulder and told me to go away.\n\n"
            + "I have flown a great many first mornings since, other people's as well as my own. "
            + "I have never got that one back.";

        const string JournalLorem =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor "
            + "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud "
            + "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.\n\n"
            + "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu "
            + "fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in "
            + "culpa qui officia deserunt mollit anim id est laborum.";

        static CampaignOutroLine[] Outro1 => new[]
        {
            new CampaignOutroLine("roussel", "l1_after1"),
            new CampaignOutroLine("you", "l1_after2"),
            new CampaignOutroLine("roussel", "l1_after3"),
            new CampaignOutroLine("marchand", "l1_after4"),
            new CampaignOutroLine("roussel", "l1_after5"),
            new CampaignOutroLine("marchand", "l1_after6"),
            new CampaignOutroLine("roussel", "l1_after7"),
        };

        static CampaignOutroLine[] OutroLorem(int level) => new[]
        {
            new CampaignOutroLine("hq", $"l{level}_after1"),
            new CampaignOutroLine("you", $"l{level}_after2"),
            new CampaignOutroLine("wing", $"l{level}_after3"),
            new CampaignOutroLine("you", $"l{level}_after4"),
            new CampaignOutroLine("hq", $"l{level}_after5"),
        };

        public static CampaignDefinition Level1 => new CampaignDefinition
        {
            seed = 1917,
            terrain = TerrainKind.Verdun,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level1",
            companion = true,
            zeppelins = true,
            flak = 1.0f,
            enemyHealthScale = 0.50f,
            enemyRotationScale = 0.80f,
            supplyDrops = 1,
            companionFoe = PlaneModels.Fokker,
            title = "WARMING ENGINES",
            dateline = "14 April 1916",
            lore = Before1,
            outro = Outro1,
            journal = Journal1,
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
            zeppelins = true,
            flak = 1.1f,
            enemyHealthScale = 0.60f,
            enemyRotationScale = 0.84f,
            supplyDrops = 1,
            title = "THE NUMBERS",
            dateline = "22 June 1916 — Verdun sector — high midday",
            lore = Lore2,
            outro = OutroLorem(2),
            journal = JournalLorem,
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
            zeppelins = true,
            flak = 0.9f,
            enemyHealthScale = 0.65f,
            enemyRotationScale = 0.88f,
            supplyDrops = 1,
            title = "FIXED GROUND",
            dateline = "12 February 1917 — Vaux-le-Bois — failing light",
            lore = Lore1,
            outro = OutroLorem(3),
            journal = JournalLorem,
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
            enemyHealthScale = 0.75f,
            enemyRotationScale = 1.00f,
            supplyDrops = 1,
            companionFoe = PlaneModels.Fokker,
            title = "THE RAVEN",
            dateline = "6 April 1917 — Flanders — hard spring light",
            lore = Lore2,
            outro = OutroLorem(4),
            journal = JournalLorem,
        };

        public static CampaignDefinition Level5 => new CampaignDefinition
        {
            seed = 1707,
            terrain = TerrainKind.Flanders,
            daytime = Daytime.Midday,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level5",
            flak = 1.25f,
            enemyHealthScale = 0.78f,
            enemyRotationScale = 1.02f,
            supplyDrops = 1,
            title = "FACING BACKWARDS",
            dateline = "21 June 1917 — the Flanders coast — midday",
            lore = Lore1,
            outro = OutroLorem(5),
            journal = JournalLorem,
        };

        public static CampaignDefinition Level6 => new CampaignDefinition
        {
            seed = 1909,
            terrain = TerrainKind.Flanders,
            daytime = Daytime.Night,
            weather = Weather.Calm,
            clouds = new CloudsPart { frequency = CloudLevel.Low },
            script = "level6",
            flak = 1.35f,
            enemyHealthScale = 0.70f,
            enemyRotationScale = 0.91f,
            supplyDrops = 1,
            title = "NOTHING BURNS AT NIGHT",
            dateline = "19 September 1917 — Wulpendamme, behind the Flanders coast — night",
            lore = Lore1,
            outro = OutroLorem(6),
            journal = JournalLorem,
        };

        public static CampaignDefinition Level7 => new CampaignDefinition
        {
            seed = 1310,
            terrain = TerrainKind.Dolomites,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart { size = CloudLevel.High },
            script = "level7",
            companion = true,
            flak = 1.5f,
            enemyHealthScale = 0.80f,
            enemyRotationScale = 1.03f,
            supplyDrops = 1,
            title = "HOHRUPT",
            dateline = "3 October 1917 — Hohrupt, upper Fecht valley — low grey morning",
            lore = Lore2,
            outro = OutroLorem(7),
            journal = JournalLorem,
        };

        public static CampaignDefinition Level8 => new CampaignDefinition
        {
            seed = 1803,
            terrain = TerrainKind.Dolomites,
            daytime = Daytime.Midday,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            script = "level8",
            flak = 1.2f,
            enemyHealthScale = 0.90f,
            enemyRotationScale = 1.10f,
            supplyDrops = 2,
            title = "TWO FIRES",
            dateline = "24 March 1918 — Rimbach valley — morning into midday",
            lore = Lore1,
            outro = OutroLorem(8),
            journal = JournalLorem,
        };

        public static CampaignDefinition Level9 => new CampaignDefinition
        {
            seed = 1505,
            terrain = TerrainKind.Dolomites,
            daytime = Daytime.Evening,
            weather = Weather.Calm,
            clouds = new CloudsPart { size = CloudLevel.High },
            script = "level9",
            companion = true,
            flak = 1.4f,
            enemyHealthScale = 1.00f,
            enemyRotationScale = 1.18f,
            supplyDrops = 2,
            companionFoe = PlaneModels.Fokker,
            title = "IRON BIRDS OF PREY",
            dateline = "15 May 1918 — over the passes, towards the Belfort gap — last light",
            lore = Lore2,
            outro = OutroLorem(9),
            journal = JournalLorem,
        };

        public static CampaignDefinition Custom(BattleMap map, Daytime daytime) => new CampaignDefinition
        {
            seed = map.Seed,
            terrain = map.Terrain,
            daytime = daytime,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            zeppelins = map.Terrain == TerrainKind.Verdun,
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
                case 9: return Level9;
                default:
                    Debug.LogError($"CampaignLevels: no definition for level {number}; flying Level 1's.");
                    return Level1;
            }
        }
    }
}
