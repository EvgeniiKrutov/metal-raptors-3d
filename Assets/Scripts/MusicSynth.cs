using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class RenderedMusic
    {
        public AudioClip Intro;
        public AudioClip Loop;
        public double IntroDuration;
    }

    public class MusicBake
    {
        public float[] Intro;
        public float[] Loop;
        public int Channels = 2;
        public int Rate;
        public double IntroDuration;
    }

    public static class MusicSynth
    {
        const float GateRatio = 0.9f;
        const float DetuneVoiceLevel = 0.5f;
        const float DefaultAttack = 0.01f;
        const float DefaultRelease = 0.05f;
        const float NoiseDefaultAttack = 0.005f;
        const float NoiseFilterHz = 3500f;
        const float NoiseMaxGateSec = 0.25f;
        const int NoiseSeed = 1234;

        static readonly Dictionary<string, float> TrackPans = new Dictionary<string, float>
        {
            ["lead"] = -0.20f,
            ["harmony"] = 0.28f,
            ["arp"] = 0.42f,
            ["hats"] = -0.30f,
            ["organLow"] = -0.22f,
            ["organMid"] = 0.26f,
            ["organHigh"] = 0.40f,
            ["horns"] = 0.30f,
            ["lowbrass"] = -0.28f,
            ["toms"] = 0.24f,
            ["pulse"] = 0.30f,
            ["grit"] = 0.30f,
            ["drone"] = 0.22f,
        };

        readonly struct StereoGain
        {
            public readonly float L;
            public readonly float R;

            public StereoGain(float pan)
            {
                float p = Mathf.Clamp(pan, -1f, 1f);
                L = p > 0f ? 1f - p : 1f;
                R = p < 0f ? 1f + p : 1f;
            }
        }

        public static RenderedMusic Render(MusicConfig config)
        {
            var bake = Bake(config, AudioSettings.outputSampleRate);
            return ToClips(config, bake);
        }

        public static MusicBake Bake(MusicConfig config, int rate)
        {
            var loop = BakeSection(config, rate, intro: false);
            if (loop == null) return null;

            var intro = BakeSection(config, rate, intro: true);
            if (intro != null)
            {
                loop.Intro = intro.Intro;
                loop.IntroDuration = intro.IntroDuration;
            }
            return loop;
        }

        public static MusicBake BakeSection(MusicConfig config, int rate, bool intro)
        {
            if (config == null || config.Sequence.Count == 0) return null;

            int loopStart = LoopStartIndex(config);
            if (intro && loopStart <= 0) return null;

            int from = intro ? 0 : loopStart;
            int to = intro ? loopStart : config.Sequence.Count;
            var durations = PatternDurations(config);
            var bake = new MusicBake { Rate = rate, Channels = 2 };

            float[] samples;
            double lengthSec;
            if (config.IsRetro)
                samples = MusicSynthRetro.Render(config, from, to, rate, durations, !intro, out lengthSec);
            else
                samples = RenderSection(config, from, to, rate, durations, BuildNoise(rate), out lengthSec);

            if (samples == null) return null;
            if (intro)
            {
                bake.Intro = samples;
                bake.IntroDuration = lengthSec;
            }
            else
            {
                bake.Loop = samples;
            }
            return bake;
        }

        public static int LoopStartIndex(MusicConfig config) =>
            config == null || config.Sequence.Count == 0
                ? 0
                : Mathf.Clamp(config.LoopStart, 0, config.Sequence.Count - 1);

        public static double SectionSeconds(MusicConfig config, int from, int to)
        {
            if (config == null) return 0;
            var durations = PatternDurations(config);
            double total = 0;
            for (int i = from; i < to && i < config.Sequence.Count; i++)
            {
                if (durations.TryGetValue(config.Sequence[i], out double duration)) total += duration;
            }
            return total;
        }

        public static RenderedMusic ToClips(MusicConfig config, MusicBake bake)
        {
            if (bake?.Loop == null) return null;

            return new RenderedMusic
            {
                Intro = ToClip(bake.Intro, bake, $"{config.Id}-intro"),
                Loop = ToClip(bake.Loop, bake, $"{config.Id}-loop"),
                IntroDuration = bake.IntroDuration,
            };
        }

        public static AudioClip ToClip(MusicBake bake, bool intro, string name) =>
            bake == null ? null : ToClip(intro ? bake.Intro : bake.Loop, bake, name);

        static AudioClip ToClip(float[] samples, MusicBake bake, string name)
        {
            if (samples == null || samples.Length == 0) return null;
            int frames = samples.Length / bake.Channels;
            var clip = AudioClip.Create(name, frames, bake.Channels, bake.Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static Dictionary<string, double> PatternDurations(MusicConfig config)
        {
            double secPerBeat = 60.0 / config.Tempo;
            var durations = new Dictionary<string, double>();
            foreach (var kvp in config.Patterns)
            {
                double longest = 0;
                foreach (var notes in kvp.Value.Values)
                {
                    double beats = 0;
                    foreach (var note in notes) beats += note.Beats;
                    longest = Math.Max(longest, beats);
                }
                durations[kvp.Key] = longest * secPerBeat;
            }
            return durations;
        }

        static float[] RenderSection(MusicConfig config, int from, int to, int rate,
            Dictionary<string, double> durations, float[] noise, out double lengthSec)
        {
            double bodySec = 0;
            for (int i = from; i < to; i++)
            {
                if (durations.TryGetValue(config.Sequence[i], out double duration)) bodySec += duration;
            }

            int frames = (int)Math.Round(bodySec * rate);
            lengthSec = frames / (double)rate;
            if (frames <= 0) return null;

            var buffer = new float[frames * 2];
            double secPerBeat = 60.0 / config.Tempo;
            double time = 0;

            for (int i = from; i < to; i++)
            {
                string patternName = config.Sequence[i];
                if (config.Patterns.TryGetValue(patternName, out var pattern))
                {
                    foreach (var line in pattern)
                    {
                        if (!config.Tracks.TryGetValue(line.Key, out var track)) continue;
                        var gain = new StereoGain(PanOf(line.Key, track));
                        double noteTime = time;
                        foreach (var note in line.Value)
                        {
                            double duration = note.Beats * secPerBeat;
                            RenderNote(buffer, frames, rate, track, note, noteTime, duration,
                                config.Volume, noise, gain);
                            noteTime += duration;
                        }
                    }
                }
                if (durations.TryGetValue(patternName, out double patternDuration)) time += patternDuration;
            }

            for (int i = 0; i < buffer.Length; i++) buffer[i] = Mathf.Clamp(buffer[i], -1f, 1f);
            return buffer;
        }

        static float PanOf(string name, MusicTrack track)
        {
            if (track.HasPan) return track.Pan;
            return TrackPans.TryGetValue(name, out float pan) ? pan : 0f;
        }

        static void RenderNote(float[] buffer, int frames, int rate, MusicTrack track, MusicNote note,
            double startSec, double durationSec, float master, float[] noise, StereoGain gain)
        {
            float level = track.Volume * note.Velocity * master;
            if (level <= 0f || durationSec <= 0) return;

            if (track.Wave == "noise")
            {
                RenderNoise(buffer, frames, rate, track, level, startSec, durationSec, noise, gain);
                return;
            }
            if (note.Frequency <= 0f) return;

            if (track.Detune > 0f)
            {
                RenderVoice(buffer, frames, rate, track, note.Frequency, track.Detune / 2f,
                    level * DetuneVoiceLevel, startSec, durationSec, gain);
                RenderVoice(buffer, frames, rate, track, note.Frequency, -track.Detune / 2f,
                    level * DetuneVoiceLevel, startSec, durationSec, gain);
            }
            else
            {
                RenderVoice(buffer, frames, rate, track, note.Frequency, 0f, level, startSec,
                    durationSec, gain);
            }
        }

        static void RenderVoice(float[] buffer, int frames, int rate, MusicTrack track, float frequency,
            float detuneCents, float level, double startSec, double durationSec, StereoGain gain)
        {
            float attack = track.Attack >= 0f ? track.Attack : DefaultAttack;
            float release = track.Release >= 0f ? track.Release : DefaultRelease;
            double gate = durationSec * GateRatio;
            double releaseStart = Math.Max(attack, gate - release);

            int start = (int)Math.Round(startSec * rate);
            if (start < 0) return;
            int count = Math.Min((int)(gate * rate), frames - start);
            if (count <= 0) return;

            double phase = 0;
            double step = frequency * Math.Pow(2.0, detuneCents / 1200.0) / rate;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate;
                float env =
                    t < attack ? (float)(t / attack) :
                    t <= releaseStart ? 1f :
                    (float)((gate - t) / (gate - releaseStart));

                float sample = Sample(track.Wave, phase) * level * env;
                int o = (start + i) * 2;
                buffer[o] += sample * gain.L;
                buffer[o + 1] += sample * gain.R;

                phase += step;
                if (phase >= 1.0) phase -= 1.0;
            }
        }

        static void RenderNoise(float[] buffer, int frames, int rate, MusicTrack track, float level,
            double startSec, double durationSec, float[] noise, StereoGain gain)
        {
            float attack = track.Attack >= 0f ? track.Attack : NoiseDefaultAttack;
            double gate = Math.Min(durationSec * GateRatio, NoiseMaxGateSec);

            int start = (int)Math.Round(startSec * rate);
            if (start < 0) return;
            int count = Math.Min((int)(gate * rate), frames - start);
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate;
                float env = t < attack
                    ? (float)(t / attack)
                    : (float)((gate - t) / (gate - attack));

                float sample = noise[i % noise.Length] * level * env;
                int o = (start + i) * 2;
                buffer[o] += sample * gain.L;
                buffer[o + 1] += sample * gain.R;
            }
        }

        static float Sample(string wave, double phase)
        {
            switch (wave)
            {
                case "square": return phase < 0.5 ? 1f : -1f;
                case "triangle": return phase < 0.5 ? (float)(4.0 * phase - 1.0) : (float)(3.0 - 4.0 * phase);
                case "sawtooth": return (float)(2.0 * phase - 1.0);
                default: return Mathf.Sin((float)(phase * 2.0 * Math.PI));
            }
        }

        static float[] BuildNoise(int rate)
        {
            var random = new System.Random(NoiseSeed);
            var noise = new float[rate];
            for (int i = 0; i < noise.Length; i++) noise[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            double w0 = 2.0 * Math.PI * NoiseFilterHz / rate;
            double cosw = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * 0.7071);
            double a0 = 1.0 + alpha;
            double b0 = (1.0 + cosw) / 2.0 / a0;
            double b1 = -(1.0 + cosw) / a0;
            double b2 = b0;
            double a1 = -2.0 * cosw / a0;
            double a2 = (1.0 - alpha) / a0;

            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < noise.Length; i++)
            {
                double x0 = noise[i];
                double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x0;
                y2 = y1;
                y1 = y0;
                noise[i] = (float)y0;
            }
            return noise;
        }
    }
}
