using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MetalRaptors
{
    public readonly struct MusicTrackInfo
    {
        public readonly string Id;
        public readonly string Title;

        public MusicTrackInfo(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }

    public static class MusicTracks
    {
        public const string DefaultId = "flak-parade";

        const string Folder = "Music";
        const string CoverFolder = "ui/music/";
        const string PrefSelected = "mr_menu_track";
        const string MixSeparator = " — ";

        static readonly string[] Hidden = { };

        static readonly Regex NamePattern = new Regex("\"name\"\\s*:\\s*\"([^\"]*)\"");
        static readonly Dictionary<string, Sprite> Covers = new Dictionary<string, Sprite>();

        static MusicTrackInfo[] _all;
        static string _selected;

        public static MusicTrackInfo[] All
        {
            get
            {
                if (_all == null) _all = Scan();
                return _all;
            }
        }

        public static string Selected
        {
            get
            {
                if (_selected == null) _selected = Resolve(PlayerPrefs.GetString(PrefSelected, DefaultId));
                return _selected;
            }
        }

        public static void Select(string id)
        {
            if (!Listed(id)) return;

            _selected = id;
            PlayerPrefs.SetString(PrefSelected, id);
            PlayerPrefs.Save();
        }

        public static int IndexOf(string id)
        {
            MusicTrackInfo[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id == id) return i;
            }
            return 0;
        }

        public static Sprite Cover(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Covers.TryGetValue(id, out Sprite cached)) return cached;

            Sprite sprite = LoadCover(CoverFolder + id);
            Covers[id] = sprite;
            return sprite;
        }

        static Sprite LoadCover(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static MusicTrackInfo[] Scan()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(Folder);
            var tracks = new List<MusicTrackInfo>(assets.Length);

            foreach (TextAsset asset in assets)
            {
                if (IsHidden(asset.name)) continue;
                tracks.Add(new MusicTrackInfo(asset.name, TitleOf(asset)));
            }

            tracks.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
            return tracks.ToArray();
        }

        static string TitleOf(TextAsset asset)
        {
            Match match = NamePattern.Match(asset.text);
            string name = match.Success ? match.Groups[1].Value.Trim() : string.Empty;

            int cut = name.IndexOf(MixSeparator, StringComparison.Ordinal);
            if (cut > 0) name = name.Substring(0, cut).Trim();

            return name.Length > 0 ? name : asset.name;
        }

        static string Resolve(string id)
        {
            if (Listed(id)) return id;
            if (Listed(DefaultId)) return DefaultId;
            return All.Length > 0 ? All[0].Id : DefaultId;
        }

        static bool Listed(string id) =>
            !string.IsNullOrEmpty(id) && !IsHidden(id)
            && Resources.Load<TextAsset>(Folder + "/" + id) != null;

        static bool IsHidden(string id) => Array.IndexOf(Hidden, id) >= 0;
    }
}
