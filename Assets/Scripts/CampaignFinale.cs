using UnityEngine;

namespace MetalRaptors
{
    public static class CampaignFinale
    {
        public static void Play(CampaignDefinition level, int levelNumber, GameObject hud,
            string subtitle)
        {
            LevelOutro.Open(level.outro, () => Journal(level, levelNumber, hud, subtitle));
        }

        static void Journal(CampaignDefinition level, int levelNumber, GameObject hud,
            string subtitle)
        {
            if (string.IsNullOrEmpty(level.journal))
            {
                Completed(levelNumber, hud, subtitle);
                return;
            }

            LevelBriefing.OpenJournal(LevelOutro.JournalTitle,
                CampaignLevelEntry.DatePart(level.dateline), level.journal,
                () => Completed(levelNumber, hud, subtitle));
        }

        static void Completed(int levelNumber, GameObject hud, string subtitle)
        {
            bool hasNext = levelNumber < CampaignRun.LastLevel;
            GameMenu.Open(GameMenuKind.Completed, subtitle, hud,
                hasNext ? SceneNames.CampaignLevel1 : null,
                hasNext ? (System.Action)(() => CampaignRun.Request(levelNumber + 1)) : null);
        }
    }
}
