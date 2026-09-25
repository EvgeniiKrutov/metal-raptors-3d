using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneBarrelRoll : MonoBehaviour
    {
        public float Charge => _config == null
            ? 1f
            : IsRunning
                ? 1f
                : 1f - Mathf.Clamp01(_cooldown / Mathf.Max(0.01f, _config.rollCooldown));

        public bool IsReady => enabled && _cooldown <= 0f && !CinematicBars.AnyShowing;

        public bool IsRunning => _plane != null && _plane.BarrelRolling;

        public float Cooldown
        {
            get => IsRunning && _config != null ? _config.rollCooldown : _cooldown;
            set => _cooldown = Mathf.Max(0f, value);
        }

        PlayerConfig _config;
        CubeController _plane;
        WingStreaks _trails;
        float _cooldown;
        bool _wasRolling;

        public void Initialize(PlayerConfig config, CubeController plane, Transform model)
        {
            _config = config;
            _plane = plane;
            _trails = WingStreaks.Mount(gameObject, model);
        }

        public void Stop()
        {
            enabled = false;
            if (_wasRolling) End();
        }

        public void Resume() => enabled = true;

        public void Request()
        {
            if (!IsReady || IsRunning || GameMenu.IsOpen || LevelBriefing.IsOpen) return;
            if (_plane == null || !_plane.BeginBarrelRoll(RollRate, _config.rollGrace)) return;

            _wasRolling = true;
            if (_trails != null) _trails.SetEmitting(this, true);
        }

        float RollRate => _config == null
            ? 0f
            : _config.rotationSpeed * Mathf.Max(1f, _config.rollRateMultiplier);

        void Update()
        {
            if (_config == null || GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            if (_wasRolling)
            {
                if (!IsRunning) End();
                return;
            }

            _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);

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
