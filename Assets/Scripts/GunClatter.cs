using UnityEngine;

namespace MetalRaptors
{
    public static class GunClatter
    {
        const int Rate = 44100;
        const int Seed = 1917;
        const float Gain = 0.55f;

        const float ClickHz = 2600f;
        const float ClickToneGain = 0.6f;
        const float ClickDecay = 90f;
        const float ClickNoiseDecay = 140f;

        const float RackSeconds = 0.1f;
        const float RackSecondAt = 0.045f;
        const float RackSecondGain = 0.6f;

        const float JamSeconds = 0.24f;
        const float ThudHz = 140f;
        const float ThudDecay = 28f;
        const float ThudNoiseDecay = 55f;
        const float ThudNoiseGain = 0.45f;

        const float ClearSeconds = 0.2f;
        const float SnapAt = 0.06f;
        const float SnapHz = 900f;
        const float SnapDecay = 40f;

        static AudioClip _rack;
        static AudioClip _jam;
        static AudioClip _clear;

        public static AudioClip Rack
        {
            get
            {
                if (_rack == null) _rack = Build("GunRack", RackSeconds, RackSample);
                return _rack;
            }
        }

        public static AudioClip Jam
        {
            get
            {
                if (_jam == null) _jam = Build("GunJam", JamSeconds, JamSample);
                return _jam;
            }
        }

        public static AudioClip Clear
        {
            get
            {
                if (_clear == null) _clear = Build("GunClear", ClearSeconds, ClearSample);
                return _clear;
            }
        }

        static AudioClip Build(string name, float seconds, System.Func<float, float, float> sample)
        {
            var rng = new System.Random(Seed);
            int frames = Mathf.RoundToInt(Rate * seconds);
            var data = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                data[i] = sample(i / (float)Rate, noise) * Gain;
            }

            var clip = AudioClip.Create(name, frames, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Click(float t, float noise)
        {
            if (t < 0f) return 0f;

            return Mathf.Sin(2f * Mathf.PI * ClickHz * t) * Mathf.Exp(-t * ClickDecay) * ClickToneGain
                   + noise * Mathf.Exp(-t * ClickNoiseDecay);
        }

        static float RackSample(float t, float noise) =>
            Click(t, noise) + Click(t - RackSecondAt, noise) * RackSecondGain;

        static float JamSample(float t, float noise) =>
            Mathf.Sin(2f * Mathf.PI * ThudHz * t) * Mathf.Exp(-t * ThudDecay)
            + noise * Mathf.Exp(-t * ThudNoiseDecay) * ThudNoiseGain;

        static float ClearSample(float t, float noise)
        {
            float late = t - SnapAt;
            float snap = late < 0f
                ? 0f
                : Mathf.Sin(2f * Mathf.PI * SnapHz * late) * Mathf.Exp(-late * SnapDecay);
            return Click(t, noise) + snap;
        }
    }
}
