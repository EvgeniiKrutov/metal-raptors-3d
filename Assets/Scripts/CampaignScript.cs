using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public enum CampaignOp { Wait, Say, Task, TaskDone, Wave, Spawn, WaitClear, Finish }

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
            var root = Json.Parse(source) as Dictionary<string, object>;
            var list = root != null ? Value(root, "steps") as List<object> : null;

            if (list == null)
            {
                Debug.LogError($"CampaignScript '{origin}': no 'steps' array.");
                return null;
            }

            var steps = new List<CampaignStep>();
            for (int i = 0; i < list.Count; i++)
            {
                CampaignStep step = ParseStep(list[i] as Dictionary<string, object>, origin, i);
                if (step != null) steps.Add(step);
            }

            if (steps.Count == 0)
                Debug.LogError($"CampaignScript '{origin}': parsed to no steps.");

            return new CampaignScript(steps.ToArray());
        }

        static CampaignStep ParseStep(Dictionary<string, object> step, string origin, int index)
        {
            if (step == null)
            {
                Debug.LogError($"CampaignScript {origin}[{index}]: step is not an object.");
                return null;
            }

            string op = Text(step, "op");

            switch (op.ToLowerInvariant())
            {
                case "wait":
                    return new CampaignStep { op = CampaignOp.Wait, seconds = Seconds(step) };
                case "say": return ParseSay(step, origin, index);
                case "task": return ParseTask(step, origin, index);
                case "taskdone": return new CampaignStep { op = CampaignOp.TaskDone };
                case "wave": return ParseWave(CampaignOp.Wave, step, origin, index);
                case "spawn": return ParseWave(CampaignOp.Spawn, step, origin, index);
                case "waitclear": return new CampaignStep { op = CampaignOp.WaitClear };
                case "finish": return new CampaignStep { op = CampaignOp.Finish };
                default:
                    Debug.LogError($"CampaignScript {origin}[{index}]: unknown op '{op}'.");
                    return null;
            }
        }

        static CampaignStep ParseSay(Dictionary<string, object> step, string origin, int index)
        {
            string speaker = Text(step, "speaker");
            string key = Text(step, "line");

            if (speaker.Length == 0 || key.Length == 0)
            {
                Debug.LogError($"CampaignScript {origin}[{index}]: 'say' needs 'speaker' and 'line'.");
                return null;
            }

            string line = DialogueLines.For(key);
            float seconds = Seconds(step);

            return new CampaignStep
            {
                op = CampaignOp.Say,
                speaker = CampaignSpeakers.For(speaker),
                text = line,
                seconds = seconds > 0f ? seconds : ReadingTime(line),
            };
        }

        static CampaignStep ParseTask(Dictionary<string, object> step, string origin, int index)
        {
            string key = Text(step, "line");
            if (key.Length == 0)
            {
                Debug.LogError($"CampaignScript {origin}[{index}]: 'task' needs a 'line' key.");
                return null;
            }

            return new CampaignStep { op = CampaignOp.Task, text = DialogueLines.For(key) };
        }

        static CampaignStep ParseWave(CampaignOp op, Dictionary<string, object> step, string origin,
            int index)
        {
            var groups = new List<EnemyGroup>();

            if (Value(step, "enemies") is List<object> list)
            {
                foreach (object entry in list)
                {
                    if (!(entry is Dictionary<string, object> group)) continue;

                    string id = Text(group, "plane");
                    PlaneModelConfig plane = PlaneModels.ById(id);
                    if (plane == null)
                    {
                        Debug.LogError($"CampaignScript {origin}[{index}]: unknown plane '{id}'.");
                        continue;
                    }

                    int count = Mathf.RoundToInt(Number(group, "count", 1f));
                    groups.Add(new EnemyGroup(plane, Mathf.Max(1, count)));
                }
            }

            if (groups.Count == 0)
            {
                Debug.LogError($"CampaignScript {origin}[{index}]: '{op}' names no planes.");
                return null;
            }

            return new CampaignStep { op = op, groups = groups.ToArray() };
        }

        static float Seconds(Dictionary<string, object> step) =>
            Mathf.Max(0f, Number(step, "seconds", 0f));

        static object Value(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out object value) ? value : null;

        static string Text(Dictionary<string, object> obj, string key) =>
            Value(obj, key) is string text ? text.Trim() : string.Empty;

        static float Number(Dictionary<string, object> obj, string key, float fallback) =>
            Value(obj, key) is double number ? (float)number : fallback;

        public static float ReadingTime(string text)
        {
            int words = text.Split(Space, StringSplitOptions.RemoveEmptyEntries).Length;
            return Mathf.Clamp(ReadBase + words * ReadPerWord, ReadMin, ReadMax);
        }
    }
}
