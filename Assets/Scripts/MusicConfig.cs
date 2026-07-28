using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MetalRaptors
{
    /// <summary>Data model for one synthesized soundtrack. See docs/music.md.</summary>
    public class MusicConfig
    {
        public const string RetroEngine = "retrowave";

        public string Id = "";
        public string Name = "";
        public string Engine = "";
        public float Tempo = 120f;
        public float Volume = 0.5f;
        public int LoopStart;
        public MusicFx Fx;
        public MusicSidechain Sidechain;
        public readonly Dictionary<string, MusicTrack> Tracks = new Dictionary<string, MusicTrack>();
        public readonly Dictionary<string, Dictionary<string, List<MusicNote>>> Patterns =
            new Dictionary<string, Dictionary<string, List<MusicNote>>>();
        public readonly List<string> Sequence = new List<string>();

        public bool IsRetro => Engine == RetroEngine;
    }

    public class MusicTrack
    {
        public string Wave = "sine";
        public float Volume = 1f;
        public float Detune;
        public float Attack = -1f;
        public float Release = -1f;

        public string Drum = "";
        public int Voices = 1;
        public float Spread;
        public float Width;
        public float Pan;
        public float Gate = -1f;
        public float Glide;
        public float Duck;
        public float PulseWidth = 0.5f;
        public float DelaySend;
        public float ReverbSend;
        public MusicAdsr Adsr;
        public MusicFilter Filter;

        public float Tune = -1f;
        public float Drop = -1f;
        public float Decay = -1f;
        public float Click = -1f;
        public float NoiseMix = -1f;
        public float GateAmount;
        public float GateHold = 0.16f;
    }

    public class MusicAdsr
    {
        public float Attack = 0.01f;
        public float Decay = 0.1f;
        public float Sustain = 1f;
        public float Release = 0.08f;
    }

    public class MusicFilter
    {
        public float Cutoff = 20000f;
        public float Resonance;
        public float Env;
        public float Decay = 0.2f;
        public float Keytrack;
    }

    public class MusicFx
    {
        public MusicDelay Delay;
        public MusicReverb Reverb;
    }

    public class MusicDelay
    {
        public float Beats = 0.75f;
        public float Feedback = 0.3f;
        public float Mix = 1f;
        public float Damp = 4500f;
        public bool PingPong = true;
    }

    public class MusicReverb
    {
        public float Size = 0.8f;
        public float Damp = 0.4f;
        public float Mix = 1f;
        public float Width = 1f;
    }

    public class MusicSidechain
    {
        public string Source = "kick";
        public float Amount = 0.6f;
        public float Attack = 0.005f;
        public float Release = 0.25f;
    }

    public class MusicNote
    {
        public float Frequency;
        public float Beats;
        public float Velocity = 1f;
    }

    /// <summary>Loads music JSON from a Resources/Music folder and caches parsed configs.</summary>
    public static class MusicLibrary
    {
        static readonly Dictionary<string, MusicConfig> Cache = new Dictionary<string, MusicConfig>();
        static readonly Dictionary<char, int> NoteOffsets = new Dictionary<char, int>
        {
            ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11,
        };
        static readonly Regex PitchPattern = new Regex(@"^([A-Ga-g])([#b]?)(-?\d+)$");

        public static MusicConfig Load(string id)
        {
            if (Cache.TryGetValue(id, out var cached)) return cached;

            var asset = Resources.Load<TextAsset>("Music/" + id);
            if (asset == null)
            {
                Debug.LogWarning($"Music '{id}' not found under a Resources/Music folder.");
                return null;
            }

            var config = Build(id, MusicJson.Parse(asset.text) as Dictionary<string, object>);
            Cache[id] = config;
            return config;
        }

        static MusicConfig Build(string id, Dictionary<string, object> root)
        {
            if (root == null) return null;

            var config = new MusicConfig
            {
                Id = GetString(root, "id", id),
                Name = GetString(root, "name", ""),
                Engine = GetString(root, "engine", ""),
                Tempo = GetFloat(root, "tempo", 120f),
                Volume = GetFloat(root, "volume", 0.5f),
                LoopStart = (int)GetFloat(root, "loopStart", 0f),
            };

            if (GetObject(root, "fx") is Dictionary<string, object> fx)
            {
                config.Fx = new MusicFx();
                if (GetObject(fx, "delay") is Dictionary<string, object> delay)
                {
                    config.Fx.Delay = new MusicDelay
                    {
                        Beats = GetFloat(delay, "beats", 0.75f),
                        Feedback = GetFloat(delay, "feedback", 0.3f),
                        Mix = GetFloat(delay, "mix", 1f),
                        Damp = GetFloat(delay, "damp", 4500f),
                        PingPong = GetBool(delay, "pingpong", true),
                    };
                }
                if (GetObject(fx, "reverb") is Dictionary<string, object> reverb)
                {
                    config.Fx.Reverb = new MusicReverb
                    {
                        Size = GetFloat(reverb, "size", 0.8f),
                        Damp = GetFloat(reverb, "damp", 0.4f),
                        Mix = GetFloat(reverb, "mix", 1f),
                        Width = GetFloat(reverb, "width", 1f),
                    };
                }
            }

            if (GetObject(root, "sidechain") is Dictionary<string, object> chain)
            {
                config.Sidechain = new MusicSidechain
                {
                    Source = GetString(chain, "source", "kick"),
                    Amount = GetFloat(chain, "amount", 0.6f),
                    Attack = GetFloat(chain, "attack", 0.005f),
                    Release = GetFloat(chain, "release", 0.25f),
                };
            }

            if (GetObject(root, "tracks") is Dictionary<string, object> tracks)
            {
                foreach (var kvp in tracks)
                {
                    if (kvp.Value is Dictionary<string, object> track) config.Tracks[kvp.Key] = BuildTrack(track);
                }
            }

            if (root.TryGetValue("patterns", out var patternsValue) && patternsValue is Dictionary<string, object> patterns)
            {
                foreach (var patternKvp in patterns)
                {
                    if (!(patternKvp.Value is Dictionary<string, object> lines)) continue;
                    var pattern = new Dictionary<string, List<MusicNote>>();
                    foreach (var lineKvp in lines)
                    {
                        if (lineKvp.Value is List<object> tuples) pattern[lineKvp.Key] = BuildNotes(tuples);
                    }
                    config.Patterns[patternKvp.Key] = pattern;
                }
            }

            if (root.TryGetValue("sequence", out var sequenceValue) && sequenceValue is List<object> sequence)
            {
                foreach (var entry in sequence)
                {
                    if (entry is string name) config.Sequence.Add(name);
                }
            }

            return config;
        }

        static MusicTrack BuildTrack(Dictionary<string, object> track)
        {
            var result = new MusicTrack
            {
                Wave = GetString(track, "wave", "sine"),
                Volume = GetFloat(track, "volume", 1f),
                Detune = GetFloat(track, "detune", 0f),
                Attack = GetFloat(track, "attack", -1f),
                Release = GetFloat(track, "release", -1f),
                Drum = GetString(track, "drum", ""),
                Voices = (int)GetFloat(track, "voices", 1f),
                Spread = GetFloat(track, "spread", 0f),
                Width = GetFloat(track, "width", 0f),
                Pan = GetFloat(track, "pan", 0f),
                Gate = GetFloat(track, "gate", -1f),
                Glide = GetFloat(track, "glide", 0f),
                Duck = GetFloat(track, "duck", 0f),
                PulseWidth = GetFloat(track, "pulseWidth", 0.5f),
                Tune = GetFloat(track, "tune", -1f),
                Drop = GetFloat(track, "drop", -1f),
                Decay = GetFloat(track, "decay", -1f),
                Click = GetFloat(track, "click", -1f),
                NoiseMix = GetFloat(track, "noiseMix", -1f),
                GateAmount = GetFloat(track, "gatedReverb", 0f),
                GateHold = GetFloat(track, "gateHold", 0.16f),
            };

            if (GetObject(track, "send") is Dictionary<string, object> send)
            {
                result.DelaySend = GetFloat(send, "delay", 0f);
                result.ReverbSend = GetFloat(send, "reverb", 0f);
            }

            if (GetObject(track, "adsr") is Dictionary<string, object> adsr)
            {
                result.Adsr = new MusicAdsr
                {
                    Attack = GetFloat(adsr, "attack", 0.01f),
                    Decay = GetFloat(adsr, "decay", 0.1f),
                    Sustain = GetFloat(adsr, "sustain", 1f),
                    Release = GetFloat(adsr, "release", 0.08f),
                };
            }

            if (GetObject(track, "filter") is Dictionary<string, object> filter)
            {
                result.Filter = new MusicFilter
                {
                    Cutoff = GetFloat(filter, "cutoff", 20000f),
                    Resonance = GetFloat(filter, "resonance", 0f),
                    Env = GetFloat(filter, "env", 0f),
                    Decay = GetFloat(filter, "decay", 0.2f),
                    Keytrack = GetFloat(filter, "keytrack", 0f),
                };
            }

            return result;
        }

        static List<MusicNote> BuildNotes(List<object> tuples)
        {
            var notes = new List<MusicNote>(tuples.Count);
            foreach (var value in tuples)
            {
                if (!(value is List<object> tuple) || tuple.Count < 2) continue;
                notes.Add(new MusicNote
                {
                    Frequency = PitchToFrequency(tuple[0]),
                    Beats = tuple[1] is double beats ? (float)beats : 0f,
                    Velocity = tuple.Count > 2 && tuple[2] is double velocity ? (float)velocity : 1f,
                });
            }
            return notes;
        }

        static float PitchToFrequency(object pitch)
        {
            if (pitch is double hz) return hz > 0 ? (float)hz : 0f;
            if (!(pitch is string name)) return 0f;

            var match = PitchPattern.Match(name.Trim());
            if (!match.Success) return 0f;

            int semitone = NoteOffsets[char.ToUpperInvariant(match.Groups[1].Value[0])];
            if (match.Groups[2].Value == "#") semitone += 1;
            if (match.Groups[2].Value == "b") semitone -= 1;
            int midi = (int.Parse(match.Groups[3].Value) + 1) * 12 + semitone;
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        static float GetFloat(Dictionary<string, object> obj, string key, float fallback) =>
            obj.TryGetValue(key, out var value) && value is double number ? (float)number : fallback;

        static bool GetBool(Dictionary<string, object> obj, string key, bool fallback) =>
            obj.TryGetValue(key, out var value) && value is bool flag ? flag : fallback;

        static string GetString(Dictionary<string, object> obj, string key, string fallback) =>
            obj.TryGetValue(key, out var value) && value is string text ? text : fallback;

        static object GetObject(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;
    }
}
