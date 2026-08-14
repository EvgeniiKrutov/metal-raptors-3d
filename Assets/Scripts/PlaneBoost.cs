using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneBoost : MonoBehaviour
    {
        public float Charge => _config == null
            ? 1f
            : _running > 0f
                ? 1f
                : 1f - Mathf.Clamp01(_cooldown / Mathf.Max(0.01f, _config.boostCooldown));

        public bool IsReady => enabled && _cooldown <= 0f && !CinematicBars.AnyShowing;

        public bool IsRunning => _running > 0f;

        PlayerConfig _config;
        CubeController _plane;
        BoostTrails _trails;
        float _running;
        float _cooldown;

        public void Initialize(PlayerConfig config, CubeController plane, Transform model)
        {
            _config = config;
            _plane = plane;
            _trails = BoostTrails.Mount(gameObject, model);
        }

        public void Stop()
        {
            enabled = false;
            if (_running > 0f) End();
        }

        public void Resume() => enabled = true;

        void Update()
        {
            if (_config == null || GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            if (_running > 0f)
            {
                _running -= Time.deltaTime;
                if (_running <= 0f) End();
                return;
            }

            _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);

            if (CinematicBars.AnyShowing) return;

            var kb = Keyboard.current;
            if (kb == null || !kb.rKey.wasPressedThisFrame || _cooldown > 0f) return;

            Begin();
        }

        void Begin()
        {
            _running = Mathf.Max(0.01f, _config.boostDuration);
            if (_plane != null) _plane.SetBoost(true);
            if (_trails != null) _trails.SetEmitting(true);
        }

        void End()
        {
            _running = 0f;
            _cooldown = _config != null ? Mathf.Max(0.01f, _config.boostCooldown) : 0f;
            if (_plane != null) _plane.SetBoost(false);
            if (_trails != null) _trails.SetEmitting(false);
        }

        void OnDisable()
        {
            if (_trails != null) _trails.SetEmitting(false);
        }
    }
}
