using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class LordCliveShips : MonoBehaviour
    {
        const string ModelResource = "objects/machines/hms_lord_clive";
        const string TurretNode = "Turret_12in_Gunhouse";
        const float ProbeY = -9000f;

        const float LaneNear = 840f, LaneFar = 950f;
        const float SpeedShare = 0.15f;
        const float SpeedMin = ThornycroftBoats.SpeedMin * SpeedShare;
        const float SpeedMax = ThornycroftBoats.SpeedMax * SpeedShare;
        const float GapMin = 2f, GapMax = 6f;
        const float OnScreenMin = -0.3f, OnScreenMax = 0.5f;
        const float EdgeMargin = 40f;

        const float Heading = 180f;
        const float Draft = 5.1f;
        const float BowWave = 0.47f;
        const float WakeRun = 0.4f, WakeStrength = 0.6f;

        const float PitchSlowMin = 0.12f, PitchSlowMax = 0.25f, PitchSlowSecMin = 5f, PitchSlowSecMax = 7f;
        const float PitchFastMin = 0.04f, PitchFastMax = 0.08f, PitchFastSecMin = 2f, PitchFastSecMax = 3f;
        const float RollMin = 0.4f, RollMax = 0.8f, RollSecMin = 7f, RollSecMax = 9f;
        const float HeaveMin = 0.04f, HeaveMax = 0.08f, HeaveSecMin = 3f, HeaveSecMax = 4f;

        const float ScreenInset = 0.04f;
        const float RetryMin = 0.4f, RetryMax = 2f;
        const float RetargetChance = 0.5f;

        struct GunSpec
        {
            public string traverse, barrels, twin;
            public float restLift, arc, liftMin, liftMax;
            public float yawSpeed, liftSpeed, yawStep, liftStep;
            public float intervalMin, intervalMax, flashMeters, smokeMeters;
            public float volume, pitchMin, pitchMax, cutoff;
        }

        static readonly GunSpec[] Guns =
        {
            new GunSpec
            {
                traverse = "Turret_12in_Gunhouse", barrels = "Gun_12in_Port", twin = "Gun_12in_Starboard",
                restLift = 0f, arc = 135f, liftMin = 8f, liftMax = 28f,
                yawSpeed = 6f, liftSpeed = 3f, yawStep = 35f, liftStep = 8f,
                intervalMin = 5f, intervalMax = 8f, flashMeters = 22f, smokeMeters = 16f,
                volume = 0.13f, pitchMin = 0.42f, pitchMax = 0.5f, cutoff = 700f,
            },
            new GunSpec
            {
                barrels = "Gun_18in",
                restLift = 10.5f, liftMin = 18f, liftMax = 40f,
                liftSpeed = 2f, liftStep = 10f,
                intervalMin = 10f, intervalMax = 15f, flashMeters = 30f, smokeMeters = 22f,
                volume = 0.17f, pitchMin = 0.32f, pitchMax = 0.38f, cutoff = 500f,
            },
        };

        static GameObject _prefab;
        static Vector3 _center;
        static Vector3 _size;
        static float _waterline;
        static float _yaw;
        static bool _measured;

        class Gun
        {
            public GunSpec spec;
            public Transform yaw, lift;
            public Quaternion yawRest, liftRest;
            public Vector3[] muzzles;
            public Vector3 axis;
            public float facing;
            public float yawNow, liftNow, yawGoal, liftGoal, wait;
        }

        class Ship
        {
            public Transform root, motion;
            public BoatWake wake;
            public List<Gun> guns;
            public float speed, age;
            public bool gone;
            public BoatSway pitchSlow, pitchFast, roll, heave;
        }

        readonly List<Ship> _ships = new List<Ship>();
        Camera _cam;
        float _halfViewWidth, _cameraDistance;
        float _scale, _length, _beam;
        float _wait;

        public static LordCliveShips Begin(Camera cam, float halfViewWidth, float playPlaneZ,
            float cameraDistance, bool wanted)
        {
            if (cam == null || !wanted || !Measure()) return null;

            float scale = ThornycroftBoats.ModelScale(playPlaneZ, cameraDistance);
            if (scale <= 0f) return null;

            var ships = new GameObject("Lord Clive Ships").AddComponent<LordCliveShips>();
            ships._cam = cam;
            ships._halfViewWidth = halfViewWidth;
            ships._cameraDistance = Mathf.Max(1f, cameraDistance);
            ships._scale = scale;
            ships._length = _size.x * scale;
            ships._beam = _size.z * scale;

            ships.Spawn(cam.transform.position, onScreen: true);
            return ships;
        }

        static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"LordCliveShips: {ModelResource} not found in Resources.");
                return false;
            }

            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("Lord Clive Probe");
            holder.transform.position = origin;
            var view = Instantiate(prefab, holder.transform, false);

            Transform turret = Find(view.transform, TurretNode);
            if (!Box(view, out Bounds box) || turret == null)
            {
                Debug.LogError($"LordCliveShips: {ModelResource} has no hull or {TurretNode}.");
                Destroy(holder);
                return false;
            }

            Vector3 bow = turret.position - box.center;
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

            for (int i = _ships.Count - 1; i >= 0; i--)
            {
                Ship ship = _ships[i];
                ship.age += dt;
                ship.root.position += Vector3.left * (ship.speed * dt);

                Vector3 pos = ship.root.position;
                float left = eye.x - HalfWindow(pos.z, eye) - EdgeMargin;

                if (!ship.gone && pos.x + half < left)
                {
                    ship.gone = true;
                    _wait = Random.Range(GapMin, GapMax);
                }

                if (ship.gone && pos.x + ship.wake.Reach < left)
                {
                    Release(ship);
                    _ships.RemoveAt(i);
                    continue;
                }

                Animate(ship, time);
                ship.wake.Follow(pos, time);
                foreach (Gun gun in ship.guns)
                {
                    Aim(gun, dt);
                    Fire(gun, dt);
                }
                if (!ship.gone) sailing = true;
            }

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

            var root = new GameObject("Lord Clive").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(new Vector3(x, SeaSurface.Level, z),
                Quaternion.Euler(0f, Heading, 0f));

            var motion = new GameObject("Motion").transform;
            motion.SetParent(root, false);

            var holder = new GameObject("Model").transform;
            holder.SetParent(motion, false);
            holder.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            holder.localScale = Vector3.one * _scale;
            holder.localPosition = -_scale * new Vector3(_center.x, _waterline, _center.z);

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

            var ship = new Ship
            {
                root = root,
                motion = motion,
                wake = new BoatWake(transform, _length, _beam, BowWave, WakeRun, WakeStrength),
                guns = Arm(view.transform, root),
                speed = Random.Range(SpeedMin, SpeedMax),
                pitchSlow = BoatSway.Roll(PitchSlowMin, PitchSlowMax, PitchSlowSecMin, PitchSlowSecMax),
                pitchFast = BoatSway.Roll(PitchFastMin, PitchFastMax, PitchFastSecMin, PitchFastSecMax),
                roll = BoatSway.Roll(RollMin, RollMax, RollSecMin, RollSecMax),
                heave = BoatSway.Roll(HeaveMin, HeaveMax, HeaveSecMin, HeaveSecMax),
            };

            float time = Time.time;
            Animate(ship, time);
            ship.wake.Follow(root.position, time);
            _ships.Add(ship);
        }

        static List<Gun> Arm(Transform view, Transform root)
        {
            var guns = new List<Gun>();
            foreach (GunSpec spec in Guns)
            {
                Transform barrels = Find(view, spec.barrels);
                Renderer body = barrels != null ? barrels.GetComponentInChildren<Renderer>() : null;
                if (body == null) continue;

                Bounds box = body.bounds;
                Vector3 a = root.InverseTransformPoint(box.min);
                Vector3 b = root.InverseTransformPoint(box.max);
                Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);

                Vector3 pivot = root.InverseTransformPoint(barrels.position);
                Transform twin = spec.twin != null ? Find(view, spec.twin) : null;
                float twinZ = twin != null ? root.InverseTransformPoint(twin.position).z : pivot.z;
                float midZ = twin != null ? (pivot.z + twinZ) * 0.5f : (min.z + max.z) * 0.5f;

                float facing = (min.x + max.x) * 0.5f >= pivot.x ? 1f : -1f;
                float reach = Mathf.Abs((facing > 0f ? max.x : min.x) - pivot.x);
                float rest = spec.restLift * Mathf.Deg2Rad;
                var tip = new Vector3(pivot.x + facing * reach, pivot.y + Mathf.Tan(rest) * reach, 0f);

                Transform yaw = null;
                Transform house = spec.traverse != null ? Find(view, spec.traverse) : null;
                if (house != null)
                {
                    yaw = new GameObject(spec.barrels + " Traverse").transform;
                    yaw.SetParent(house.parent, false);
                    yaw.SetPositionAndRotation(house.position, root.rotation);
                    house.SetParent(yaw, true);
                }

                var lift = new GameObject(spec.barrels + " Elevation").transform;
                lift.SetParent(yaw != null ? yaw : barrels.parent, false);
                lift.SetPositionAndRotation(root.TransformPoint(new Vector3(pivot.x, pivot.y, midZ)),
                    root.rotation);
                barrels.SetParent(lift, true);

                var muzzles = new List<Vector3>();
                if (twin != null)
                {
                    muzzles.Add(lift.InverseTransformPoint(root.TransformPoint(new Vector3(tip.x, tip.y, pivot.z))));
                    muzzles.Add(lift.InverseTransformPoint(root.TransformPoint(new Vector3(tip.x, tip.y, twinZ))));
                }
                else muzzles.Add(lift.InverseTransformPoint(root.TransformPoint(new Vector3(tip.x, tip.y, midZ))));

                var gun = new Gun
                {
                    spec = spec,
                    yaw = yaw,
                    lift = lift,
                    yawRest = yaw != null ? yaw.localRotation : Quaternion.identity,
                    liftRest = lift.localRotation,
                    muzzles = muzzles.ToArray(),
                    axis = new Vector3(facing * Mathf.Cos(rest), Mathf.Sin(rest), 0f),
                    facing = facing,
                    yawNow = yaw != null ? Random.Range(-spec.arc, spec.arc) : 0f,
                    liftNow = Random.Range(spec.liftMin, spec.liftMax),
                    wait = Random.Range(RetryMin, spec.intervalMax),
                };
                gun.yawGoal = gun.yawNow;
                gun.liftGoal = gun.liftNow;
                Pose(gun);
                guns.Add(gun);
            }
            return guns;
        }

        static void Retarget(Gun gun)
        {
            GunSpec spec = gun.spec;
            if (gun.yaw != null)
                gun.yawGoal = Mathf.Clamp(gun.yawNow + Random.Range(-spec.yawStep, spec.yawStep),
                    -spec.arc, spec.arc);
            gun.liftGoal = Mathf.Clamp(gun.liftNow + Random.Range(-spec.liftStep, spec.liftStep),
                spec.liftMin, spec.liftMax);
        }

        static void Aim(Gun gun, float dt)
        {
            gun.yawNow = Mathf.MoveTowards(gun.yawNow, gun.yawGoal, gun.spec.yawSpeed * dt);
            gun.liftNow = Mathf.MoveTowards(gun.liftNow, gun.liftGoal, gun.spec.liftSpeed * dt);
            Pose(gun);
        }

        static void Pose(Gun gun)
        {
            if (gun.yaw != null) gun.yaw.localRotation = gun.yawRest * Quaternion.Euler(0f, gun.yawNow, 0f);
            gun.lift.localRotation = gun.liftRest
                * Quaternion.AngleAxis(gun.facing * (gun.liftNow - gun.spec.restLift), Vector3.forward);
        }

        void Fire(Gun gun, float dt)
        {
            gun.wait -= dt;
            if (gun.wait > 0f || gun.yawNow != gun.yawGoal || gun.liftNow != gun.liftGoal) return;

            Vector3 first = gun.lift.TransformPoint(gun.muzzles[0]);
            if (!Seen(first))
            {
                gun.wait = Random.Range(RetryMin, RetryMax);
                return;
            }

            GunSpec spec = gun.spec;
            Vector3 dir = gun.lift.TransformDirection(gun.axis);
            foreach (Vector3 local in gun.muzzles)
            {
                Vector3 muzzle = gun.lift.TransformPoint(local);
                MuzzleFlash.Spawn(muzzle, dir, spec.flashMeters * _scale);
                MuzzleSmoke.Spawn(muzzle, dir, spec.smokeMeters * _scale);
            }
            Boom(first, spec);

            gun.wait = Random.Range(spec.intervalMin, spec.intervalMax);
            if (Random.value < RetargetChance) Retarget(gun);
        }

        bool Seen(Vector3 point)
        {
            Vector3 vp = _cam.WorldToViewportPoint(point);
            return vp.z > 0f
                   && vp.x > ScreenInset && vp.x < 1f - ScreenInset
                   && vp.y > ScreenInset && vp.y < 1f - ScreenInset;
        }

        static void Boom(Vector3 position, GunSpec spec)
        {
            var clip = Resources.Load<AudioClip>($"Sounds/explosion_{Random.Range(1, 4)}");
            if (clip == null) return;

            var go = new GameObject("Lord Clive Gun Sound");
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.pitch = Random.Range(spec.pitchMin, spec.pitchMax);
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = spec.cutoff;
            audio.PlayOneShot(clip, spec.volume * AudioOptions.Scene);
            Destroy(go, clip.length / audio.pitch + 0.1f);
        }

        void Animate(Ship ship, float time)
        {
            Vector3 c = ship.root.position;
            float half = _length * 0.5f, side = _beam * 0.5f;

            float bow = SeaSurface.HeightAt(c.x - half, c.z, time);
            float stern = SeaSurface.HeightAt(c.x + half, c.z, time);
            float near = SeaSurface.HeightAt(c.x, c.z - side, time);
            float far = SeaSurface.HeightAt(c.x, c.z + side, time);
            float under = SeaSurface.HeightAt(c.x, c.z, time);

            float t = ship.age;
            float pitch = ship.pitchSlow.At(t) + ship.pitchFast.At(t)
                          + Mathf.Atan2(bow - stern, _length) * Mathf.Rad2Deg;
            float roll = ship.roll.At(t) - Mathf.Atan2(near - far, _beam) * Mathf.Rad2Deg;
            float heave = under - SeaSurface.Level + ship.heave.At(t) * _scale;

            ship.motion.localPosition = new Vector3(0f, heave, 0f);
            ship.motion.localRotation = Quaternion.Euler(roll, 0f, pitch);
        }

        static void Release(Ship ship)
        {
            ship.wake.Release();
            if (ship.root != null) Destroy(ship.root.gameObject);
        }

        void OnDestroy()
        {
            foreach (Ship ship in _ships) Release(ship);
            _ships.Clear();
        }
    }
}
