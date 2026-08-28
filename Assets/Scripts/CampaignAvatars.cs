using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class CampaignAvatars
    {
        public const string Folder = "Avatars/";

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite For(CampaignSpeaker speaker)
        {
            if (speaker == null) return null;

            if (Cache.TryGetValue(speaker.Id, out Sprite cached)) return cached;

            Sprite sprite = Load(Folder + speaker.Id)
                         ?? Load(Folder + speaker.Name.ToLowerInvariant())
                         ?? Load(speaker.Id)
                         ?? Load(speaker.Name.ToLowerInvariant());

            Cache[speaker.Id] = sprite;
            return sprite;
        }

        static Sprite Load(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
