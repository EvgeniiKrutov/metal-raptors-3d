using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MetalRaptors
{
    public static class CampaignVoices
    {
        public const string Folder = "Sounds/Voices/";

        const string MarksPath = Folder + "marks";

        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        static readonly char[] Space = { ' ', '\t' };

        static Dictionary<string, float[]> _marks;

        public static AudioClip For(CampaignSpeaker speaker)
        {
            if (speaker == null) return null;

            if (Cache.TryGetValue(speaker.Id, out AudioClip cached)) return cached;

            AudioClip clip = Resources.Load<AudioClip>(Folder + speaker.Id)
                          ?? Resources.Load<AudioClip>(Folder + speaker.Name.ToLowerInvariant());

            Cache[speaker.Id] = clip;
            return clip;
        }

        public static void Preload()
        {
            foreach (CampaignSpeaker speaker in CampaignSpeakers.All)
            {
                AudioClip clip = For(speaker);
                if (clip != null) clip.LoadAudioData();
            }
        }

        public static float[] MarksOf(AudioClip clip)
        {
            if (clip == null) return null;

            if (_marks == null) _marks = LoadMarks();
            return _marks.TryGetValue(clip.name, out float[] marks) ? marks : null;
        }

        static Dictionary<string, float[]> LoadMarks()
        {
            var marks = new Dictionary<string, float[]>();

            var asset = Resources.Load<TextAsset>(MarksPath);
            if (asset == null) return marks;

            foreach (string line in asset.text.Split('\n'))
            {
                string[] parts = line.Trim().Split(Space, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var times = new List<float>(parts.Length - 1);
                for (int i = 1; i < parts.Length; i++)
                    if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float time))
                        times.Add(time);

                marks[parts[0]] = times.ToArray();
            }

            return marks;
        }
    }
}
