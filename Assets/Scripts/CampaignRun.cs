using UnityEngine;

namespace MetalRaptors
{
    public static class CampaignRun
    {
        public const int FirstLevel = 1;
        public const int LastLevel = CampaignLevels.Count;

        public static int Level { get; private set; } = FirstLevel;

        public static void Request(int level) => Level = Mathf.Clamp(level, FirstLevel, LastLevel);
    }

    public static class CampaignProgress
    {
        public static int HighestCompleted =>
            GameManager.Instance != null ? GameManager.Instance.CampaignLevelsCompleted : 0;

        public static bool IsCompleted(int level) => level <= HighestCompleted;

        public static bool IsUnlocked(int level) => level <= HighestCompleted + 1;

        public static bool AllCompleted => HighestCompleted >= CampaignRun.LastLevel;

        public static int NextLevel =>
            Mathf.Clamp(HighestCompleted + 1, CampaignRun.FirstLevel, CampaignRun.LastLevel);

        public static void Complete(int level)
        {
            if (GameManager.Instance != null) GameManager.Instance.CompleteCampaignLevel(level);
        }
    }

    public readonly struct CampaignLevelEntry
    {
        public readonly int Number;
        public readonly string Label;
        public readonly string Title;
        public readonly string MapName;
        public readonly string Dateline;
        public readonly string Brief;
        public readonly TerrainKind Terrain;
        public readonly int Seed;

        public CampaignLevelEntry(int number, CampaignDefinition level)
        {
            Number = number;
            Label = $"level {number}";
            Title = level.title;
            MapName = TerrainNames.Full(level.terrain);
            Dateline = level.dateline;
            Brief = FirstParagraph(level.lore);
            Terrain = level.terrain;
            Seed = level.seed;
        }

        static string FirstParagraph(string lore)
        {
            if (string.IsNullOrEmpty(lore)) return string.Empty;

            int split = lore.IndexOf("\n\n", System.StringComparison.Ordinal);
            return split > 0 ? lore.Substring(0, split) : lore;
        }
    }

    public static class CampaignLevelList
    {
        public static readonly CampaignLevelEntry[] All = Build();

        static CampaignLevelEntry[] Build()
        {
            var entries = new CampaignLevelEntry[CampaignRun.LastLevel];
            for (int i = 0; i < entries.Length; i++)
            {
                int number = CampaignRun.FirstLevel + i;
                entries[i] = new CampaignLevelEntry(number, CampaignLevels.ForNumber(number));
            }
            return entries;
        }
    }
}
