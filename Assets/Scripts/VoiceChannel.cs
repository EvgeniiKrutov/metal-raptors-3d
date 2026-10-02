using System;
using UnityEngine;

namespace MetalRaptors
{
    public class VoiceChannel : MonoBehaviour
    {
        const float Volume = 0.8f;
        const double ReleaseSec = 0.015;
        const double LeadSec = 0.03;
        const double StartSec = 0.05;
        const double Never = double.MaxValue;

        AudioSource _source;
        float[] _marks;
        double _startDsp;
        double _endDsp = Never;
        int _rate;

        public static VoiceChannel Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            var channel = go.AddComponent<VoiceChannel>();
            channel._source = source;
            channel._rate = AudioSettings.outputSampleRate;
            return channel;
        }

        public void Play(AudioClip clip, float[] marks)
        {
            _marks = marks;
            _endDsp = Never;
            _source.clip = clip;
            _source.volume = Volume * AudioOptions.Voices;
            _startDsp = AudioSettings.dspTime + StartSec;
            _source.PlayScheduled(_startDsp);
        }

        public void Finish()
        {
            if (!_source.isPlaying || _endDsp != Never || _marks == null) return;

            double now = AudioSettings.dspTime - _startDsp;
            foreach (float mark in _marks)
            {
                if (mark - now < ReleaseSec + LeadSec) continue;

                _endDsp = _startDsp + mark;
                return;
            }
        }

        public void Cut()
        {
            if (!_source.isPlaying) return;

            double end = AudioSettings.dspTime + ReleaseSec + LeadSec;
            if (end < _endDsp) _endDsp = end;
        }

        void Update()
        {
            if (_source == null || !_source.isPlaying) return;

            if (CutscenePause.Halted) Cut();

            _source.volume = Volume * AudioOptions.Voices;

            if (_endDsp != Never && AudioSettings.dspTime > _endDsp + LeadSec)
            {
                _source.Stop();
                _endDsp = Never;
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            double end = _endDsp;
            if (end == Never) return;

            double time = AudioSettings.dspTime;
            double step = 1.0 / _rate;
            for (int i = 0; i < data.Length; i += channels)
            {
                float gain = Gain(end - time);
                for (int c = 0; c < channels; c++) data[i + c] *= gain;
                time += step;
            }
        }

        static float Gain(double left)
        {
            if (left >= ReleaseSec) return 1f;
            if (left <= 0.0) return 0f;
            return (float)(0.5 - 0.5 * Math.Cos(Math.PI * left / ReleaseSec));
        }
    }
}
