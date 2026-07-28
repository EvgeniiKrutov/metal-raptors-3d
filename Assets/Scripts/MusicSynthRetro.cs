using System;
using System.Collections.Generic;

namespace MetalRaptors
{
    /// <summary>Stereo subtractive renderer for tracks declaring "engine": "retrowave". See docs/music.md.</summary>
    public static class MusicSynthRetro
    {
        public const double TailSec = 2.5;

        const float DefaultGate = 0.9f;
        const int CoeffInterval = 32;
        const int NoiseSeed = 8087;
        const float RefHz = 261.6256f;

        class Ctx
        {
            public int Rate;
            public int Frames;
            public bool Wrap;
            public float[] Dry;
            public float[] DelaySend;
            public float[] ReverbSend;
            public float[] Duck;
            public float[] Noise;
            public float[] ScratchL = new float[0];
            public float[] ScratchR = new float[0];

            public void EnsureScratch(int count)
            {
                if (ScratchL.Length >= count) return;
                ScratchL = new float[count];
                ScratchR = new float[count];
            }
        }

        public static float[] Render(MusicConfig config, int from, int to, int rate,
            Dictionary<string, double> durations, bool wrap, out double lengthSec)
        {
            double bodySec = 0;
            for (int i = from; i < to; i++)
            {
                if (durations.TryGetValue(config.Sequence[i], out double duration)) bodySec += duration;
            }

            int body = (int)Math.Round(bodySec * rate);
            lengthSec = body / (double)rate;
            if (body <= 0) return null;

            int frames = wrap ? body : body + (int)(TailSec * rate);
            var ctx = new Ctx
            {
                Rate = rate,
                Frames = frames,
                Wrap = wrap,
                Dry = new float[frames * 2],
                DelaySend = new float[frames],
                ReverbSend = new float[frames],
                Noise = BuildNoise(rate),
            };
            ctx.Duck = BuildDuck(config, from, to, rate, durations, frames, wrap);

            double secPerBeat = 60.0 / config.Tempo;
            double time = 0;
            var previous = new Dictionary<string, float>();

            for (int i = from; i < to; i++)
            {
                string patternName = config.Sequence[i];
                if (config.Patterns.TryGetValue(patternName, out var pattern))
                {
                    foreach (var line in pattern)
                    {
                        if (!config.Tracks.TryGetValue(line.Key, out var track)) continue;
                        double noteTime = time;
                        previous.TryGetValue(line.Key, out float prevHz);
                        foreach (var note in line.Value)
                        {
                            double slot = note.Beats * secPerBeat;
                            float level = track.Volume * note.Velocity * config.Volume;
                            if (level > 0f && slot > 0)
                            {
                                if (!string.IsNullOrEmpty(track.Drum))
                                    RenderDrum(ctx, track, level, noteTime);
                                else if (note.Frequency > 0f)
                                    RenderVoiceGroup(ctx, track, note.Frequency, prevHz, level, noteTime, slot);
                            }
                            if (note.Frequency > 0f) prevHz = note.Frequency;
                            noteTime += slot;
                        }
                        previous[line.Key] = prevHz;
                    }
                }
                if (durations.TryGetValue(patternName, out double patternDuration)) time += patternDuration;
            }

            if (config.Fx?.Delay != null) ApplyDelay(ctx, config.Fx.Delay, secPerBeat);
            if (config.Fx?.Reverb != null) ApplyReverb(ctx, config.Fx.Reverb);

            for (int i = 0; i < ctx.Dry.Length; i++) ctx.Dry[i] = SoftClip(ctx.Dry[i]);
            return ctx.Dry;
        }

        static float[] BuildDuck(MusicConfig config, int from, int to, int rate,
            Dictionary<string, double> durations, int frames, bool wrap)
        {
            var duck = new float[frames];
            for (int i = 0; i < frames; i++) duck[i] = 1f;

            var chain = config.Sidechain;
            if (chain == null || chain.Amount <= 0f) return duck;

            double secPerBeat = 60.0 / config.Tempo;
            double time = 0;
            float attack = Math.Max(chain.Attack, 0.001f);
            float release = Math.Max(chain.Release, 0.01f);
            int span = (int)((attack + release) * rate);

            for (int i = from; i < to; i++)
            {
                string patternName = config.Sequence[i];
                if (config.Patterns.TryGetValue(patternName, out var pattern) &&
                    pattern.TryGetValue(chain.Source, out var notes))
                {
                    double noteTime = time;
                    foreach (var note in notes)
                    {
                        if (note.Velocity > 0f)
                        {
                            int start = (int)Math.Round(noteTime * rate);
                            for (int n = 0; n < span; n++)
                            {
                                int idx = Index(start + n, frames, wrap);
                                if (idx < 0) break;
                                float t = n / (float)rate;
                                float gain = t < attack
                                    ? 1f - chain.Amount * (t / attack)
                                    : 1f - chain.Amount * Math.Max(0f, 1f - (t - attack) / release);
                                if (gain < duck[idx]) duck[idx] = gain;
                            }
                        }
                        noteTime += note.Beats * secPerBeat;
                    }
                }
                if (durations.TryGetValue(patternName, out double patternDuration)) time += patternDuration;
            }
            return duck;
        }

        static void RenderVoiceGroup(Ctx ctx, MusicTrack track, float frequency, float previous,
            float level, double startSec, double slotSec)
        {
            var adsr = track.Adsr ?? Legacy(track);
            float gate = track.Gate >= 0f ? track.Gate : DefaultGate;
            double hold = slotSec * gate;
            int count = (int)((hold + adsr.Release) * ctx.Rate);
            if (count <= 0) return;

            int start = (int)Math.Round(startSec * ctx.Rate);
            if (!ctx.Wrap) count = Math.Min(count, ctx.Frames - start);
            if (count <= 0) return;

            ctx.EnsureScratch(count);
            var left = ctx.ScratchL;
            var right = ctx.ScratchR;
            Array.Clear(left, 0, count);
            Array.Clear(right, 0, count);

            int voices = Math.Max(1, track.Voices);
            float voiceLevel = 1f / (float)Math.Sqrt(voices);
            bool glide = track.Glide > 0f && previous > 0f;
            int wave = WaveId(track.Wave);
            float pulseWidth = track.PulseWidth;

            for (int v = 0; v < voices; v++)
            {
                float spot = voices > 1 ? v / (float)(voices - 1) : 0.5f;
                float cents = voices > 1 ? (spot - 0.5f) * track.Spread : 0f;
                float pan = Clamp(track.Pan + (voices > 1 ? (spot * 2f - 1f) * track.Width : 0f), -1f, 1f);
                float angle = (pan + 1f) * (float)Math.PI * 0.25f;
                float gainL = (float)Math.Cos(angle) * voiceLevel;
                float gainR = (float)Math.Sin(angle) * voiceLevel;

                double detune = Math.Pow(2.0, cents / 1200.0);
                double phase = (v * 0.1373) % 1.0;

                for (int i = 0; i < count; i++)
                {
                    double t = i / (double)ctx.Rate;
                    double hz = frequency;
                    if (glide && t < track.Glide)
                    {
                        double k = t / track.Glide;
                        hz = previous * Math.Pow(frequency / previous, k);
                    }
                    float s = Shape(wave, pulseWidth, phase);
                    left[i] += s * gainL;
                    right[i] += s * gainR;
                    phase += hz * detune / ctx.Rate;
                    if (phase >= 1.0) phase -= 1.0;
                }
            }

            if (track.Filter != null) ApplyNoteFilter(ctx, track.Filter, frequency, left, right, count);

            float duckAmount = track.Duck;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)ctx.Rate;
                float env = Envelope(adsr, t, (float)hold);
                if (env <= 0f) continue;

                int idx = Index(start + i, ctx.Frames, ctx.Wrap);
                if (idx < 0) break;

                float duck = duckAmount > 0f ? 1f - duckAmount * (1f - ctx.Duck[idx]) : 1f;
                float gain = env * level * duck;
                float outL = left[i] * gain;
                float outR = right[i] * gain;

                ctx.Dry[idx * 2] += outL;
                ctx.Dry[idx * 2 + 1] += outR;
                float mono = (outL + outR) * 0.5f;
                if (track.DelaySend > 0f) ctx.DelaySend[idx] += mono * track.DelaySend;
                if (track.ReverbSend > 0f) ctx.ReverbSend[idx] += mono * track.ReverbSend;
            }
        }

        static void ApplyNoteFilter(Ctx ctx, MusicFilter filter, float frequency,
            float[] left, float[] right, int count)
        {
            float baseHz = filter.Cutoff;
            if (filter.Keytrack > 0f) baseHz *= (float)Math.Pow(frequency / RefHz, filter.Keytrack);
            float decay = Math.Max(filter.Decay, 0.001f);
            float k = 2f - 1.98f * Clamp(filter.Resonance, 0f, 1f);
            float nyquist = ctx.Rate * 0.45f;

            double l1 = 0, l2 = 0, r1 = 0, r2 = 0;
            float a1 = 0, a2 = 0, a3 = 0;

            for (int i = 0; i < count; i++)
            {
                if (i % CoeffInterval == 0)
                {
                    float t = i / (float)ctx.Rate;
                    float cutoff = Clamp(baseHz + filter.Env * (float)Math.Exp(-t / decay), 30f, nyquist);
                    float g = (float)Math.Tan(Math.PI * cutoff / ctx.Rate);
                    a1 = 1f / (1f + g * (g + k));
                    a2 = g * a1;
                    a3 = g * a2;
                }

                double v3 = left[i] - l2;
                double v1 = a1 * l1 + a2 * v3;
                double v2 = l2 + a2 * l1 + a3 * v3;
                l1 = 2 * v1 - l1;
                l2 = 2 * v2 - l2;
                left[i] = (float)v2;

                v3 = right[i] - r2;
                v1 = a1 * r1 + a2 * v3;
                v2 = r2 + a2 * r1 + a3 * v3;
                r1 = 2 * v1 - r1;
                r2 = 2 * v2 - r2;
                right[i] = (float)v2;
            }
        }

        static void RenderDrum(Ctx ctx, MusicTrack track, float level, double startSec)
        {
            switch (track.Drum)
            {
                case "kick": RenderKick(ctx, track, level, startSec); break;
                case "snare": RenderSnare(ctx, track, level, startSec); break;
                case "clap": RenderClap(ctx, track, level, startSec); break;
                default: RenderHat(ctx, track, level, startSec); break;
            }
        }

        static void RenderKick(Ctx ctx, MusicTrack track, float level, double startSec)
        {
            float tune = track.Tune >= 0f ? track.Tune : 128f;
            float drop = track.Drop >= 0f ? track.Drop : 47f;
            float decay = track.Decay >= 0f ? track.Decay : 0.30f;
            float click = track.Click >= 0f ? track.Click : 0.35f;

            int start = (int)Math.Round(startSec * ctx.Rate);
            int count = (int)((decay * 3f + 0.02f) * ctx.Rate);
            PanGains(track.Pan, UnityCentre, out float gainL, out float gainR);
            double phase = 0;

            for (int i = 0; i < count; i++)
            {
                int idx = Index(start + i, ctx.Frames, ctx.Wrap);
                if (idx < 0) break;

                float t = i / (float)ctx.Rate;
                float hz = drop + (tune - drop) * (float)Math.Exp(-t / 0.030f);
                float amp = (float)Math.Exp(-t / decay) * (1f - (float)Math.Exp(-t / 0.0015f));
                float s = (float)Math.Sin(phase * 2.0 * Math.PI);
                if (t < 0.006f) s += click * ctx.Noise[i % ctx.Noise.Length] * (float)Math.Exp(-t / 0.0012f);
                s = Drive(s * 1.5f);

                float outSample = s * amp * level;
                ctx.Dry[idx * 2] += outSample * gainL;
                ctx.Dry[idx * 2 + 1] += outSample * gainR;
                if (track.ReverbSend > 0f) ctx.ReverbSend[idx] += outSample * track.ReverbSend;

                phase += hz / (double)ctx.Rate;
                if (phase >= 1.0) phase -= 1.0;
            }
        }

        static void RenderSnare(Ctx ctx, MusicTrack track, float level, double startSec)
        {
            float tune = track.Tune >= 0f ? track.Tune : 182f;
            float decay = track.Decay >= 0f ? track.Decay : 0.17f;
            float noiseMix = track.NoiseMix >= 0f ? track.NoiseMix : 0.72f;
            float hold = track.GateHold;
            float tail = track.GateAmount > 0f ? hold + 0.012f : 0f;

            int start = (int)Math.Round(startSec * ctx.Rate);
            int count = (int)((Math.Max(decay * 3f, tail) + 0.02f) * ctx.Rate);
            int offset = (int)(startSec * 137.0) % ctx.Noise.Length;
            PanGains(track.Pan, UnityCentre, out float gainL, out float gainR);
            double p1 = 0, p2 = 0;
            float hp = 0, prev = 0;

            for (int i = 0; i < count; i++)
            {
                int idx = Index(start + i, ctx.Frames, ctx.Wrap);
                if (idx < 0) break;

                float t = i / (float)ctx.Rate;
                float raw = ctx.Noise[(offset + i) % ctx.Noise.Length];
                hp = 0.82f * (hp + raw - prev);
                prev = raw;

                float tone = 0.5f * ((float)Math.Sin(p1 * 2.0 * Math.PI) + (float)Math.Sin(p2 * 2.0 * Math.PI))
                             * (float)Math.Exp(-t / 0.085f);
                float body = hp * (float)Math.Exp(-t / decay) * noiseMix + tone * (1f - noiseMix);

                if (track.GateAmount > 0f && t < tail)
                {
                    float gate = t < hold ? 1f : 1f - (t - hold) / 0.012f;
                    body += hp * 0.55f * track.GateAmount * gate * (1f - 0.35f * t / Math.Max(hold, 0.001f));
                }

                float outSample = Drive(body * 1.2f) * level;
                ctx.Dry[idx * 2] += outSample * gainL;
                ctx.Dry[idx * 2 + 1] += outSample * gainR;
                if (track.ReverbSend > 0f) ctx.ReverbSend[idx] += outSample * track.ReverbSend;
                if (track.DelaySend > 0f) ctx.DelaySend[idx] += outSample * track.DelaySend;

                p1 += tune / (double)ctx.Rate;
                p2 += tune * 1.42 / (double)ctx.Rate;
                if (p1 >= 1.0) p1 -= 1.0;
                if (p2 >= 1.0) p2 -= 1.0;
            }
        }

        static void RenderHat(Ctx ctx, MusicTrack track, float level, double startSec)
        {
            float decay = track.Decay >= 0f ? track.Decay : 0.045f;
            int start = (int)Math.Round(startSec * ctx.Rate);
            int count = (int)(decay * 4f * ctx.Rate);
            int offset = (int)(startSec * 311.0) % ctx.Noise.Length;

            PanGains(track.Pan, HalfPowerCentre, out float gainL, out float gainR);
            float hp = 0, prev = 0;

            for (int i = 0; i < count; i++)
            {
                int idx = Index(start + i, ctx.Frames, ctx.Wrap);
                if (idx < 0) break;

                float t = i / (float)ctx.Rate;
                float raw = ctx.Noise[(offset + i) % ctx.Noise.Length];
                hp = 0.93f * (hp + raw - prev);
                prev = raw;

                float outSample = hp * (float)Math.Exp(-t / decay) * level;
                ctx.Dry[idx * 2] += outSample * gainL;
                ctx.Dry[idx * 2 + 1] += outSample * gainR;
                if (track.ReverbSend > 0f) ctx.ReverbSend[idx] += outSample * track.ReverbSend;
            }
        }

        static void RenderClap(Ctx ctx, MusicTrack track, float level, double startSec)
        {
            float decay = track.Decay >= 0f ? track.Decay : 0.13f;
            int start = (int)Math.Round(startSec * ctx.Rate);
            int count = (int)(decay * 4f * ctx.Rate);
            int offset = (int)(startSec * 523.0) % ctx.Noise.Length;
            PanGains(track.Pan, UnityCentre, out float gainL, out float gainR);
            float hp = 0, prev = 0;

            for (int i = 0; i < count; i++)
            {
                int idx = Index(start + i, ctx.Frames, ctx.Wrap);
                if (idx < 0) break;

                float t = i / (float)ctx.Rate;
                float raw = ctx.Noise[(offset + i) % ctx.Noise.Length];
                hp = 0.88f * (hp + raw - prev);
                prev = raw;

                float burst =
                    t < 0.010f ? 1f :
                    t < 0.020f ? 0.85f :
                    t < 0.030f ? 0.7f : (float)Math.Exp(-(t - 0.030f) / decay);

                float outSample = hp * burst * level;
                ctx.Dry[idx * 2] += outSample * gainL;
                ctx.Dry[idx * 2 + 1] += outSample * gainR;
                if (track.ReverbSend > 0f) ctx.ReverbSend[idx] += outSample * track.ReverbSend;
            }
        }

        static void ApplyDelay(Ctx ctx, MusicDelay delay, double secPerBeat)
        {
            int size = Math.Max(1, (int)Math.Round(delay.Beats * secPerBeat * ctx.Rate));
            var lineL = new float[size];
            var lineR = new float[size];
            float feedback = Clamp(delay.Feedback, 0f, 0.95f);
            float damp = (float)Math.Exp(-2.0 * Math.PI * delay.Damp / ctx.Rate);
            int passes = ctx.Wrap ? 2 : 1;

            for (int pass = 0; pass < passes; pass++)
            {
                float lpL = 0, lpR = 0;
                int p = 0;
                bool write = pass == passes - 1;
                for (int i = 0; i < ctx.Frames; i++)
                {
                    float readL = lineL[p];
                    float readR = lineR[p];
                    lpL = readR + damp * (lpL - readR);
                    lpR = readL + damp * (lpR - readL);

                    float input = ctx.DelaySend[i];
                    lineL[p] = input + (delay.PingPong ? lpL : lpR) * feedback;
                    lineR[p] = delay.PingPong ? readL : input + lpR * feedback;

                    if (write)
                    {
                        ctx.Dry[i * 2] += readL * delay.Mix;
                        ctx.Dry[i * 2 + 1] += readR * delay.Mix;
                    }
                    if (++p >= size) p = 0;
                }
            }
        }

        static readonly int[] CombTuning = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        static readonly int[] AllpassTuning = { 556, 441, 341, 225 };
        const int StereoSpread = 23;

        static void ApplyReverb(Ctx ctx, MusicReverb reverb)
        {
            float scale = ctx.Rate / 44100f;
            float room = 0.70f + Clamp(reverb.Size, 0f, 1f) * 0.28f;
            float damp = Clamp(reverb.Damp, 0f, 1f) * 0.4f;

            var combs = new float[2][][];
            var combStore = new float[2][];
            var allpass = new float[2][][];
            var allpassPos = new int[2][];
            var combPos = new int[2][];

            for (int c = 0; c < 2; c++)
            {
                combs[c] = new float[CombTuning.Length][];
                combPos[c] = new int[CombTuning.Length];
                combStore[c] = new float[CombTuning.Length];
                for (int i = 0; i < CombTuning.Length; i++)
                    combs[c][i] = new float[(int)((CombTuning[i] + (c == 1 ? StereoSpread : 0)) * scale)];

                allpass[c] = new float[AllpassTuning.Length][];
                allpassPos[c] = new int[AllpassTuning.Length];
                for (int i = 0; i < AllpassTuning.Length; i++)
                    allpass[c][i] = new float[(int)((AllpassTuning[i] + (c == 1 ? StereoSpread : 0)) * scale)];
            }

            int passes = ctx.Wrap ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                bool write = pass == passes - 1;
                for (int i = 0; i < ctx.Frames; i++)
                {
                    float input = ctx.ReverbSend[i] * 0.30f;
                    for (int c = 0; c < 2; c++)
                    {
                        float sum = 0;
                        for (int n = 0; n < CombTuning.Length; n++)
                        {
                            var line = combs[c][n];
                            int p = combPos[c][n];
                            float value = line[p];
                            sum += value;
                            combStore[c][n] = value * (1f - damp) + combStore[c][n] * damp;
                            line[p] = input + combStore[c][n] * room;
                            combPos[c][n] = p + 1 >= line.Length ? 0 : p + 1;
                        }

                        for (int n = 0; n < AllpassTuning.Length; n++)
                        {
                            var line = allpass[c][n];
                            int p = allpassPos[c][n];
                            float value = line[p];
                            line[p] = sum + value * 0.5f;
                            sum = value - sum;
                            allpassPos[c][n] = p + 1 >= line.Length ? 0 : p + 1;
                        }

                        if (write) ctx.Dry[i * 2 + c] += sum * reverb.Mix;
                    }
                }
            }

            if (reverb.Width < 1f)
            {
                float side = Clamp(reverb.Width, 0f, 1f);
                for (int i = 0; i < ctx.Frames; i++)
                {
                    float l = ctx.Dry[i * 2];
                    float r = ctx.Dry[i * 2 + 1];
                    float mid = (l + r) * 0.5f;
                    ctx.Dry[i * 2] = mid + (l - mid) * side;
                    ctx.Dry[i * 2 + 1] = mid + (r - mid) * side;
                }
            }
        }

        static float Envelope(MusicAdsr adsr, float t, float hold)
        {
            float attack = Math.Max(adsr.Attack, 0.0005f);
            float decay = Math.Max(adsr.Decay, 0.0005f);
            float release = Math.Max(adsr.Release, 0.0005f);

            if (t < hold)
            {
                if (t < attack) return t / attack;
                if (t < attack + decay) return 1f + (adsr.Sustain - 1f) * (t - attack) / decay;
                return adsr.Sustain;
            }

            float atHold =
                hold < attack ? hold / attack :
                hold < attack + decay ? 1f + (adsr.Sustain - 1f) * (hold - attack) / decay :
                adsr.Sustain;

            float k = (t - hold) / release;
            return k >= 1f ? 0f : atHold * (1f - k);
        }

        static MusicAdsr Legacy(MusicTrack track) => new MusicAdsr
        {
            Attack = track.Attack >= 0f ? track.Attack : 0.01f,
            Decay = 0.1f,
            Sustain = 1f,
            Release = track.Release >= 0f ? track.Release : 0.08f,
        };

        static int WaveId(string wave)
        {
            switch (wave)
            {
                case "square": return 1;
                case "pulse": return 2;
                case "triangle": return 3;
                case "sawtooth": return 4;
                default: return 0;
            }
        }

        static float Shape(int wave, float pulseWidth, double phase)
        {
            switch (wave)
            {
                case 1: return phase < 0.5 ? 1f : -1f;
                case 2: return phase < pulseWidth ? 1f : -1f;
                case 3: return phase < 0.5 ? (float)(4.0 * phase - 1.0) : (float)(3.0 - 4.0 * phase);
                case 4: return (float)(2.0 * phase - 1.0);
                default: return (float)Math.Sin(phase * 2.0 * Math.PI);
            }
        }

        const double UnityCentre = 1.4142135623730951;
        const double HalfPowerCentre = 1.0;

        static void PanGains(float pan, double scale, out float left, out float right)
        {
            double angle = (Clamp(pan, -1f, 1f) + 1f) * Math.PI * 0.25;
            left = (float)(Math.Cos(angle) * scale);
            right = (float)(Math.Sin(angle) * scale);
        }

        static int Index(int position, int frames, bool wrap)
        {
            if (position < 0) return -1;
            if (position < frames) return position;
            if (!wrap) return -1;
            return position % frames;
        }

        static float Drive(float x) => (float)Math.Tanh(x) * 0.85f;

        const float ClipKnee = 0.7f;

        static float SoftClip(float x)
        {
            float magnitude = x < 0f ? -x : x;
            if (magnitude <= ClipKnee) return x;
            float sign = x < 0f ? -1f : 1f;
            return sign * (ClipKnee + (1f - ClipKnee) * (float)Math.Tanh((magnitude - ClipKnee) / (1f - ClipKnee)));
        }

        static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;

        static float[] BuildNoise(int rate)
        {
            var random = new Random(NoiseSeed);
            var noise = new float[rate];
            for (int i = 0; i < noise.Length; i++) noise[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return noise;
        }
    }
}
