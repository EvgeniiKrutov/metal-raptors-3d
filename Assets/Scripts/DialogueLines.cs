using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class DialogueLines
    {
        public const string Resource = "Dialogue/lines";

        static Dictionary<string, string> _lines;

        public static string For(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            Load();
            if (_lines.TryGetValue(key, out string line)) return line;

            Debug.LogError($"DialogueLines: no line '{key}' in {Resource}.json.");
            return key;
        }

        static void Load()
        {
            if (_lines != null) return;

            _lines = new Dictionary<string, string>();

            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null)
            {
                Debug.LogError($"DialogueLines: no line table at Resources/{Resource}.json.");
                return;
            }

            if (!(Json.Parse(asset.text) is Dictionary<string, object> root))
            {
                Debug.LogError($"DialogueLines: {Resource}.json is not a key/text object.");
                return;
            }

            foreach (KeyValuePair<string, object> entry in root)
            {
                if (entry.Value is string text) _lines[entry.Key] = text;
                else Debug.LogError($"DialogueLines: '{entry.Key}' is not a string.");
            }
        }
    }
}
