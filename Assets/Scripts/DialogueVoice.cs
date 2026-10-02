using UnityEngine;

namespace MetalRaptors
{
    public class DialogueVoice
    {
        readonly VoiceChannel[] _channels;
        VoiceChannel _current;
        int _next;

        public DialogueVoice(Transform parent)
        {
            var root = new GameObject("Radio Voice");
            root.transform.SetParent(parent, false);

            _channels = new[]
            {
                VoiceChannel.Create(root.transform, "Voice A"),
                VoiceChannel.Create(root.transform, "Voice B"),
            };

            CampaignVoices.Preload();
        }

        public void Play(CampaignSpeaker speaker, string text)
        {
            Cut();
            if (!Speaks(text)) return;

            AudioClip clip = CampaignVoices.For(speaker);
            if (clip == null) return;

            _current = _channels[_next];
            _next = (_next + 1) % _channels.Length;
            _current.Play(clip, CampaignVoices.MarksOf(clip));
        }

        public void Finish()
        {
            if (_current != null) _current.Finish();
        }

        public void Cut()
        {
            if (_current != null) _current.Cut();
            _current = null;
        }

        static bool Speaks(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (char c in text)
                if (char.IsLetterOrDigit(c)) return true;

            return false;
        }
    }
}
