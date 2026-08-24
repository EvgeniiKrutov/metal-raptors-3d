using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneShooter : MonoBehaviour
    {
        const float ShotVolume = 0.36f;
        const float AimMemory = 0.25f;

        PlayerConfig _config;
        Transform _muzzle;
        Transform _flashPoint;
        Collider _planeCollider;
        float _bodyRadius;

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;
        float _cooldown;
        float _aimHold;
        bool _held;

        public bool IsReady => enabled;

        public bool Firing => _aimHold > 0f;

        public void Initialize(PlayerConfig config, Transform muzzle, Transform flashPoint,
            Collider planeCollider)
        {
            _config = config;
            _muzzle = muzzle;
            _flashPoint = flashPoint != null ? flashPoint : muzzle;
            _planeCollider = planeCollider;
            _bodyRadius = MeasureBodyRadius();

            _bulletTemplate = Bullet.BuildTemplate(Bullet.RoundColor);

            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            if (_shotClip == null)
                Debug.LogWarning("PlaneShooter: Sounds/bullet_shot_1 not found in Resources.");

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        public void Stop()
        {
            enabled = false;
            _aimHold = 0f;
        }

        public void Resume() => enabled = true;

        public void SetHeld(bool held) => _held = held;

        void Update()
        {
            _aimHold = Mathf.Max(0f, _aimHold - Time.deltaTime);
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            _cooldown -= Time.deltaTime;

            var kb = Keyboard.current;
            bool wants = _held || (kb != null && kb.fKey.isPressed);
            if (!wants) return;

            _aimHold = AimMemory;
            if (_cooldown > 0f) return;

            _cooldown = Mathf.Max(0.01f, _config.fireRate);
            Fire();
        }

        void Fire()
        {
            Vector3 dir = transform.right;

            var go = Instantiate(_bulletTemplate, _muzzle.position,
                transform.rotation * Quaternion.Euler(0f, 0f, -90f));
            go.name = "Bullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, _config.bulletSpeed, _config.damage,
                _planeCollider, fromEnemy: false);

            MuzzleFlash.Spawn(_flashPoint.position, dir, _bodyRadius);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume);
        }

        float MeasureBodyRadius()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                return Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f;
            }
            if (_planeCollider != null)
            {
                var s = _planeCollider.bounds.size;
                return Mathf.Max(s.x, s.y, s.z) * 0.5f;
            }
            return 30f;
        }
    }
}
