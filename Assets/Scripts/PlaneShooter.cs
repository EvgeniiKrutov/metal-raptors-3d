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
        Collider _planeCollider;

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;
        float _cooldown;

        public void Initialize(PlayerConfig config, Transform muzzle, Collider planeCollider)
        {
            _config = config;
            _muzzle = muzzle;
            _planeCollider = planeCollider;

            // Bright polished brass for the player's rounds.
            _bulletTemplate = Bullet.BuildTemplate(new Color(0.85f, 0.62f, 0.30f));

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

            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume);
        }
    }
}
