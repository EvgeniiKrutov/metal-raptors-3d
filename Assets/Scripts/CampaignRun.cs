using UnityEngine;

namespace MetalRaptors
{
    public static class CampaignRun
    {
        public const int FirstLevel = 1;
        public const int LastLevel = 2;

        public static int Level { get; private set; } = FirstLevel;

        public static void Request(int level) => Level = Mathf.Clamp(level, FirstLevel, LastLevel);
    }

    public readonly struct CampaignLevelEntry
    {
        public readonly int Number;
        public readonly string Label;
        public readonly string MapName;

        public CampaignLevelEntry(int number, string label, string mapName)
        {
            Number = number;
            Label = label;
            MapName = mapName;
        }
    }

    public static class CampaignLevelList
    {
        public static readonly CampaignLevelEntry[] All =
        {
            new CampaignLevelEntry(1, "level 1", TerrainNames.Verdun),
            new CampaignLevelEntry(2, "level 2", TerrainNames.FlandersFull),
        };
    }
}
