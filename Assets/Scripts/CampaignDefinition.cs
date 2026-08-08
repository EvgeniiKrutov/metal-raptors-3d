using UnityEngine;

namespace MetalRaptors
{
    public class EnemyWave
    {
        public float distance;
        public EnemyGroup[] groups;
    }

    public class CampaignDefinition
    {
        public int seed;
        public TerrainKind terrain = TerrainKind.Verdun;
        public Daytime daytime;
        public Weather weather;
        public CloudsPart clouds;
        public EnemyWave[] waves;
    }

    public static class CampaignLevels
    {
        public static CampaignDefinition Level1 => new CampaignDefinition
        {
            seed = 1917,
            terrain = TerrainKind.Verdun,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            waves = new EnemyWave[0],
        };

        public static CampaignDefinition Level2 => new CampaignDefinition
        {
            seed = 1918,
            terrain = TerrainKind.Flanders,
            daytime = Daytime.Morning,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            waves = new EnemyWave[0],
        };

        public static CampaignDefinition Custom(BattleMap map, Daytime daytime) => new CampaignDefinition
        {
            seed = map.Seed,
            terrain = map.Terrain,
            daytime = daytime,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            waves = new EnemyWave[0],
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
