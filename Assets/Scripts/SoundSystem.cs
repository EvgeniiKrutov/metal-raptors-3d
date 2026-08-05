using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class SoundSystem : MonoBehaviour
    {
        const float IdleVolume = 0.95f;
        const float ThrottleVolume = 0.2f;
        const float StutterVolume = 0.3f;
        const float WindVolume = 0.35f;
        const float EnemyThrottleVolume = 0.15f;

        const float CrossfadeSeconds = 0.7f;
        const float RetireFadeSeconds = 0.3f;
        const float ThrottleGraceSeconds = 0.3f;
        const float SpawnFadeSeconds = 0.6f;
        const float PauseFadeSeconds = 0.12f;

        const float TurnRateThreshold = 0.4f;
        const float ClimbAngleDeg = 30f;

        const float EnemyFadeStartDistance = 320f;
        const float EnemyFadeEndDistance = 900f;
        const int MaxAudibleEnemies = 3;
        const float AttenuationSmoothing = 6f;

        const float StutterHealthFraction = 0.3f;

        static readonly string[] ThrottleClipPaths =
        {
            "Sounds/engine_throttle_1",
            "Sounds/engine_throttle_2",
        };

        struct Ramp
        {
            public float Value;
            public float Target;
            public float Rate;

            public void Jump(float value)
            {
                Value = value;
                Target = value;
                Rate = 1f;
            }

            public void Set(float target, float seconds)
            {
                Target = target;
                Rate = seconds > 0.0001f ? 1f / seconds : 1000f;
            }

            public void Tick(float dt) => Value = Mathf.MoveTowards(Value, Target, Rate * dt);

            public bool Done => Mathf.Approximately(Value, Target);
        }

        abstract class EngineVoice
        {
            public float TargetAttenuation = 1f;
            public bool Finished { get; private set; }

            protected Ramp Envelope;
            protected float Attenuation = 1f;
            protected float PauseGain = 1f;

            protected EngineVoice()
            {
                Envelope.Jump(0f);
                Envelope.Set(1f, SpawnFadeSeconds);
            }

            public void Retire() => Envelope.Set(0f, RetireFadeSeconds);

            public void Tick(float dt, float pauseGain)
            {
                if (Finished) return;

                PauseGain = pauseGain;
                Envelope.Tick(dt);

                float smoothing = 1f - Mathf.Exp(-AttenuationSmoothing * dt);
                Attenuation += (TargetAttenuation - Attenuation) * smoothing;

                Advance(dt);
                Apply();

                if (Envelope.Target <= 0f && Envelope.Done) Finished = true;
            }

            protected abstract void Advance(float dt);

            protected abstract void Apply();

            public abstract void Dispose();

            protected static AudioSource CreateLoop(GameObject host, string clipPath)
            {
                var source = host.AddComponent<AudioSource>();
                source.clip = Resources.Load<AudioClip>(clipPath);
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0f;
                if (source.clip == null)
                    Debug.LogWarning($"SoundSystem: {clipPath} not found in Resources.");
                else
                    source.Play();
                return source;
            }

            protected static string RandomThrottleClip() =>
                ThrottleClipPaths[Random.Range(0, ThrottleClipPaths.Length)];
        }

        class PlayerEngineVoice : EngineVoice
        {
            readonly CubeController _plane;
            readonly AudioSource _idle;
            readonly AudioSource _throttle;

            Ramp _idleLevel;
            Ramp _throttleLevel;
            bool _revving;
            float _grace;

            public PlayerEngineVoice(GameObject host, CubeController plane)
            {
                _plane = plane;
                _idle = CreateLoop(host, "Sounds/engine_idle");
                _throttle = CreateLoop(host, RandomThrottleClip());
                _idleLevel.Jump(1f);
                _throttleLevel.Jump(0f);
            }

            protected override void Advance(float dt)
            {
                if (IsManeuvering())
                {
                    _grace = ThrottleGraceSeconds;
                    if (!_revving) EnterThrottle();
                }
                else if (_revving)
                {
                    _grace -= dt;
                    if (_grace <= 0f) EnterIdle();
                }

                _idleLevel.Tick(dt);
                _throttleLevel.Tick(dt);
            }

            bool IsManeuvering()
            {
                if (_plane == null) return false;
                bool turning = Mathf.Abs(_plane.AngularVelocity)
                               > _plane.MaxTurnRate * TurnRateThreshold;
                bool climbing = Mathf.Sin(_plane.Heading) > Mathf.Sin(ClimbAngleDeg * Mathf.Deg2Rad);
                return turning || climbing;
            }

            void EnterThrottle()
            {
                _revving = true;
                if (_throttle != null && !_throttle.isPlaying && _throttle.clip != null)
                    _throttle.Play();
                _idleLevel.Set(0f, CrossfadeSeconds);
                _throttleLevel.Set(1f, CrossfadeSeconds);
            }

            void EnterIdle()
            {
                _revving = false;
                _idleLevel.Set(1f, CrossfadeSeconds);
                _throttleLevel.Set(0f, CrossfadeSeconds);
            }

            protected override void Apply()
            {
                float bed = Envelope.Value * Attenuation * PauseGain;
                if (_idle != null) _idle.volume = IdleVolume * bed * _idleLevel.Value;
                if (_throttle != null) _throttle.volume = ThrottleVolume * bed * _throttleLevel.Value;
            }

            public override void Dispose()
            {
                if (_idle != null) UnityEngine.Object.Destroy(_idle);
                if (_throttle != null) UnityEngine.Object.Destroy(_throttle);
            }
        }

        class EnemyEngineVoice : EngineVoice
        {
            readonly AudioSource _throttle;

            public EnemyEngineVoice(GameObject host)
            {
                _throttle = CreateLoop(host, RandomThrottleClip());
            }

            protected override void Advance(float dt)
            {
            }

            protected override void Apply()
            {
                if (_throttle != null)
                    _throttle.volume = EnemyThrottleVolume * Envelope.Value * Attenuation * PauseGain;
            }

            public override void Dispose()
            {
                if (_throttle != null) UnityEngine.Object.Destroy(_throttle);
            }
        }

        struct AudibleEnemy
        {
            public EnemyEngineVoice Voice;
            public float Distance;
        }

        static readonly System.Comparison<AudibleEnemy> ByDistance =
            (a, b) => a.Distance.CompareTo(b.Distance);

        CubeController _player;
        Transform _playerTr;
        IReadOnlyList<EnemyController> _enemies;

        PlayerEngineVoice _playerVoice;
        readonly Dictionary<EnemyController, EnemyEngineVoice> _enemyVoices =
            new Dictionary<EnemyController, EnemyEngineVoice>();
        readonly List<EngineVoice> _retiring = new List<EngineVoice>();
        readonly List<EnemyController> _dropScratch = new List<EnemyController>();
        readonly List<AudibleEnemy> _rankScratch = new List<AudibleEnemy>();

        AudioSource _wind;
        AudioSource _stutter;

        bool _gameOver;
        bool _paused;
        bool _windStopped;
        Ramp _pauseGain;

        public static SoundSystem Begin(CubeController player, IReadOnlyList<EnemyController> enemies)
        {
            var go = new GameObject("SoundSystem");
            var system = go.AddComponent<SoundSystem>();
            system.Init(player, enemies);
            return system;
        }

        void Init(CubeController player, IReadOnlyList<EnemyController> enemies)
        {
            _player = player;
            _playerTr = player != null ? player.transform : null;
            _enemies = enemies;

            _pauseGain.Jump(1f);

            if (player != null) _playerVoice = new PlayerEngineVoice(gameObject, player);

            _wind = gameObject.AddComponent<AudioSource>();
            _wind.clip = Resources.Load<AudioClip>("Sounds/ambient_wind");
            _wind.loop = true;
            _wind.playOnAwake = false;
            _wind.spatialBlend = 0f;
            _wind.volume = WindVolume;
            if (_wind.clip != null) _wind.Play();

            _stutter = gameObject.AddComponent<AudioSource>();
            _stutter.clip = Resources.Load<AudioClip>("Sounds/engine_stutter");
            _stutter.loop = false;
            _stutter.playOnAwake = false;
            _stutter.spatialBlend = 0f;
            _stutter.volume = StutterVolume;
        }

        public void ReportPlayerDamaged()
        {
            if (_gameOver || _player == null) return;
            if (_player.CurrentHealth <= 0f || _player.MaxHealth <= 0f) return;
            if (_player.CurrentHealth / _player.MaxHealth > StutterHealthFraction) return;
            PlayStutter();
        }

        public void PlayStutter()
        {
            if (_stutter == null || _stutter.clip == null || _stutter.isPlaying) return;
            _stutter.Play();
        }

        public void EnterGameOver()
        {
            if (_gameOver) return;
            _gameOver = true;

            if (_playerVoice != null)
            {
                Retire(_playerVoice);
                _playerVoice = null;
            }
            foreach (var voice in _enemyVoices.Values) Retire(voice);
            _enemyVoices.Clear();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_gameOver && GameMenu.IsOpen) StopWind();

            UpdatePause(dt);
            if (_paused) return;

            _playerVoice?.Tick(dt, _pauseGain.Value);

            if (!_gameOver)
            {
                SyncEnemyVoices();
                UpdateEnemyVoices(dt);
            }

            UpdateRetiring(dt);
            ApplyAmbient();
        }

        void UpdatePause(float dt)
        {
            bool wantPause = GameMenu.IsOpen && !_gameOver;

            if (wantPause && !_paused)
            {
                _pauseGain.Set(0f, PauseFadeSeconds);
                StepPauseRamp(dt);
                if (_pauseGain.Done)
                {
                    _paused = true;
                    SetSourcesPaused(true);
                }
                return;
            }

            if (!wantPause && _paused)
            {
                _paused = false;
                SetSourcesPaused(false);
                _pauseGain.Set(1f, PauseFadeSeconds);
            }

            if (!wantPause && _pauseGain.Target < 1f) _pauseGain.Set(1f, PauseFadeSeconds);
            StepPauseRamp(dt);
        }

        void StepPauseRamp(float dt)
        {
            _pauseGain.Tick(dt);
            _playerVoice?.Tick(0f, _pauseGain.Value);
            foreach (var voice in _enemyVoices.Values) voice.Tick(0f, _pauseGain.Value);
            ApplyAmbient();
        }

        void SetSourcesPaused(bool paused)
        {
            foreach (var source in GetComponents<AudioSource>())
            {
                if (paused) source.Pause();
                else source.UnPause();
            }
        }

        void ApplyAmbient()
        {
            if (_wind != null && !_windStopped) _wind.volume = WindVolume * _pauseGain.Value;
        }

        void StopWind()
        {
            if (_wind == null || _windStopped) return;
            _windStopped = true;
            _wind.volume = 0f;
            _wind.Stop();
        }

        void SyncEnemyVoices()
        {
            _dropScratch.Clear();
            foreach (var pair in _enemyVoices)
            {
                var enemy = pair.Key;
                if (enemy != null && enemy.IsAlive && Contains(enemy)) continue;
                _dropScratch.Add(enemy);
                Retire(pair.Value);
            }

            foreach (var enemy in _dropScratch) _enemyVoices.Remove(enemy);

            if (_enemies == null) return;

            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if (!_enemyVoices.ContainsKey(enemy))
                    _enemyVoices[enemy] = new EnemyEngineVoice(gameObject);
            }
        }

        bool Contains(EnemyController enemy)
        {
            if (_enemies == null) return false;
            for (int i = 0; i < _enemies.Count; i++)
                if (ReferenceEquals(_enemies[i], enemy)) return true;
            return false;
        }

        void UpdateEnemyVoices(float dt)
        {
            _rankScratch.Clear();
            foreach (var pair in _enemyVoices)
                _rankScratch.Add(new AudibleEnemy
                {
                    Voice = pair.Value,
                    Distance = DistanceToPlayer(pair.Key),
                });

            _rankScratch.Sort(ByDistance);

            for (int i = 0; i < _rankScratch.Count; i++)
            {
                var entry = _rankScratch[i];
                entry.Voice.TargetAttenuation =
                    i < MaxAudibleEnemies ? AttenuationFor(entry.Distance) : 0f;
                entry.Voice.Tick(dt, _pauseGain.Value);
            }
        }

        float AttenuationFor(float distance)
        {
            if (distance <= EnemyFadeStartDistance) return 1f;
            if (distance >= EnemyFadeEndDistance) return 0f;
            return 1f - (distance - EnemyFadeStartDistance)
                / (EnemyFadeEndDistance - EnemyFadeStartDistance);
        }

        float DistanceToPlayer(EnemyController enemy)
        {
            if (enemy == null || _playerTr == null) return float.MaxValue;
            return Vector2.Distance(enemy.transform.position, _playerTr.position);
        }

        void Retire(EngineVoice voice)
        {
            voice.Retire();
            _retiring.Add(voice);
        }

        void UpdateRetiring(float dt)
        {
            for (int i = _retiring.Count - 1; i >= 0; i--)
            {
                var voice = _retiring[i];
                voice.Tick(dt, _pauseGain.Value);
                if (!voice.Finished) continue;
                voice.Dispose();
                _retiring.RemoveAt(i);
            }
        }
    }
}
