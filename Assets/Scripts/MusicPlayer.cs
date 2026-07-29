using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetalRaptors
{
    /// <summary>Persistent soundtrack player driving the menu music and fades. See docs/music.md.</summary>
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        public const string MenuThemeId = "flak-parade";
        const float MusicVolume = 0.45f;
        const float FadeInSec = 1.5f;
        const float FadeOutSec = 0.8f;
        const double ScheduleDelaySec = 0.1;

        // Start-latency budget (docs/music.md): the intro plays as soon as it is baked, held
        // back only long enough that the still-rendering loop is predicted to land before the
        // intro's musical end.
        const float LoopBakeSafety = 1.3f;    // margin on the predicted loop bake
        const float HandoffMarginSec = 0.35f; // loop clip must exist this long before it is due
        const float MaxStartWaitSec = 2.5f;   // never hold the intro back longer than this
        const double LateLoopDelaySec = 0.05; // the loop missed its slot: in as soon as possible

        static readonly Dictionary<string, RenderedMusic> RenderCache = new Dictionary<string, RenderedMusic>();

        /// <summary>One track being rendered: intro and loop on separate worker threads.</summary>
        class BakeJob
        {
            public string Id;
            public MusicConfig Config;
            public float Fade;
            public bool Wanted;          // a prewarm bakes into the cache without playing
            public Task<MusicBake> Intro;
            public Task<MusicBake> Loop;
            public float StartedAt;
            public double IntroSec, LoopSec;
            public AudioClip IntroClip;
            public double IntroDuration;
            public double BakeSecPerAudioSec;
            public bool IntroBaked, IntroStarted, LoopDone;
            public double LoopDueDsp;
        }

        AudioSource _introSource;
        AudioSource _loopSource;
        string _currentId;
        float _volume;
        float _volumeTarget;
        float _fadeSec = 1f;
        bool _stopWhenSilent;
        double _fadeAfterDsp;   // the ramp waits for scheduled playback, so it is never spent on silence

        BakeJob _job;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
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
            _introSource = CreateSource(false);
            _loopSource = CreateSource(true);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Bootstrap runs before the first scene loads, so the bake overlaps the menu's
            // scene load and UI construction instead of starting after them.
            Prewarm(MenuThemeId);
        }

        void Start()
        {
            if (_currentId == null && SceneManager.GetActiveScene().name == SceneNames.MainMenu)
                Play(MenuThemeId, FadeInSec);
        }

        void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Update()
        {
            PollBake();
            if (_currentId == null) return;
            if (!_stopWhenSilent && AudioSettings.dspTime < _fadeAfterDsp) return;

            _volume = Mathf.MoveTowards(_volume, _volumeTarget,
                MusicVolume * Time.unscaledDeltaTime / Mathf.Max(_fadeSec, 0.01f));
            _introSource.volume = _volume;
            _loopSource.volume = _volume;

            if (_stopWhenSilent && _volume <= 0f) Stop();
        }

        public void Play(string id, float fadeSec = FadeInSec)
        {
            if (_currentId == id && !_stopWhenSilent) return;
            Stop();

            if (RenderCache.TryGetValue(id, out var rendered))
            {
                Begin(id, rendered, fadeSec);
                return;
            }

            if (_job != null && _job.Id == id)
            {
                _job.Wanted = true;
                _job.Fade = fadeSec;
                return;
            }

            StartJob(id, fadeSec, wanted: true);
        }

        /// <summary>Bakes a track into the cache without playing it, so a later Play is instant.</summary>
        public void Prewarm(string id)
        {
            if (RenderCache.ContainsKey(id) || (_job != null && _job.Id == id)) return;
            StartJob(id, FadeInSec, wanted: false);
        }

        public void FadeOutAndStop(float fadeSec = FadeOutSec)
        {
            if (_job != null) _job.Wanted = false;
            if (_currentId == null || _stopWhenSilent) return;
            _volumeTarget = 0f;
            _fadeSec = fadeSec;
            _stopWhenSilent = true;
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
            _job = job;
        }

        void PollBake()
        {
            var job = _job;
            if (job == null) return;

            if (!job.IntroBaked && job.Intro != null && job.Intro.IsCompleted) TakeIntro(job);
            if (job.Wanted && job.IntroBaked && !job.IntroStarted && job.IntroClip != null) StartIntro(job);

            if (!job.LoopDone && job.Loop.IsCompleted) TakeLoop(job);
            if (job.LoopDone && (job.Intro == null || job.IntroBaked)) _job = null;
        }

        /// <summary>The intro buffer is ready: clip it, and time the loop bake from its cost.</summary>
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

            // Both halves render the same way, so the intro's measured cost per second of
            // audio predicts the loop's — on this machine, at this load.
            double rendered = job.IntroSec + (job.Config.IsRetro ? MusicSynthRetro.TailSec : 0.0);
            job.BakeSecPerAudioSec = (Time.realtimeSinceStartup - job.StartedAt) / Mathf.Max((float)rendered, 0.05f);
        }

        /// <summary>
        /// Starts the intro, delayed only by however long the loop still needs: the loop enters
        /// on the intro's last beat, so holding the intro back is what keeps that handoff clean.
        /// </summary>
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
            _volumeTarget = MusicVolume;
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
                // The intro is already playing (or scheduled): slot the loop onto its end. If
                // the bake overran that moment the loop enters late, under the intro's tail.
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
            _volumeTarget = MusicVolume;
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
            if (_job != null)
            {
                _job.Wanted = false;
                _job.IntroStarted = false; // a later Play re-schedules from the top
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
            if (scene.name == SceneNames.MainMenu) Play(MenuThemeId, FadeInSec);
            else FadeOutAndStop(FadeOutSec);
        }
    }
}
