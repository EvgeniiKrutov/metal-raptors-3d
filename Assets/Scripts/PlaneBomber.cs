using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneBomber : MonoBehaviour
    {
        const float BellyClearance = 4f;

        public event Action<Vector3, float> OnDetonated;

        public float Charge => _config == null
            ? 1f
            : 1f - Mathf.Clamp01(_cooldown / Mathf.Max(0.01f, _config.bombCooldown));

        public bool IsReady => enabled && _cooldown <= 0f && !CinematicBars.AnyShowing;

        PlayerConfig _config;
        Collider _planeCollider;
        Rigidbody _rb;
        GameObject _template;
        float _cooldown;

        public void Initialize(PlayerConfig config, Collider planeCollider)
        {
            _config = config;
            _planeCollider = planeCollider;
            _rb = GetComponent<Rigidbody>();
            _template = Bomb.BuildTemplate();

            Physics.IgnoreLayerCollision(Bomb.Layer, Bomb.Layer, true);
        }

        public void Stop() => enabled = false;

        public void Resume() => enabled = true;

        public void Request()
        {
            if (!IsReady || _config == null || GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            _cooldown = Mathf.Max(0.01f, _config.bombCooldown);
            Release();
        }

        void Update()
        {
            if (_config == null || GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);

            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) Request();
        }

        void Release()
        {
            Vector3 position = transform.position - transform.up * BellyClearance;

            var go = Instantiate(_template, position, Quaternion.identity);
            go.name = "Bomb";
            go.SetActive(true);
            go.GetComponent<Bomb>().Launch(_rb != null ? _rb.linearVelocity : Vector3.zero,
                _config.bombDamage, _config.bombBlastRadius, _planeCollider, Report);
        }

        void Report(Vector3 position, float radius) => OnDetonated?.Invoke(position, radius);
    }
}
