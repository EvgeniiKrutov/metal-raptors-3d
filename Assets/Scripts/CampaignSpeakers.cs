using System;
using UnityEngine;

namespace MetalRaptors
{
    public class CampaignSpeaker
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool IsPlayer;

        public CampaignSpeaker(string id, string name, bool isPlayer)
        {
            Id = id;
            Name = name;
            IsPlayer = isPlayer;
        }
    }

    public static class CampaignSpeakers
    {
        public static readonly CampaignSpeaker Player =
            new CampaignSpeaker("you", "YOU", true);

        public static readonly CampaignSpeaker[] All =
        {
            Player,
            new CampaignSpeaker("hq", "FLIGHT CONTROL", false),
            new CampaignSpeaker("wing", "BLUE TWO", false),
            new CampaignSpeaker("ace", "RED BARON", false),
        };

        public static CampaignSpeaker For(string id)
        {
            foreach (CampaignSpeaker speaker in All)
                if (string.Equals(speaker.Id, id, StringComparison.OrdinalIgnoreCase))
                    return speaker;

            Debug.LogError($"CampaignSpeakers: unknown speaker '{id}'; using the player.");
            return Player;
        }
    }
}
