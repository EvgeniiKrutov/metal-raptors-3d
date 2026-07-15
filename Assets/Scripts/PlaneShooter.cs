using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    /// <summary>
    /// The player plane's machine guns. While F is held, fires a glowing yellow tracer round
    /// from the muzzle every <see cref="CubeConfig.fireRate"/> seconds and plays the shot sound
    /// (the sibling repo's <c>bullet_shot_1.wav</c>). Lives on the physics body next to
    /// <see cref="CubeController"/>, whose yaw makes this transform's +X the flight heading, so
    /// rounds always leave along the nose. Wired up by <see cref="LevelController"/>, which also
    /// places the muzzle just ahead of the propeller at machine-gun height.
    /// </summary>
    public class PlaneShooter : MonoBehaviour
    {
        const float ShotVolume = 0.3f; // matches the sibling repo's bullet_shot volume

        CubeConfig _config;
        Transform _muzzle;
        Collider _planeCollider;

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;
        float _cooldown;

        public void Initialize(CubeConfig config, Transform muzzle, Collider planeCollider)
        {
            _config = config;
            _muzzle = muzzle;
            _planeCollider = planeCollider;

            _bulletTemplate = BuildBulletTemplate();

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

        /// <summary>
        /// Builds the one inactive round every shot is cloned from, so all bullets share a
        /// single material instead of creating one per shot. An 8 m emissive yellow cylinder —
        /// tracer-sized, so it still reads at the camera's ~420 m distance.
        /// </summary>
        GameObject BuildBulletTemplate()
        {
            var template = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(1.2f, 4f, 1.2f),
                new Color(1f, 0.9f, 0.2f), emissive: true);
            template.AddComponent<Bullet>(); // RequireComponent pulls the Rigidbody in with it
            template.SetActive(false);
            template.name = "BulletTemplate";
            return template;
        }
    }
}
