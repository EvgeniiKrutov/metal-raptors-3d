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

        const float LoopBakeSafety = 1.3f;
        const float HandoffMarginSec = 0.35f;
        const float MaxStartWaitSec = 2.5f;
        const double LateLoopDelaySec = 0.05;

        static readonly Dictionary<string, RenderedMusic> RenderCache = new Dictionary<string, RenderedMusic>();

        class BakeJob
        {
            public string Id;
            public MusicConfig Config;
            public float Fade;
            public bool Wanted;
            public Task<MusicBake> Intro;
            public Task<MusicBake> Loop;
            public float StartedAt;
            public double IntroSec, LoopSec;
            public AudioClip IntroClip;
            public double IntroDuration;
            public double BakeSecPerAudioSec;
            public bool IntroBaked, IntroStarted, LoopDone, Dropped;
            public double LoopDueDsp;
        }

        AudioSource _introSource;
        AudioSource _loopSource;
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
            _introSource = CreateSource(false);
            _loopSource = CreateSource(true);
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
            _introSource.volume = _volume;
            _loopSource.volume = _volume;

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
            if (_loopSource.clip != null) return true;
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
                Discard(rendered.Intro);
                Discard(rendered.Loop);
            }
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
            var job = new BakeJob
            {
                Id = id,
                Config = config,
                Fade = fadeSec,
                Wanted = wanted,
                StartedAt = Time.realtimeSinceStartup,
                IntroSec = MusicSynth.SectionSeconds(config, 0, loopStart),
                LoopSec = MusicSynth.SectionSeconds(config, loopStart, config.Sequence.Count),
            };

            if (loopStart > 0)
                job.Intro = Task.Run(() => MusicSynth.BakeSection(config, rate, intro: true));
            job.Loop = Task.Run(() => MusicSynth.BakeSection(config, rate, intro: false));
            _jobs.Add(job);
        }

        void PollBake()
        {
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                BakeJob job = _jobs[i];

                if (!job.IntroBaked && job.Intro != null && job.Intro.IsCompleted) TakeIntro(job);
                if (job.Wanted && job.IntroBaked && !job.IntroStarted && job.IntroClip != null) StartIntro(job);

                if (!job.LoopDone && job.Loop.IsCompleted) TakeLoop(job);
                if (!job.LoopDone || (job.Intro != null && !job.IntroBaked)) continue;

                if (job.Dropped) Discard(job.IntroClip);
                _jobs.RemoveAt(i);
            }
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

            job.IntroClip = MusicSynth.ToClip(bake, intro: true, $"{job.Id}-intro");
            job.IntroDuration = bake.IntroDuration;

            double rendered = job.IntroSec + (job.Config.IsRetro ? MusicSynthRetro.TailSec : 0.0);
            job.BakeSecPerAudioSec = (Time.realtimeSinceStartup - job.StartedAt) / Mathf.Max((float)rendered, 0.05f);
        }

        void StartIntro(BakeJob job)
        {
            job.IntroStarted = true;

            float elapsed = Time.realtimeSinceStartup - job.StartedAt;
            double loopReadyIn = job.LoopDone
                ? 0.0
                : job.BakeSecPerAudioSec * job.LoopSec * LoopBakeSafety - elapsed;
            double wait = Mathf.Clamp((float)(loopReadyIn + HandoffMarginSec - job.IntroSec),
                (float)ScheduleDelaySec, MaxStartWaitSec);

            double start = AudioSettings.dspTime + wait;
            _introSource.clip = job.IntroClip;
            _introSource.PlayScheduled(start);
            job.LoopDueDsp = start + job.IntroDuration;

            _currentId = job.Id;
            _volume = 0f;
            _volumeTarget = MusicTarget;
            _fadeSec = job.Fade;
            _fadeAfterDsp = start;
        }

        void TakeLoop(BakeJob job)
        {
            job.LoopDone = true;
            var task = job.Loop;

            if (task.IsFaulted)
            {
                Debug.LogError($"Music '{job.Id}' failed to bake: {task.Exception?.GetBaseException().Message}");
                return;
            }

            var bake = task.Result;
            var loopClip = MusicSynth.ToClip(bake, intro: false, $"{job.Id}-loop");
            if (loopClip == null) return;

            if (!job.Wanted && !Keeps(job.Id))
            {
                job.Dropped = true;
                Discard(loopClip);
                return;
            }

            var rendered = new RenderedMusic
            {
                Intro = job.IntroClip,
                Loop = loopClip,
                IntroDuration = job.IntroDuration,
            };
            RenderCache[job.Id] = rendered;

            if (!job.Wanted) return;

            if (job.IntroStarted)
            {
                _loopSource.clip = loopClip;
                _loopSource.PlayScheduled(System.Math.Max(job.LoopDueDsp,
                    AudioSettings.dspTime + LateLoopDelaySec));
            }
            else
            {
                Begin(job.Id, rendered, job.Fade);
            }
        }

        void Begin(string id, RenderedMusic rendered, float fadeSec)
        {
            double start = AudioSettings.dspTime + ScheduleDelaySec;
            if (rendered.Intro != null)
            {
                _introSource.clip = rendered.Intro;
                _introSource.PlayScheduled(start);
                _loopSource.clip = rendered.Loop;
                _loopSource.PlayScheduled(start + rendered.IntroDuration);
            }
            else
            {
                _loopSource.clip = rendered.Loop;
                _loopSource.PlayScheduled(start);
            }

            _currentId = id;
            _volume = 0f;
            _volumeTarget = MusicTarget;
            _fadeSec = fadeSec;
            _fadeAfterDsp = start;
        }

        void Stop()
        {
            _introSource.Stop();
            _loopSource.Stop();
            _introSource.clip = null;
            _loopSource.clip = null;
            _currentId = null;
            _volume = 0f;
            _volumeTarget = 0f;
            _stopWhenSilent = false;
            _fadeAfterDsp = 0;
            foreach (BakeJob job in _jobs)
            {
                job.Wanted = false;
                job.IntroStarted = false;
            }
        }

        AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
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
