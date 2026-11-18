using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneBarrelRoll : MonoBehaviour
    {
        public const float DuelistSeconds = 2.5f;
        const float FullTurn = 360f;
        const float MinRollRate = 40f;

        public float Charge => _config == null
            ? 1f
            : IsRunning
                ? 1f
                : 1f - Mathf.Clamp01(_cooldown / Mathf.Max(0.01f, _config.rollCooldown));

        public bool Locked { get; set; }

        public bool IsReady => enabled && !Locked && _cooldown <= 0f && !CinematicBars.AnyShowing;

        public bool IsRunning => _plane != null && _plane.BarrelRolling;

        public float Cooldown
        {
            get => IsRunning && _config != null ? _config.rollCooldown : _cooldown;
            set => _cooldown = Mathf.Max(0f, value);
        }

        PlayerConfig _config;
        CubeController _plane;
        WingStreaks _trails;
        float _spinScale = 1f;
        bool _rateRoll;
        float _cooldown;
        bool _wasRolling;

        public void Initialize(PlayerConfig config, CubeController plane, Transform model,
            float spinScale = 1f, bool streaks = true, PlaneModelConfig planeModel = null,
            bool rateRoll = false)
        {
            _config = config;
            _plane = plane;
            _spinScale = Mathf.Max(0.01f, spinScale);
            _rateRoll = rateRoll;
            if (streaks) _trails = WingStreaks.Mount(gameObject, model, null, planeModel);
        }

        public void Stop()
        {
            enabled = false;
            if (_wasRolling) End();
        }

        public void Resume() => enabled = true;

        public void Request()
        {
            if (!IsReady || IsRunning || GameMenu.IsOpen || LevelBriefing.IsOpen || DevFreeze.Active) return;
            if (_plane == null || !_plane.BeginBarrelRoll(RollSeconds, 1f, _config.rollGrace)) return;

            _wasRolling = true;
            if (_trails != null) _trails.SetEmitting(this, true);
        }

        float RollSeconds => _rateRoll ? FullTurn / Mathf.Max(MinRollRate, RollRate) : DuelistSeconds;

        float RollRate => _config == null
            ? 0f
            : _config.rotationSpeed * Mathf.Max(1f, _config.rollRateMultiplier) * _spinScale;

        void Update()
        {
            if (_config == null || GameMenu.IsOpen || LevelBriefing.IsOpen || DevFreeze.Active) return;

            if (_wasRolling)
            {
                if (!IsRunning) End();
                return;
            }

            _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime * DevCheats.CooldownFactor);

            var kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame) { Request(); return; }

            var pad = Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame) Request();
        }

        void End()
        {
            _wasRolling = false;
            _cooldown = _config != null ? Mathf.Max(0.01f, _config.rollCooldown) : 0f;
            if (_trails != null) _trails.SetEmitting(this, false);
        }

        void OnDisable()
        {
            if (_trails != null) _trails.SetEmitting(this, false);
        }
    }
}
