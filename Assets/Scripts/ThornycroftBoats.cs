using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class ThornycroftBoats : MonoBehaviour
    {
        const string ModelResource = "objects/machines/hms_thornycroft";
        const float ProbeY = -9000f;

        const float ScreenLength = 1.8f;
        const float LaneNear = 400f, LaneFar = 480f;
        public const float SpeedMin = 185f, SpeedMax = 225f;
        const float GapMin = 2f, GapMax = 6f;
        const float OnScreenMin = -0.3f, OnScreenMax = 0.5f;
        const float EdgeMargin = 40f;

        const float Heading = 180f;
        const float Draft = 1.15f;
        const float PivotAft = 0.3f;
        const float Trim = 3.5f;

        const float LampForward = 0.85f;
        const float LampHeight = 3.7f;
        const float LampDip = 2.5f;
        const float LampReach = 0.7f;
        const float LampSource = 1f;
        const float LampGlowSize = 0.9f;
        const float LampSoft = 2f;
        const float LampIntensity = 0.55f;
        const float LampGlow = 1.6f;

        const float PitchSlowMin = 0.5f, PitchSlowMax = 0.9f, PitchSlowSecMin = 1.6f, PitchSlowSecMax = 2.4f;
        const float PitchFastMin = 0.2f, PitchFastMax = 0.4f, PitchFastSecMin = 0.6f, PitchFastSecMax = 0.9f;
        const float RollMin = 1f, RollMax = 1.8f, RollSecMin = 2.6f, RollSecMax = 3.6f;
        const float HeaveMin = 0.08f, HeaveMax = 0.16f, HeaveSecMin = 0.8f, HeaveSecMax = 1.3f;

        static GameObject _prefab;
        static Vector3 _center;
        static Vector3 _size;
        static float _waterline;
        static bool _measured;

        class Boat
        {
            public Transform root, motion;
            public BoatWake wake;
            public BoatSpray spray;
            public float speed, age;
            public bool gone;
            public BoatSway pitchSlow, pitchFast, roll, heave;
        }

        readonly List<Boat> _boats = new List<Boat>();
        Camera _cam;
        float _halfViewWidth, _cameraDistance;
        float _scale, _length, _beam, _pivot;
        float _wait;

        public static ThornycroftBoats Begin(Camera cam, float halfViewWidth, float playPlaneZ,
            float cameraDistance, bool wanted)
        {
            if (cam == null || !wanted) return null;

            float scale = ModelScale(playPlaneZ, cameraDistance);
            if (scale <= 0f) return null;

            var boats = new GameObject("Thornycroft Boats").AddComponent<ThornycroftBoats>();
            boats._cam = cam;
            boats._halfViewWidth = halfViewWidth;
            boats._cameraDistance = Mathf.Max(1f, cameraDistance);
            boats._scale = scale;
            boats._length = _size.x * boats._scale;
            boats._beam = _size.z * boats._scale;
            boats._pivot = boats._length * PivotAft;

            boats.Spawn(cam.transform.position, onScreen: true);
            return boats;
        }

        public static float ModelScale(float playPlaneZ, float cameraDistance)
        {
            if (!Measure()) return 0f;

            float laneDistance = (LaneNear + LaneFar) * 0.5f - (playPlaneZ - cameraDistance);
            float plane = PlaneModels.Sopwith.LengthUnits * PlaneModelConfig.LevelScale;
            return ScreenLength * plane * laneDistance / Mathf.Max(1f, cameraDistance) / _size.x;
        }

        static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"ThornycroftBoats: {ModelResource} not found in Resources.");
                return false;
            }

            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("Thornycroft Probe");
            holder.transform.position = origin;
            var view = Instantiate(prefab, holder.transform, false);

            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError($"ThornycroftBoats: {ModelResource} has no renderers.");
                Destroy(holder);
                return false;
            }

            Bounds box = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);

            _center = box.center - origin;
            _size = box.size;
            _waterline = box.min.y - origin.y + Draft;
            _prefab = prefab;

            Destroy(holder);
            return true;
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
                if (!boat.gone) sailing = true;
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

            var root = new GameObject("Thornycroft").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(new Vector3(x, SeaSurface.Level, z),
                Quaternion.Euler(0f, Heading, 0f));

            var motion = new GameObject("Motion").transform;
            motion.SetParent(root, false);

            var holder = new GameObject("Model").transform;
            holder.SetParent(motion, false);
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
            MountLamp(motion, window);

            var boat = new Boat
            {
                root = root,
                motion = motion,
                wake = new BoatWake(transform, _length, _beam),
                spray = new BoatSpray(transform, _scale, _length, _beam),
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

        void MountLamp(Transform motion, float window)
        {
            var spot = new Vector3(_pivot + LampForward * _scale, (LampHeight - Draft) * _scale, 0f);
            Searchlight lamp = Searchlight.Mount(motion, spot);
            if (lamp == null) return;

            float dip = LampDip * Mathf.Deg2Rad;
            lamp.aim = new Vector3(Mathf.Cos(dip), -Mathf.Sin(dip), 0f);
            lamp.sweep = 0f;
            lamp.length = window * LampReach;
            lamp.sourceRadius = LampSource * _scale;
            lamp.glowSize = LampGlowSize * _scale;
            lamp.soft = LampSoft * _scale;
            lamp.intensity = LampIntensity;
            lamp.glowIntensity = LampGlow;
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
        }
    }
}
