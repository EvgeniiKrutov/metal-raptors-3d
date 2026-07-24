using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    /// <summary>
    /// The player plane's machine guns. While F is held, fires a brass machine-gun round
    /// from the muzzle every <see cref="PlayerConfig.fireRate"/> seconds and plays the shot sound
    /// (the sibling repo's <c>bullet_shot_1.wav</c>). Lives on the physics body next to
    /// <see cref="CubeController"/>, whose yaw makes this transform's +X the flight heading, so
    /// rounds always leave along the nose. Wired up by <see cref="LevelController"/>, which also
    /// places the muzzle just ahead of the propeller at machine-gun height.
    /// </summary>
    public class PlaneShooter : MonoBehaviour
    {
        const float ShotVolume = 0.3f; // matches the sibling repo's bullet_shot volume

        PlayerConfig _config;
        Transform _muzzle;
        Transform _flashPoint; // where the muzzle flash bursts: the cowl, lower than the gun muzzle
        Collider _planeCollider;
        float _bodyRadius; // half the plane model's longest extent — scales the muzzle flash

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;
        float _cooldown;

        public void Initialize(PlayerConfig config, Transform muzzle, Transform flashPoint,
            Collider planeCollider)
        {
            _config = config;
            _muzzle = muzzle;
            _flashPoint = flashPoint != null ? flashPoint : muzzle;
            _planeCollider = planeCollider;
            _bodyRadius = MeasureBodyRadius();

            // Both sides fire the same polished-brass round.
            _bulletTemplate = Bullet.BuildTemplate(Bullet.RoundColor);

            _shotClip = Resources.Load<AudioClip>("Sounds/bullet_shot_1");
            if (_shotClip == null)
                Debug.LogWarning("PlaneShooter: Sounds/bullet_shot_1 not found in Resources.");

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D: the camera sits ~420 m back, 3D rolloff would mute it
        }

        /// <summary>The guns fall silent when the level ends (crash or win).</summary>
        public void Stop() => enabled = false;

        void Update()
        {
            _cooldown -= Time.deltaTime;

            var kb = Keyboard.current;
            if (kb == null || !kb.fKey.isPressed || _cooldown > 0f) return;

            _cooldown = Mathf.Max(0.01f, _config.fireRate);
            Fire();
        }

        void Fire()
        {
            // The physics body yaws about Z to the heading, so its +X is the flight direction.
            Vector3 dir = transform.right;

            // The extra -90° about Z lays the cylinder's long axis (+Y) along the heading (+X).
            var go = Instantiate(_bulletTemplate, _muzzle.position,
                transform.rotation * Quaternion.Euler(0f, 0f, -90f));
            go.name = "Bullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, _config.bulletSpeed, _config.damage,
                _planeCollider);

            MuzzleFlash.Spawn(_flashPoint.position, dir, _bodyRadius);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume);
        }

        /// <summary>Half the longest side of the plane model's combined renderer bounds, matching
        /// <see cref="EnemyController"/>, so the muzzle flash scales to whatever size the model is
        /// built at. Falls back to the collider bounds, then a sensible constant.</summary>
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
