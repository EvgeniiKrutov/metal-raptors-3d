using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetalRaptors
{
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        const float MusicVolume = 0.45f;
        const float FadeInSec = 1.5f;
        public const float FadeOutSec = 0.8f;
        const float SwitchFadeSec = 0.3f;
        const float BrowseFadeInSec = 0.5f;
        const double ScheduleDelaySec = 0.1;
        const double LateSec = 0.02;
        const int LoopParts = 3;
        const float BakeSafety = 1.5f;
        const double StartMarginSec = 1.0;

        static readonly Dictionary<string, RenderedMusic> RenderCache = new Dictionary<string, RenderedMusic>();

        class BakeJob
        {
            public string Id;
            public float Fade;
            public bool Wanted;
            public bool Started;
            public float StartedAt;
            public Task<MusicBake> Intro;
            public Task<MusicBake>[] Parts;
            public double[] PartSec;
            public bool[] PartTaken;
            public bool IntroBaked;
            public double BakeSecPerAudioSec = -1;
            public RenderedMusic Music;
        }

        AudioSource _introSource;
        readonly AudioSource[] _partSources = new AudioSource[2];
        AudioClip[] _parts;
        int _nextPart;
        int _slot;
        double _nextAt;
        double _queuedAt;
        string _currentId;
        bool _menuScene = true;
        float _volume;
        float _volumeTarget;
        float _fadeSec = 1f;
        bool _stopWhenSilent;
        double _fadeAfterDsp;

        string _pendingId;
        float _pendingFade;
        float _pendingAt;

        readonly List<BakeJob> _jobs = new List<BakeJob>();
        readonly HashSet<string> _retain = new HashSet<string>();
        readonly HashSet<string> _pinned = new HashSet<string>();
        readonly List<string> _prewarmQueue = new List<string>();
        readonly List<string> _trash = new List<string>();

        static float MusicTarget => MusicVolume * AudioOptions.Music;

        public static bool IsAudible(string id) => Instance != null && Instance.Audible(id);

        public static float LevelPresence => Instance != null ? Instance.Presence() : 0f;

        static bool HasMenuMusic(string scene) =>
            scene == SceneNames.MainMenu || scene == SceneNames.Garage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;

            AudioOutput.EnsureStereo();
            var go = new GameObject("MusicPlayer");
            go.AddComponent<MusicPlayer>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            AudioOptions.Changed += OnVolumeChanged;
            _introSource = CreateSource();
            _partSources[0] = CreateSource();
            _partSources[1] = CreateSource();
            SceneManager.sceneLoaded += OnSceneLoaded;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            Prewarm(MusicTracks.Selected);
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) AudioOutput.EnsureStereo();
        }

        void OnAudioConfigurationChanged(bool deviceChanged)
        {
            if (_currentId == null || _stopWhenSilent) return;

            string id = _currentId;
            _currentId = null;
            Play(id, FadeInSec);
        }

        void Start()
        {
            if (_currentId == null && HasMenuMusic(SceneManager.GetActiveScene().name))
                Play(MusicTracks.Selected, FadeInSec);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            AudioOptions.Changed -= OnVolumeChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        void Update()
        {
            PollBake();
            Advance();
            Fade();
            LaunchPending();
            PrewarmNext();
        }

        void Fade()
        {
            if (_currentId == null) return;
            if (!_stopWhenSilent && AudioSettings.dspTime < _fadeAfterDsp) return;

            _volume = Mathf.MoveTowards(_volume, _volumeTarget,
                MusicVolume * Time.unscaledDeltaTime / Mathf.Max(_fadeSec, 0.01f));
            float level = _volume * CutsceneMix.Level;
            _introSource.volume = level;
            _partSources[0].volume = level;
            _partSources[1].volume = level;

            if (_stopWhenSilent && _volume <= 0f) Stop();
        }

        void LaunchPending()
        {
            if (_pendingId == null || _currentId != null) return;
            if (Time.realtimeSinceStartup < _pendingAt) return;

            string id = _pendingId;
            _pendingId = null;
            Launch(id, _pendingFade);
            Trim();
        }

        void PrewarmNext()
        {
            if (_jobs.Count > 0 || _pendingId != null) return;

            while (_prewarmQueue.Count > 0)
            {
                string id = _prewarmQueue[0];
                _prewarmQueue.RemoveAt(0);
                if (RenderCache.ContainsKey(id) || !_retain.Contains(id)) continue;

                StartJob(id, FadeInSec, wanted: false);
                return;
            }
        }

        void OnVolumeChanged()
        {
            if (_currentId == null || _stopWhenSilent) return;
            _volumeTarget = MusicTarget;
        }

        public void Play(string id, float fadeSec = FadeInSec)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (_currentId == id && (!_stopWhenSilent || LoopArmed(id)))
            {
                _pendingId = null;
                if (!_stopWhenSilent) return;

                _stopWhenSilent = false;
                _volumeTarget = MusicTarget;
                _fadeSec = fadeSec;
                return;
            }

            BakeJob wanted = WantedJob();
            if (_currentId == null && _pendingId == null && wanted == null)
            {
                Launch(id, fadeSec);
                return;
            }

            if (_currentId == null && wanted != null && wanted.Id == id)
            {
                _pendingId = null;
                wanted.Fade = fadeSec;
                return;
            }

            _pendingId = id;
            _pendingFade = fadeSec;
            _pendingAt = Time.realtimeSinceStartup + SwitchFadeSec;

            if (_currentId != null && _volume > 0f)
            {
                _volumeTarget = 0f;
                _fadeSec = SwitchFadeSec;
                _stopWhenSilent = true;
            }
            else
            {
                Stop();
            }
        }

        public void Browse(string id, params string[] neighbours)
        {
            _retain.Clear();
            _prewarmQueue.Clear();
            _retain.Add(id);

            foreach (string other in neighbours)
            {
                if (string.IsNullOrEmpty(other) || !_retain.Add(other)) continue;
                _prewarmQueue.Add(other);
            }

            Play(id, BrowseFadeInSec);
            Trim();
        }

        public void EndBrowse()
        {
            _retain.Clear();
            _prewarmQueue.Clear();
            Play(MusicTracks.Selected, BrowseFadeInSec);
            Trim();
        }

        public void Prewarm(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            _pinned.Add(id);
            if (RenderCache.ContainsKey(id) || FindJob(id) != null) return;
            StartJob(id, FadeInSec, wanted: false);
        }

        public void FadeOutAndStop(float fadeSec = FadeOutSec)
        {
            _pendingId = null;
            foreach (BakeJob job in _jobs) job.Wanted = false;
            if (_currentId == null || _stopWhenSilent) return;
            _volumeTarget = 0f;
            _fadeSec = fadeSec;
            _stopWhenSilent = true;
        }

        float Presence()
        {
            if (_menuScene || _currentId == null) return 0f;

            float target = MusicTarget;
            return target > 0f ? Mathf.Clamp01(_volume / target) : 0f;
        }

        bool Audible(string id) =>
            _currentId == id && !_stopWhenSilent && AudioSettings.dspTime >= _fadeAfterDsp;

        bool LoopArmed(string id)
        {
            if (_parts != null) return true;
            BakeJob job = FindJob(id);
            return job != null && job.Wanted;
        }

        BakeJob FindJob(string id)
        {
            foreach (BakeJob job in _jobs)
            {
                if (job.Id == id) return job;
            }
            return null;
        }

        BakeJob WantedJob()
        {
            foreach (BakeJob job in _jobs)
            {
                if (job.Wanted) return job;
            }
            return null;
        }

        bool Keeps(string id)
        {
            if (id == MusicTracks.Selected || id == _currentId || id == _pendingId) return true;
            if (_retain.Contains(id) || _pinned.Contains(id)) return true;

            BakeJob wanted = WantedJob();
            return wanted != null && wanted.Id == id;
        }

        void Trim()
        {
            _trash.Clear();
            foreach (var kvp in RenderCache)
            {
                if (!Keeps(kvp.Key)) _trash.Add(kvp.Key);
            }

            foreach (string id in _trash)
            {
                RenderedMusic rendered = RenderCache[id];
                RenderCache.Remove(id);
                Discard(rendered);
            }
        }

        static void Discard(RenderedMusic music)
        {
            Discard(music.Intro);
            foreach (AudioClip part in music.Parts) Discard(part);
        }

        static void Discard(AudioClip clip)
        {
            if (clip != null) Destroy(clip);
        }

        void Launch(string id, float fadeSec)
        {
            AudioOutput.EnsureStereo();
            Stop();

            if (RenderCache.TryGetValue(id, out var rendered))
            {
                Begin(id, rendered, fadeSec);
                return;
            }

            BakeJob job = FindJob(id);
            if (job != null)
            {
                job.Wanted = true;
                job.Fade = fadeSec;
                return;
            }

            StartJob(id, fadeSec, wanted: true);
        }

        void StartJob(string id, float fadeSec, bool wanted)
        {
            var config = MusicLibrary.Load(id);
            if (config == null) return;

            int rate = AudioSettings.outputSampleRate;
            int loopStart = MusicSynth.LoopStartIndex(config);
            int[] bounds = MusicSynth.LoopBounds(config, LoopParts);
            int count = bounds.Length - 1;
            var job = new BakeJob
            {
                Id = id,
                Fade = fadeSec,
                Wanted = wanted,
                StartedAt = Time.realtimeSinceStartup,
                Parts = new Task<MusicBake>[count],
                PartSec = new double[count],
                PartTaken = new bool[count],
                Music = new RenderedMusic { Parts = new AudioClip[count] },
            };

            if (loopStart > 0)
                job.Intro = Task.Run(() => MusicSynth.BakeSection(config, rate, intro: true));
            for (int p = 0; p < count; p++)
            {
                int part = p;
                job.PartSec[p] = MusicSynth.SectionSeconds(config, bounds[p], bounds[p + 1]);
                job.Parts[p] = Task.Run(() => MusicSynth.BakePart(config, rate, bounds, part));
            }
            _jobs.Add(job);
        }

        void PollBake()
        {
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                BakeJob job = _jobs[i];

                if (!job.IntroBaked && job.Intro != null && job.Intro.IsCompleted) TakeIntro(job);
                bool baked = job.Intro == null || job.IntroBaked;
                for (int p = 0; p < job.Parts.Length; p++)
                {
                    if (!job.PartTaken[p] && job.Parts[p].IsCompleted) TakePart(job, p);
                    baked &= job.PartTaken[p];
                }

                if (baked)
                {
                    _jobs.RemoveAt(i);
                    Finish(job);
                }
                else if (job.Wanted && !job.Started && CanStart(job))
                {
                    job.Started = true;
                    Begin(job.Id, job.Music, job.Fade);
                }
            }
        }

        bool CanStart(BakeJob job)
        {
            if (job.Intro != null && !job.IntroBaked) return false;
            if (job.Music.Parts[0] == null || job.BakeSecPerAudioSec < 0) return false;

            double elapsed = Time.realtimeSinceStartup - job.StartedAt;
            double due = ScheduleDelaySec + (job.Music.Intro != null ? job.Music.IntroDuration : 0.0);
            for (int p = 0; p < job.Parts.Length; p++)
            {
                if (!job.PartTaken[p])
                {
                    double left = job.BakeSecPerAudioSec * job.PartSec[p] * BakeSafety - elapsed;
                    if (left + StartMarginSec > due) return false;
                }
                due += job.PartSec[p];
            }
            return true;
        }

        void Finish(BakeJob job)
        {
            RenderedMusic music = job.Music;
            bool failed = System.Array.IndexOf(music.Parts, null) >= 0;
            if (failed || (!job.Wanted && !Keeps(job.Id)))
            {
                if (failed && job.Started) Stop();
                Discard(music);
                return;
            }

            RenderCache[job.Id] = music;
            if (job.Wanted && !job.Started) Begin(job.Id, music, job.Fade);
        }

        void TakeIntro(BakeJob job)
        {
            job.IntroBaked = true;
            var task = job.Intro;

            if (task.IsFaulted)
            {
                Debug.LogError($"Music '{job.Id}' intro failed to bake: {task.Exception?.GetBaseException().Message}");
                return;
            }

            var bake = task.Result;
            if (bake == null) return;

            job.Music.Intro = MusicSynth.ToClip(bake, intro: true, $"{job.Id}-intro");
            job.Music.IntroDuration = bake.IntroDuration;
        }

        void TakePart(BakeJob job, int part)
        {
            job.PartTaken[part] = true;
            var task = job.Parts[part];

            if (task.IsFaulted)
            {
                Debug.LogError($"Music '{job.Id}' failed to bake: {task.Exception?.GetBaseException().Message}");
                return;
            }

            job.Music.Parts[part] = MusicSynth.ToClip(task.Result, intro: false, $"{job.Id}-loop{part}");
            if (part == 0)
                job.BakeSecPerAudioSec = (Time.realtimeSinceStartup - job.StartedAt) /
                                         System.Math.Max(job.PartSec[0], 0.05);
        }

        void Begin(string id, RenderedMusic music, float fadeSec)
        {
            double start = AudioSettings.dspTime + ScheduleDelaySec;
            _fadeAfterDsp = start;
            if (music.Intro != null)
            {
                _introSource.clip = music.Intro;
                _introSource.PlayScheduled(start);
                start += music.IntroDuration;
            }

            _parts = music.Parts;
            _nextPart = 0;
            _nextAt = start;
            _queuedAt = double.MinValue;
            _slot = 1;

            _currentId = id;
            _volume = 0f;
            _volumeTarget = MusicTarget;
            _fadeSec = fadeSec;
            Advance();
        }

        void Advance()
        {
            if (_parts == null) return;

            double now = AudioSettings.dspTime;
            if (now < _queuedAt) return;

            AudioClip clip = _parts[_nextPart];
            if (clip == null) return;

            double at = _nextAt < now + LateSec ? now + ScheduleDelaySec : _nextAt;
            _slot = 1 - _slot;
            _partSources[_slot].clip = clip;
            _partSources[_slot].PlayScheduled(at);
            _queuedAt = at;
            _nextAt = at + (double)clip.samples / clip.frequency;
            _nextPart = (_nextPart + 1) % _parts.Length;
        }

        void Stop()
        {
            _introSource.Stop();
            _introSource.clip = null;
            foreach (AudioSource source in _partSources)
            {
                source.Stop();
                source.clip = null;
            }
            _parts = null;
            _currentId = null;
            _volume = 0f;
            _volumeTarget = 0f;
            _stopWhenSilent = false;
            _fadeAfterDsp = 0;
            foreach (BakeJob job in _jobs)
            {
                job.Wanted = false;
                job.Started = false;
            }
        }

        AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;

            _menuScene = HasMenuMusic(scene.name);
            _retain.Clear();
            _prewarmQueue.Clear();
            if (HasMenuMusic(scene.name))
            {
                _pinned.Clear();
                Play(MusicTracks.Selected, FadeInSec);
            }
            else
            {
                FadeOutAndStop(FadeOutSec);
            }
            Trim();
        }
    }
}
