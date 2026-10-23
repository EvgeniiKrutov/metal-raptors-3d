using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class S150Boats : MonoBehaviour
    {
        const string ModelResource = "objects/machines/s150_schnellboot";
        const string BowGunNode = "gun_fwd";
        const float ProbeY = -9000f;

        const float LaneNear = 560f, LaneFar = 660f;
        const float SpeedShare = 0.6f;
        const float SpeedMin = ThornycroftBoats.SpeedMin * SpeedShare;
        const float SpeedMax = ThornycroftBoats.SpeedMax * SpeedShare;
        const float GapMin = 2f, GapMax = 6f;
        const float OnScreenMin = -0.3f, OnScreenMax = 0.5f;
        const float EdgeMargin = 40f;

        const float Heading = 180f;
        const float Draft = 2.2f;
        const float PivotAft = 0.1f;
        const float Trim = 1f;
        const float BowWave = 0.42f;

        const float PitchSlowMin = 0.3f, PitchSlowMax = 0.55f, PitchSlowSecMin = 2.6f, PitchSlowSecMax = 3.6f;
        const float PitchFastMin = 0.1f, PitchFastMax = 0.2f, PitchFastSecMin = 1f, PitchFastSecMax = 1.4f;
        const float RollMin = 0.6f, RollMax = 1.1f, RollSecMin = 3.8f, RollSecMax = 5f;
        const float HeaveMin = 0.05f, HeaveMax = 0.1f, HeaveSecMin = 1.4f, HeaveSecMax = 2f;

        const float MountBack = 0.16f;
        const float TrunnionHeight = 0.5f;
        const float AimHoldMin = 0.5f, AimHoldMax = 2.5f;
        const float ScreenInset = 0.04f;
        const float RetryMin = 0.4f, RetryMax = 2f;

        const float BigArc = 135f;
        const float BigLiftMin = 15f, BigLiftMax = 40f;
        const float BigYawSpeed = 12f, BigLiftSpeed = 8f;
        const float BigIntervalMin = 3f, BigIntervalMax = 6f;
        const float BigFlashMeters = 6f, BigSmokeMeters = 5f;

        const float AaArcAft = 165f, AaArcInboard = 15f;
        const float AaLiftMin = 30f, AaLiftMax = 75f;
        const float AaYawSpeed = 20f, AaLiftSpeed = 14f;
        const float AaIntervalMin = 4f, AaIntervalMax = 8f;
        const int AaBurstMin = 3, AaBurstMax = 5;
        const float AaShotGapMin = 0.12f, AaShotGapMax = 0.2f;
        const float AaFlashMeters = 3f, AaSmokeMeters = 2.5f;

        const float ShellRangeMin = 380f, ShellRangeMax = 620f, ShellRangeFloor = 180f;
        const float ShellSpeed = 320f;
        const float BurstMinZ = 260f;
        const float BurstSizeMin = 50f, BurstSizeMax = 75f;
        const float BurstInsetX = 0.02f, BurstInsetY = 0.05f;
        const float PlayerKeepOut = 0.22f;

        const float BoomVolume = 0.08f;
        const float BoomPitchMin = 0.55f, BoomPitchMax = 0.7f;
        const float BoomCutoff = 900f;

        struct GunSpec
        {
            public string gun, shield;
            public bool towardBow, big;
        }

        static readonly GunSpec[] Guns =
        {
            new GunSpec { gun = "gun_fwd", shield = "gun_fwd_shield", towardBow = true, big = true },
            new GunSpec { gun = "gun_aft", shield = "gun_aft_shield", towardBow = false, big = true },
            new GunSpec { gun = "aa_port", shield = "aa_port_shield", towardBow = true, big = false },
            new GunSpec { gun = "aa_stbd", shield = "aa_stbd_shield", towardBow = true, big = false },
        };

        static GameObject _prefab;
        static Vector3 _center;
        static Vector3 _size;
        static float _waterline;
        static float _yaw;
        static bool _measured;

        class Gun
        {
            public Transform yaw, lift;
            public Quaternion yawRest;
            public Vector3 muzzle;
            public bool big;
            public float facing, restLift;
            public float yawMin, yawMax, liftMin, liftMax, yawSpeed, liftSpeed;
            public float yawNow, liftNow, yawGoal, liftGoal;
            public float hold, wait;
            public int shotsLeft;
        }

        class Boat
        {
            public Transform root, motion;
            public BoatWake wake;
            public BoatSpray spray;
            public List<Gun> guns;
            public float speed, age;
            public bool gone;
            public BoatSway pitchSlow, pitchFast, roll, heave;
        }

        struct Shell
        {
            public Vector3 point;
            public float due, size;
        }

        readonly List<Boat> _boats = new List<Boat>();
        readonly List<Shell> _shells = new List<Shell>();
        Camera _cam;
        Transform _player;
        float _halfViewWidth, _cameraDistance;
        float _scale, _length, _beam, _pivot;
        float _wait;

        public static S150Boats Begin(Camera cam, Transform player, float halfViewWidth, float playPlaneZ,
            float cameraDistance, bool wanted)
        {
            if (cam == null || !wanted || !Measure()) return null;

            float scale = ThornycroftBoats.ModelScale(playPlaneZ, cameraDistance);
            if (scale <= 0f) return null;

            var boats = new GameObject("S150 Boats").AddComponent<S150Boats>();
            boats._cam = cam;
            boats._player = player;
            boats._halfViewWidth = halfViewWidth;
            boats._cameraDistance = Mathf.Max(1f, cameraDistance);
            boats._scale = scale;
            boats._length = _size.x * scale;
            boats._beam = _size.z * scale;
            boats._pivot = boats._length * PivotAft;

            boats.Spawn(cam.transform.position, onScreen: true);
            return boats;
        }

        static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"S150Boats: {ModelResource} not found in Resources.");
                return false;
            }

            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("S150 Probe");
            holder.transform.position = origin;
            var view = Instantiate(prefab, holder.transform, false);

            Renderer bowGun = Find(view.transform, BowGunNode)?.GetComponent<Renderer>();
            if (!Box(view, out Bounds box) || bowGun == null)
            {
                Debug.LogError($"S150Boats: {ModelResource} has no hull or {BowGunNode}.");
                Destroy(holder);
                return false;
            }

            Vector3 bow = bowGun.bounds.center - box.center;
            _yaw = Mathf.Abs(bow.x) >= Mathf.Abs(bow.z)
                ? (bow.x >= 0f ? 0f : 180f)
                : (bow.z >= 0f ? 90f : -90f);

            holder.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            Box(view, out box);

            _center = box.center - origin;
            _size = box.size;
            _waterline = box.min.y - origin.y + Draft;
            _prefab = prefab;

            Destroy(holder);
            return true;
        }

        static bool Box(GameObject view, out Bounds box)
        {
            box = default;
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            box = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);
            return true;
        }

        static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            Vector3 eye = _cam.transform.position;
            float dt = Time.deltaTime, time = Time.time;
            float half = _length * 0.5f;
            bool sailing = false;

            for (int i = _boats.Count - 1; i >= 0; i--)
            {
                Boat boat = _boats[i];
                boat.age += dt;
                boat.root.position += Vector3.left * (boat.speed * dt);

                Vector3 pos = boat.root.position;
                float window = HalfWindow(pos.z, eye);
                float left = eye.x - window - EdgeMargin;
                float right = eye.x + window + EdgeMargin;

                if (!boat.gone && pos.x + half < left)
                {
                    boat.gone = true;
                    _wait = Random.Range(GapMin, GapMax);
                }

                if (boat.gone && pos.x + boat.wake.Reach < left && boat.spray.Idle)
                {
                    Release(boat);
                    _boats.RemoveAt(i);
                    continue;
                }

                Animate(boat, time);
                boat.wake.Follow(pos, time);
                boat.spray.Tick(dt, time, !boat.gone && pos.x - half < right, boat.root, boat.speed);
                foreach (Gun gun in boat.guns)
                {
                    Aim(gun, dt);
                    Fire(gun, dt);
                }
                if (!boat.gone) sailing = true;
            }

            Burst(eye, time);

            if (sailing) return;

            _wait -= dt;
            if (_wait <= 0f) Spawn(eye, onScreen: false);
        }

        float HalfWindow(float z, Vector3 eye) => _halfViewWidth * (z - eye.z) / _cameraDistance;

        void Spawn(Vector3 eye, bool onScreen)
        {
            float z = Random.Range(LaneNear, LaneFar);
            float window = HalfWindow(z, eye);
            float x = onScreen
                ? eye.x + window * Random.Range(OnScreenMin, OnScreenMax)
                : eye.x + window + _length * 0.5f + EdgeMargin;

            var root = new GameObject("S150").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(new Vector3(x, SeaSurface.Level, z),
                Quaternion.Euler(0f, Heading, 0f));

            var motion = new GameObject("Motion").transform;
            motion.SetParent(root, false);

            var holder = new GameObject("Model").transform;
            holder.SetParent(motion, false);
            holder.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            holder.localScale = Vector3.one * _scale;
            holder.localPosition = new Vector3(_pivot, 0f, 0f)
                                   - _scale * new Vector3(_center.x, _waterline, _center.z);

            GameObject view = Instantiate(_prefab, holder, false);
            view.name = _prefab.name;

            foreach (Collider col in view.GetComponentsInChildren<Collider>())
                Destroy(col);

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            NightReceivers.Adopt(view, 1f, 1f);
            NightLights.MarkCaster(view);

            var boat = new Boat
            {
                root = root,
                motion = motion,
                wake = new BoatWake(transform, _length, _beam, BowWave),
                spray = new BoatSpray(transform, _scale, _length, _beam, BowWave),
                guns = Arm(view.transform, root),
                speed = Random.Range(SpeedMin, SpeedMax),
                pitchSlow = BoatSway.Roll(PitchSlowMin, PitchSlowMax, PitchSlowSecMin, PitchSlowSecMax),
                pitchFast = BoatSway.Roll(PitchFastMin, PitchFastMax, PitchFastSecMin, PitchFastSecMax),
                roll = BoatSway.Roll(RollMin, RollMax, RollSecMin, RollSecMax),
                heave = BoatSway.Roll(HeaveMin, HeaveMax, HeaveSecMin, HeaveSecMax),
            };

            float time = Time.time;
            Animate(boat, time);
            boat.wake.Follow(root.position, time);
            _boats.Add(boat);
        }

        static List<Gun> Arm(Transform view, Transform root)
        {
            var guns = new List<Gun>();
            foreach (GunSpec spec in Guns)
            {
                Transform barrel = Find(view, spec.gun);
                Renderer body = barrel != null ? barrel.GetComponent<Renderer>() : null;
                if (body == null) continue;

                Bounds box = body.bounds;
                Vector3 a = root.InverseTransformPoint(box.min);
                Vector3 b = root.InverseTransformPoint(box.max);
                Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);

                float facing = spec.towardBow ? 1f : -1f;
                float front = spec.towardBow ? max.x : min.x;
                float rear = spec.towardBow ? min.x : max.x;
                float mountX = rear + facing * MountBack * (max.x - min.x);
                float midZ = (min.z + max.z) * 0.5f;
                float trunnionY = Mathf.Lerp(min.y, max.y, TrunnionHeight);

                var yaw = new GameObject(spec.gun + " Traverse").transform;
                yaw.SetParent(barrel.parent, false);
                yaw.SetPositionAndRotation(root.TransformPoint(new Vector3(mountX, min.y, midZ)), root.rotation);

                var lift = new GameObject(spec.gun + " Elevation").transform;
                lift.SetParent(yaw, false);
                lift.SetPositionAndRotation(root.TransformPoint(new Vector3(mountX, trunnionY, midZ)),
                    root.rotation);

                barrel.SetParent(lift, true);
                Transform shield = Find(view, spec.shield);
                if (shield != null) shield.SetParent(yaw, true);

                float side = midZ >= 0f ? 1f : -1f;
                var gun = new Gun
                {
                    yaw = yaw,
                    lift = lift,
                    yawRest = yaw.localRotation,
                    muzzle = lift.InverseTransformPoint(root.TransformPoint(new Vector3(front, max.y, midZ))),
                    big = spec.big,
                    facing = facing,
                    restLift = Mathf.Atan2(max.y - trunnionY, Mathf.Abs(front - mountX)) * Mathf.Rad2Deg,
                    yawMin = spec.big ? -BigArc : side > 0f ? -AaArcAft : -AaArcInboard,
                    yawMax = spec.big ? BigArc : side > 0f ? AaArcInboard : AaArcAft,
                    liftMin = spec.big ? BigLiftMin : AaLiftMin,
                    liftMax = spec.big ? BigLiftMax : AaLiftMax,
                    yawSpeed = spec.big ? BigYawSpeed : AaYawSpeed,
                    liftSpeed = spec.big ? BigLiftSpeed : AaLiftSpeed,
                    wait = Random.Range(RetryMin, spec.big ? BigIntervalMax : AaIntervalMax),
                };
                gun.yawNow = Random.Range(gun.yawMin, gun.yawMax);
                gun.liftNow = Random.Range(gun.liftMin, gun.liftMax);
                PickGoal(gun);
                Pose(gun);
                guns.Add(gun);
            }
            return guns;
        }

        static void PickGoal(Gun gun)
        {
            gun.yawGoal = Random.Range(gun.yawMin, gun.yawMax);
            gun.liftGoal = Random.Range(gun.liftMin, gun.liftMax);
            gun.hold = Random.Range(AimHoldMin, AimHoldMax);
        }

        static void Aim(Gun gun, float dt)
        {
            gun.yawNow = Mathf.MoveTowards(gun.yawNow, gun.yawGoal, gun.yawSpeed * dt);
            gun.liftNow = Mathf.MoveTowards(gun.liftNow, gun.liftGoal, gun.liftSpeed * dt);

            if (gun.yawNow == gun.yawGoal && gun.liftNow == gun.liftGoal)
            {
                gun.hold -= dt;
                if (gun.hold <= 0f) PickGoal(gun);
            }

            Pose(gun);
        }

        static void Pose(Gun gun)
        {
            gun.yaw.localRotation = gun.yawRest * Quaternion.Euler(0f, gun.yawNow, 0f);
            gun.lift.localRotation =
                Quaternion.AngleAxis(gun.facing * (gun.liftNow - gun.restLift), Vector3.forward);
        }

        void Fire(Gun gun, float dt)
        {
            gun.wait -= dt;
            if (gun.wait > 0f) return;

            Vector3 muzzle = gun.lift.TransformPoint(gun.muzzle);
            if (!Seen(muzzle))
            {
                gun.shotsLeft = 0;
                gun.wait = Random.Range(RetryMin, RetryMax);
                return;
            }

            Vector3 dir = (muzzle - gun.lift.position).normalized;
            if (gun.big)
            {
                Shot(muzzle, dir, BigFlashMeters, BigSmokeMeters);
                Boom(muzzle);
                Launch(muzzle, dir);
                gun.wait = Random.Range(BigIntervalMin, BigIntervalMax);
                return;
            }

            if (gun.shotsLeft <= 0) gun.shotsLeft = Random.Range(AaBurstMin, AaBurstMax + 1);
            Shot(muzzle, dir, AaFlashMeters, AaSmokeMeters);
            gun.shotsLeft--;
            gun.wait = gun.shotsLeft > 0
                ? Random.Range(AaShotGapMin, AaShotGapMax)
                : Random.Range(AaIntervalMin, AaIntervalMax);
        }

        void Shot(Vector3 muzzle, Vector3 dir, float flashMeters, float smokeMeters)
        {
            MuzzleFlash.Spawn(muzzle, dir, flashMeters * _scale);
            MuzzleSmoke.Spawn(muzzle, dir, smokeMeters * _scale);
        }

        void Launch(Vector3 muzzle, Vector3 dir)
        {
            float range = Random.Range(ShellRangeMin, ShellRangeMax);
            if (dir.z < 0f) range = Mathf.Min(range, (muzzle.z - BurstMinZ) / -dir.z);
            if (range < ShellRangeFloor) return;

            _shells.Add(new Shell
            {
                point = muzzle + dir * range,
                due = Time.time + range / ShellSpeed,
                size = Random.Range(BurstSizeMin, BurstSizeMax),
            });
        }

        void Burst(Vector3 eye, float time)
        {
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                Shell shell = _shells[i];
                if (shell.due > time) continue;

                _shells.RemoveAt(i);
                if (Clear(shell.point)) FlakBurst.Spawn(shell.point, shell.size, eye, false);
            }
        }

        bool Seen(Vector3 point)
        {
            Vector3 vp = _cam.WorldToViewportPoint(point);
            return vp.z > 0f
                   && vp.x > ScreenInset && vp.x < 1f - ScreenInset
                   && vp.y > ScreenInset && vp.y < 1f - ScreenInset;
        }

        bool Clear(Vector3 point)
        {
            Vector3 vp = _cam.WorldToViewportPoint(point);
            if (vp.z <= 0f || vp.x < BurstInsetX || vp.x > 1f - BurstInsetX
                || vp.y < BurstInsetY || vp.y > 1f - BurstInsetY) return false;
            if (_player == null) return true;

            Vector3 pp = _cam.WorldToViewportPoint(_player.position);
            float dx = (vp.x - pp.x) * _cam.aspect, dy = vp.y - pp.y;
            return dx * dx + dy * dy >= PlayerKeepOut * PlayerKeepOut;
        }

        static void Boom(Vector3 position)
        {
            var clip = Resources.Load<AudioClip>($"Sounds/explosion_{Random.Range(1, 4)}");
            if (clip == null) return;

            var go = new GameObject("S150 Gun Sound");
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.pitch = Random.Range(BoomPitchMin, BoomPitchMax);
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = BoomCutoff;
            audio.PlayOneShot(clip, BoomVolume * AudioOptions.Scene);
            Destroy(go, clip.length / audio.pitch + 0.1f);
        }

        void Animate(Boat boat, float time)
        {
            Vector3 c = boat.root.position;
            float half = _length * 0.5f, side = _beam * 0.5f;
            float pivotX = c.x + _pivot;

            float bow = SeaSurface.HeightAt(c.x - half, c.z, time);
            float stern = SeaSurface.HeightAt(c.x + half, c.z, time);
            float near = SeaSurface.HeightAt(pivotX, c.z - side, time);
            float far = SeaSurface.HeightAt(pivotX, c.z + side, time);
            float under = SeaSurface.HeightAt(pivotX, c.z, time);

            float t = boat.age;
            float pitch = Trim + boat.pitchSlow.At(t) + boat.pitchFast.At(t)
                          + Mathf.Atan2(bow - stern, _length) * Mathf.Rad2Deg;
            float roll = boat.roll.At(t) - Mathf.Atan2(near - far, _beam) * Mathf.Rad2Deg;
            float heave = under - SeaSurface.Level + boat.heave.At(t) * _scale;

            boat.motion.localPosition = new Vector3(-_pivot, heave, 0f);
            boat.motion.localRotation = Quaternion.Euler(roll, 0f, pitch);
        }

        static void Release(Boat boat)
        {
            boat.wake.Release();
            boat.spray.Release();
            if (boat.root != null) Destroy(boat.root.gameObject);
        }

        void OnDestroy()
        {
            foreach (Boat boat in _boats) Release(boat);
            _boats.Clear();
            _shells.Clear();
        }
    }
}
