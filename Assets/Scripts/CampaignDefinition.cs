using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// One enemy formation keyed to distance flown: when the player's furthest X passes
    /// <see cref="distance"/> metres, the wave's groups are due. Configuration only for now —
    /// no campaign level spawns enemies yet (see docs/campaign.md).
    /// </summary>
    public class EnemyWave
    {
        public float distance;
        public EnemyGroup[] groups;
    }

    /// <summary>
    /// Everything that makes one endless campaign level: the seed the streamed terrain grows
    /// from, the daytime sky (picked on the campaign panel), the weather, and the distance-keyed
    /// enemy waves. <see cref="CampaignLevelController"/> composes the level from these parts.
    /// </summary>
    public class CampaignDefinition
    {
        public int seed;
        public Daytime daytime;
        public Weather weather;
        public CloudsPart clouds;
        public EnemyWave[] waves;
    }

    /// <summary>Registry of campaign level definitions, mirroring <see cref="Levels"/>.</summary>
    public static class CampaignLevels
    {
        public static CampaignDefinition Level1 => new CampaignDefinition
        {
            seed = 1917,
            daytime = GameManager.Instance != null
                ? GameManager.Instance.CampaignDaytime : Daytime.Midday,
            weather = Weather.Calm,
            clouds = new CloudsPart(),
            waves = new EnemyWave[0],
        };

        public static CampaignDefinition ForNumber(int number)
        {
            switch (number)
            {
                case 1: return Level1;
                default:
                    Debug.LogError($"CampaignLevels: no definition for level {number}; flying Level 1's.");
                    return Level1;
            }
        }
    }
}
