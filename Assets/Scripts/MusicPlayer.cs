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

        public const string MenuThemeId = "raptor-march";
        const float MusicVolume = 0.45f;
        const float FadeInSec = 1.5f;
        const float FadeOutSec = 0.8f;
        const double ScheduleDelaySec = 0.1;

        static readonly Dictionary<string, RenderedMusic> RenderCache = new Dictionary<string, RenderedMusic>();

        AudioSource _introSource;
        AudioSource _loopSource;
        string _currentId;
        float _volume;
        float _volumeTarget;
        float _fadeSec = 1f;
        bool _stopWhenSilent;

        Task<MusicBake> _bakeTask;
        MusicConfig _bakeConfig;
        string _bakeId;
        string _pendingId;
        float _pendingFade;

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

            _pendingId = id;
            _pendingFade = fadeSec;
            if (_bakeId == id) return;

            var config = MusicLibrary.Load(id);
            if (config == null)
            {
                _pendingId = null;
                return;
            }

            int rate = AudioSettings.outputSampleRate;
            _bakeConfig = config;
            _bakeId = id;
            _bakeTask = Task.Run(() => MusicSynth.Bake(config, rate));
        }

        public void FadeOutAndStop(float fadeSec = FadeOutSec)
        {
            _pendingId = null;
            if (_currentId == null || _stopWhenSilent) return;
            _volumeTarget = 0f;
            _fadeSec = fadeSec;
            _stopWhenSilent = true;
        }

        void PollBake()
        {
            if (_bakeTask == null || !_bakeTask.IsCompleted) return;

            string id = _bakeId;
            var config = _bakeConfig;
            var task = _bakeTask;
            _bakeTask = null;
            _bakeId = null;
            _bakeConfig = null;

            if (task.IsFaulted)
            {
                Debug.LogError($"Music '{id}' failed to bake: {task.Exception?.GetBaseException().Message}");
                if (_pendingId == id) _pendingId = null;
                return;
            }

            var rendered = MusicSynth.ToClips(config, task.Result);
            if (rendered == null)
            {
                if (_pendingId == id) _pendingId = null;
                return;
            }

            RenderCache[id] = rendered;
            if (_pendingId != id) return;

            float fade = _pendingFade;
            _pendingId = null;
            Begin(id, rendered, fade);
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
        }

        void Stop()
        {
            _introSource.Stop();
            _loopSource.Stop();
            _introSource.clip = null;
            _loopSource.clip = null;
            _currentId = null;
            _pendingId = null;
            _volume = 0f;
            _volumeTarget = 0f;
            _stopWhenSilent = false;
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
