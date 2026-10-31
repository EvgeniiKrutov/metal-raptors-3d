using UnityEngine;
using UnityEngine.InputSystem;

namespace MetalRaptors
{
    public class PlaneShooter : MonoBehaviour
    {
        const float ShotVolume = 0.36f;
        const float AimMemory = 0.25f;
        const float TurretFlashScale = 0.45f;

        const float JamSafeSec = 2f;
        const float JamRate = 0.5f;
        const int JamTaps = 7;
        const float ClatterVolume = 0.5f;

        PlayerConfig _config;
        Transform _muzzle;
        Transform _flashPoint;
        Collider _planeCollider;
        float _bodyRadius;
        RearGun _turret;
        Rigidbody _body;

        GameObject _bulletTemplate;
        AudioSource _audio;
        AudioClip _shotClip;
        AudioSource _clatter;
        float _cooldown;
        float _aimHold;
        bool _held;
        bool _heldBefore;
        float _burst;
        int _tapsLeft;

        public bool IsReady => enabled;

        public bool Firing => _aimHold > 0f;

        public bool Jams { get; set; }

        public bool Jammed => _tapsLeft > 0;

        public int TapsLeft => _tapsLeft;

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
            _burst = 0f;
        }

        public void Resume() => enabled = true;

        public void SetHeld(bool held) => _held = held;

        public void MountTurret(RearGun turret, float pitch)
        {
            _turret = turret;
            _body = GetComponent<Rigidbody>();
            if (_audio != null) _audio.pitch = pitch;
        }

        void Update()
        {
            _aimHold = Mathf.Max(0f, _aimHold - Time.deltaTime);
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            _cooldown -= Time.deltaTime;

            var kb = Keyboard.current;
            bool wants = _held || (kb != null && kb.fKey.isPressed);
            bool tapped = (_held && !_heldBefore) || (kb != null && kb.fKey.wasPressedThisFrame);
            _heldBefore = _held;

            if (_tapsLeft > 0)
            {
                if (!_turret.Ready) Unjam();
                else if (tapped) Rack();
                return;
            }

            if (!wants)
            {
                _burst = 0f;
                return;
            }

            _aimHold = AimMemory;
            _burst += Time.deltaTime;
            if (_cooldown > 0f) return;

            _cooldown = Mathf.Max(0.01f, _config.fireRate);
            if (Jamming()) Jam();
            else Fire();
        }

        bool Jamming()
        {
            if (!Jams || _turret == null) return false;

            float over = _burst - JamSafeSec;
            return over > 0f && Random.value < JamRate * over * _config.fireRate;
        }

        void Jam()
        {
            _tapsLeft = JamTaps;
            _burst = 0f;
            _aimHold = 0f;
            _turret.Jammed = true;
            Clatter(GunClatter.Jam);
        }

        void Rack()
        {
            _tapsLeft--;
            if (_tapsLeft > 0)
            {
                Clatter(GunClatter.Rack);
                return;
            }

            Unjam();
            Clatter(GunClatter.Clear);
        }

        void Unjam()
        {
            _tapsLeft = 0;
            _turret.Jammed = false;
        }

        void Clatter(AudioClip clip)
        {
            if (_clatter == null)
            {
                _clatter = gameObject.AddComponent<AudioSource>();
                _clatter.playOnAwake = false;
                _clatter.spatialBlend = 0f;
            }

            _clatter.PlayOneShot(clip, ClatterVolume * AudioOptions.Scene);
        }

        void Fire()
        {
            if (_turret != null)
            {
                FireTurret();
                return;
            }

            Vector3 dir = transform.right;

            var go = Instantiate(_bulletTemplate, _muzzle.position,
                transform.rotation * Quaternion.Euler(0f, 0f, -90f));
            go.name = "Bullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(dir, _config.bulletSpeed, _config.damage,
                _planeCollider, fromEnemy: false);

            MuzzleFlash.Spawn(_flashPoint.position, dir, _bodyRadius);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Scene);
        }

        void FireTurret()
        {
            Vector3 dir = _turret.Direction;
            Vector3 from = _turret.Muzzle;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector3 carry = _body != null ? _body.linearVelocity : Vector3.zero;
            Vector3 shot = AlongLine(dir, carry, _config.bulletSpeed);

            var go = Instantiate(_bulletTemplate, from, Quaternion.Euler(0f, 0f, angle - 90f));
            go.name = "Bullet";
            go.SetActive(true);
            go.GetComponent<Bullet>().Launch(shot, _config.bulletSpeed, _config.damage,
                _planeCollider, fromEnemy: false);

            MuzzleFlash.Spawn(from, dir, _bodyRadius * TurretFlashScale);
            if (_shotClip != null) _audio.PlayOneShot(_shotClip, ShotVolume * AudioOptions.Scene);
        }

        static Vector3 AlongLine(Vector3 aim, Vector3 carry, float speed)
        {
            carry.z = 0f;
            float along = Vector3.Dot(carry, aim);
            float room = along * along - carry.sqrMagnitude + speed * speed;
            if (room < 0f) return aim;

            Vector3 shot = carry + aim * (Mathf.Sqrt(room) - along);
            return shot.sqrMagnitude > 0.0001f ? shot.normalized : aim;
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
