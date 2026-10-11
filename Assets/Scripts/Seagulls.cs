using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class Seagulls : MonoBehaviour
    {
        const string ModelResource = "objects/seagull";
        const string BeakNode = "Beak";
        const float ProbeY = -9000f;

        const float SizeBoost = 3f;
        const float SizeJitter = 0.1f;

        const float DepthNear = 85f, DepthFar = 650f;
        const float SpreadZ = 25f;
        const float CompanionGap = 60f;

        const float Floor = 45f;
        const float AltitudeMin = 330f, AltitudeMax = 520f;

        const float SpeedMin = 70f, SpeedMax = 105f;

        const float IntervalMin = 8f, IntervalMax = 20f;
        const float OnScreenMin = 0f, OnScreenMax = 0.5f;
        const float EdgeMargin = 40f;

        const float ClusterX = 1.6f, ClusterY = 0.9f;
        const float MinGap = 1.6f;
        const int Candidates = 24;

        const float Heading = -90f;
        const float FlockYaw = 10f, GullYaw = 15f;
        const float BankMin = 22f, BankMax = 38f;
        const float GullPitch = 8f;
        const float PitchFollow = 0.7f;

        const float RiseMin = 8f, RiseMax = 18f, RiseSecMin = 7f, RiseSecMax = 12f;
        const float BobMin = 2f, BobMax = 4f, BobSecMin = 3f, BobSecMax = 5.5f;
        const float DriftMin = 4f, DriftMax = 10f, DriftSecMin = 5f, DriftSecMax = 10f;
        const float SwayMin = 3f, SwayMax = 6f, SwaySecMin = 2.5f, SwaySecMax = 5f;
        const float TurnMin = 2f, TurnMax = 4f, TurnSecMin = 4f, TurnSecMax = 8f;

        static GameObject _prefab;
        static Vector3 _center;
        static float _flip;
        static float _wingspan;
        static bool _measured;

        struct Wave
        {
            public float amp, rate, phase;

            public static Wave Roll(float ampMin, float ampMax, float secMin, float secMax) =>
                new Wave
                {
                    amp = Random.Range(ampMin, ampMax),
                    rate = 2f * Mathf.PI / Random.Range(secMin, secMax),
                    phase = Random.Range(0f, 2f * Mathf.PI),
                };

            public float At(float t) => amp * Mathf.Sin(t * rate + phase);

            public float Slope(float t) => amp * rate * Mathf.Cos(t * rate + phase);
        }

        class Gull
        {
            public Transform tr;
            public Vector3 offset;
            public float yaw, bank, pitch;
            public Wave bob, drift, sway, turn;
        }

        class Flock
        {
            public readonly List<Gull> gulls = new List<Gull>();
            public Vector3 start;
            public float speed, age, reach, halfWindow;
            public Wave rise;

            public Vector3 Center =>
                new Vector3(start.x - speed * age, start.y + rise.At(age), start.z);
        }

        readonly List<Flock> _flocks = new List<Flock>();
        Camera _cam;
        float _halfViewWidth, _playPlaneZ, _cameraDistance;
        float _wait;

        public static Seagulls Begin(Camera cam, float halfViewWidth, float playPlaneZ,
            float cameraDistance, bool wanted)
        {
            if (cam == null || !wanted || !Measure()) return null;

            var gulls = new GameObject("Seagulls").AddComponent<Seagulls>();
            gulls._cam = cam;
            gulls._halfViewWidth = halfViewWidth;
            gulls._playPlaneZ = playPlaneZ;
            gulls._cameraDistance = Mathf.Max(1f, cameraDistance);
            gulls._wait = Random.Range(IntervalMin, IntervalMax);

            gulls.Spawn(cam.transform.position, onScreen: true);
            return gulls;
        }

        static bool Measure()
        {
            if (_measured) return _prefab != null;
            _measured = true;

            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"Seagulls: {ModelResource} not found in Resources.");
                return false;
            }

            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("Seagull Probe");
            holder.transform.position = origin;
            var view = Instantiate(prefab, holder.transform, false);

            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError($"Seagulls: {ModelResource} has no renderers.");
                Destroy(holder);
                return false;
            }

            Bounds box = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);

            Transform beak = PlaneFactory.FindDeep(view.transform, BeakNode);
            _flip = beak != null && beak.position.z < box.center.z ? 180f : 0f;
            _center = box.center - origin;
            _wingspan = box.size.x;
            _prefab = prefab;

            Destroy(holder);
            return true;
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            Vector3 eye = _cam.transform.position;
            float dt = Time.deltaTime;

            for (int i = _flocks.Count - 1; i >= 0; i--)
            {
                Flock flock = _flocks[i];
                flock.age += dt;

                if (flock.Center.x + flock.reach < eye.x - flock.halfWindow - EdgeMargin)
                {
                    Release(flock);
                    _flocks.RemoveAt(i);
                    continue;
                }

                Place(flock);
            }

            _wait -= dt;
            if (_wait > 0f) return;

            _wait = Random.Range(IntervalMin, IntervalMax);
            Spawn(eye, onScreen: false);
        }

        void Spawn(Vector3 eye, bool onScreen)
        {
            var flock = new Flock
            {
                speed = Random.Range(SpeedMin, SpeedMax),
                rise = Wave.Roll(RiseMin, RiseMax, RiseSecMin, RiseSecMax),
            };

            float z = _playPlaneZ + PickDepth();
            flock.halfWindow = _halfViewWidth * (z - eye.z) / _cameraDistance;

            float span = _wingspan * PlaneModelConfig.UnitsPerMeter * SizeBoost;
            List<Vector2> spots = Scatter(RollCount(), span);

            float lowest = 0f, widest = 0f;
            foreach (Vector2 spot in spots)
            {
                lowest = Mathf.Min(lowest, spot.y);
                widest = Mathf.Max(widest, Mathf.Abs(spot.x));
            }
            flock.reach = widest + span + DriftMax;

            float floor = SeaSurface.Level + Floor - lowest + flock.rise.amp + BobMax;
            float x = onScreen
                ? eye.x + flock.halfWindow * Random.Range(OnScreenMin, OnScreenMax)
                : eye.x + flock.halfWindow + flock.reach + EdgeMargin;
            flock.start = new Vector3(x, Mathf.Max(PickAltitude(), floor), z);

            float yaw = Random.Range(-FlockYaw, FlockYaw);
            float side = Random.value < 0.5f ? -1f : 1f;
            foreach (Vector2 spot in spots)
                flock.gulls.Add(Hatch(
                    new Vector3(spot.x, spot.y, Random.Range(-SpreadZ, SpreadZ)), yaw, side));

            Place(flock);
            _flocks.Add(flock);
        }

        Gull Hatch(Vector3 offset, float flockYaw, float side)
        {
            var root = new GameObject("Seagull").transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * (PlaneModelConfig.UnitsPerMeter * SizeBoost
                                             * Random.Range(1f - SizeJitter, 1f + SizeJitter));

            Transform view = Instantiate(_prefab, root, false).transform;
            view.name = _prefab.name;
            Quaternion flip = Quaternion.Euler(0f, _flip, 0f);
            view.localPosition = flip * (view.localPosition - _center);
            view.localRotation = flip * view.localRotation;

            foreach (Collider col in view.GetComponentsInChildren<Collider>())
                Destroy(col);

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            NightReceivers.Adopt(view.gameObject);

            return new Gull
            {
                tr = root,
                offset = offset,
                yaw = flockYaw + Random.Range(-GullYaw, GullYaw),
                bank = side * Random.Range(BankMin, BankMax),
                pitch = Random.Range(-GullPitch, GullPitch),
                bob = Wave.Roll(BobMin, BobMax, BobSecMin, BobSecMax),
                drift = Wave.Roll(DriftMin, DriftMax, DriftSecMin, DriftSecMax),
                sway = Wave.Roll(SwayMin, SwayMax, SwaySecMin, SwaySecMax),
                turn = Wave.Roll(TurnMin, TurnMax, TurnSecMin, TurnSecMax),
            };
        }

        static void Place(Flock flock)
        {
            float t = flock.age;
            Vector3 center = flock.Center;
            float rise = flock.rise.Slope(t);

            foreach (Gull gull in flock.gulls)
            {
                if (gull.tr == null) continue;

                Vector3 pos = center + gull.offset
                              + new Vector3(gull.drift.At(t), gull.bob.At(t), 0f);
                float climb = rise + gull.bob.Slope(t);
                float pitch = gull.pitch
                              + PitchFollow * Mathf.Atan2(climb, flock.speed) * Mathf.Rad2Deg;
                float bank = gull.bank + gull.sway.At(t);
                float yaw = Heading + gull.yaw + gull.turn.At(t);

                gull.tr.SetPositionAndRotation(pos, Quaternion.Euler(-pitch, yaw, bank));
            }
        }

        static void Release(Flock flock)
        {
            foreach (Gull gull in flock.gulls)
                if (gull.tr != null) Destroy(gull.tr.gameObject);
        }

        static List<Vector2> Scatter(int count, float span)
        {
            float spread = Mathf.Sqrt(count);
            var reach = new Vector2(span * ClusterX * spread, span * ClusterY * spread);
            float gap = span * MinGap;
            var spots = new List<Vector2>(count);

            for (int i = 0; i < count; i++)
            {
                Vector2 best = Vector2.zero;
                float bestGap = -1f;

                for (int k = 0; k < Candidates && bestGap < gap; k++)
                {
                    Vector2 candidate = Vector2.Scale(Random.insideUnitCircle, reach);
                    float nearest = float.MaxValue;
                    foreach (Vector2 spot in spots)
                        nearest = Mathf.Min(nearest, Vector2.Distance(spot, candidate));

                    if (nearest <= bestGap) continue;
                    best = candidate;
                    bestGap = nearest;
                }

                spots.Add(best);
            }

            return spots;
        }

        static int RollCount()
        {
            float roll = Random.value;
            if (roll < 0.1f) return 1;
            if (roll < 0.22f) return 2;
            if (roll < 0.8f) return Random.Range(3, 8);
            return Random.Range(8, 13);
        }

        static float PickDepth()
        {
            float behind = Random.Range(DepthNear, DepthFar);
            float off = behind - CompanionFlight.Depth;
            if (Mathf.Abs(off) < CompanionGap)
                behind = CompanionFlight.Depth + (off < 0f ? -CompanionGap : CompanionGap);
            return behind;
        }

        static float PickAltitude() =>
            SeaSurface.Level + Random.Range(AltitudeMin, AltitudeMax);
    }
}
