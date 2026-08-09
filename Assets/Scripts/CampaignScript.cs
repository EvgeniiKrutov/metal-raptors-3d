using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MetalRaptors
{
    public enum CampaignOp { Wait, Say, Wave, Spawn, WaitClear, Finish }

    public class CampaignStep
    {
        public CampaignOp op;
        public float seconds;
        public CampaignSpeaker speaker;
        public string text;
        public EnemyGroup[] groups;
    }

    public class CampaignScript
    {
        public const string ResourceFolder = "CampaignScripts/";

        const float ReadBase = 1.4f;
        const float ReadPerWord = 0.32f;
        const float ReadMin = 2.5f;
        const float ReadMax = 9f;

        static readonly char[] Space = { ' ', '\t' };

        public readonly CampaignStep[] Steps;

        CampaignScript(CampaignStep[] steps) => Steps = steps;

        public static CampaignScript Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var asset = Resources.Load<TextAsset>(ResourceFolder + name);
            if (asset == null)
            {
                Debug.LogError($"CampaignScript: no script asset '{ResourceFolder}{name}'.");
                return null;
            }
            return Parse(asset.text, name);
        }

        public static CampaignScript Parse(string source, string origin)
        {
            var steps = new List<CampaignStep>();
            string[] lines = source.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                CampaignStep step = ParseLine(line, origin, i + 1);
                if (step != null) steps.Add(step);
            }

            if (steps.Count == 0)
                Debug.LogError($"CampaignScript '{origin}': parsed to no steps.");

            return new CampaignScript(steps.ToArray());
        }

        static CampaignStep ParseLine(string line, string origin, int number)
        {
            int cut = line.IndexOfAny(Space);
            string keyword = (cut < 0 ? line : line.Substring(0, cut)).ToLowerInvariant();
            string rest = cut < 0 ? string.Empty : line.Substring(cut + 1).Trim();

            switch (keyword)
            {
                case "wait":
                    return new CampaignStep
                    {
                        op = CampaignOp.Wait,
                        seconds = ParseSeconds(rest, origin, number),
                    };
                case "say": return ParseSay(rest, origin, number);
                case "wave": return ParseWave(CampaignOp.Wave, rest, origin, number);
                case "spawn": return ParseWave(CampaignOp.Spawn, rest, origin, number);
                case "waitclear": return new CampaignStep { op = CampaignOp.WaitClear };
                case "finish": return new CampaignStep { op = CampaignOp.Finish };
                default:
                    Debug.LogError($"CampaignScript {origin}:{number}: unknown op '{keyword}'.");
                    return null;
            }
        }

        static CampaignStep ParseSay(string rest, string origin, int number)
        {
            int colon = rest.IndexOf(':');
            if (colon < 0)
            {
                Debug.LogError($"CampaignScript {origin}:{number}: 'say' needs '<speaker>: <text>'.");
                return null;
            }

            string[] head = rest.Substring(0, colon).Split(Space, StringSplitOptions.RemoveEmptyEntries);
            string text = rest.Substring(colon + 1).Trim();

            if (head.Length == 0 || text.Length == 0)
            {
                Debug.LogError($"CampaignScript {origin}:{number}: 'say' needs a speaker and text.");
                return null;
            }

            float seconds = 0f;
            if (head.Length > 1) seconds = ParseSeconds(head[head.Length - 1], origin, number);

            return new CampaignStep
            {
                op = CampaignOp.Say,
                speaker = CampaignSpeakers.For(head[0]),
                text = text,
                seconds = seconds > 0f ? seconds : ReadingTime(text),
            };
        }

        static CampaignStep ParseWave(CampaignOp op, string rest, string origin, int number)
        {
            var groups = new List<EnemyGroup>();

            foreach (string chunk in rest.Split(','))
            {
                string[] tokens = chunk.Split(Space, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;

                PlaneModelConfig plane = PlaneModels.ById(tokens[0]);
                if (plane == null)
                {
                    Debug.LogError($"CampaignScript {origin}:{number}: unknown plane '{tokens[0]}'.");
                    continue;
                }

                int count = 1;
                if (tokens.Length > 1)
                {
                    string raw = tokens[1].TrimStart('x', 'X');
                    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                    {
                        Debug.LogError($"CampaignScript {origin}:{number}: bad count '{tokens[1]}'.");
                        count = 1;
                    }
                }

                groups.Add(new EnemyGroup(plane, Mathf.Max(1, count)));
            }

            if (groups.Count == 0)
            {
                Debug.LogError($"CampaignScript {origin}:{number}: '{op}' names no planes.");
                return null;
            }

            return new CampaignStep { op = op, groups = groups.ToArray() };
        }

        static float ParseSeconds(string raw, string origin, int number)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return Mathf.Max(0f, value);

            Debug.LogError($"CampaignScript {origin}:{number}: '{raw}' is not a number of seconds.");
            return 0f;
        }

        static float ReadingTime(string text)
        {
            int words = text.Split(Space, StringSplitOptions.RemoveEmptyEntries).Length;
            return Mathf.Clamp(ReadBase + words * ReadPerWord, ReadMin, ReadMax);
        }
    }
}
