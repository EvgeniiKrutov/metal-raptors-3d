using System;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignSpeaker
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool IsPlayer;
        public readonly string Skin;

        public CampaignSpeaker(string id, string name, bool isPlayer, string skin = null)
        {
            Id = id;
            Name = name;
            IsPlayer = isPlayer;
            Skin = skin;
        }
    }

    public static class CampaignSpeakers
    {
        public static readonly CampaignSpeaker Player =
            new CampaignSpeaker("you", "VASSEUR", true);

        public static readonly CampaignSpeaker[] All =
        {
            Player,
            new CampaignSpeaker("roussel", "ROUSSEL", false, "white"),
            new CampaignSpeaker("marchand", "MARCHAND", false, "dark_blue"),
            new CampaignSpeaker("crane", "CRANE", false, "red"),
            new CampaignSpeaker("lasalle", "LASALLE", false),
            new CampaignSpeaker("ravensberg", "RAVENSBERG", false),

            new CampaignSpeaker("hq", "FLIGHT CONTROL", false),
            new CampaignSpeaker("wing", "BLUE TWO", false),
            new CampaignSpeaker("ace", "RED BARON", false),
        };

        public static CampaignSpeaker Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (CampaignSpeaker speaker in All)
                if (string.Equals(speaker.Id, id, StringComparison.OrdinalIgnoreCase))
                    return speaker;

            return null;
        }

        public static CampaignSpeaker For(string id)
        {
            CampaignSpeaker speaker = Find(id);
            if (speaker != null) return speaker;

            Debug.LogError($"CampaignSpeakers: unknown speaker '{id}'; using the player.");
            return Player;
        }

        public static string SkinOf(string id) => Find(id)?.Skin;
    }
}
