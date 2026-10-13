using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public struct RaidStage
    {
        public Camera cam;
        public Vector3 camera;
        public float distance;
        public float halfViewWidth;
        public float halfViewHeight;
        public float minX, maxX;
        public float floorY, ceilY;
        public float fieldMinX, fieldMaxX;
        public float playZ;
        public float groundY;
        public EnemyZeppelin zeppelin;
        public AerodromeBlaze blaze;
        public System.Action<float> shake;
        public System.Action finished;
    }

    public class ZeppelinRaid : MonoBehaviour
    {
        const float FocusSec = 2f;
        const float GlideSec = 5f;
        const float ZeppelinFrameDrop = 0.2f;

        const int BombCount = 20;
        const float StreamSec = 3f;
        const float StreamJitter = 0.35f;
        const float BombGravity = 160f;
        const float BombScale = 1.4f;

        const float AimReach = 0.8f;
        const float AimJitter = 0.3f;
        const float MinAimSpan = 300f;
        const float DepthLead = 20f;
        const float MaxDepth = 450f;
        const float RoofHit = 0.8f;

        const float BlastMin = 70f, BlastMax = 95f;
        const float KillShare = 0.6f;
        const float ShakePerBlast = 0.7f;

        const float DescentSec = 3.4f;
        const float FieldLift = 20f;
        const float HangSec = 5f;
        const float PushIn = 0.15f;
        const float PushSec = 9f;

        const float DriftSpeed = 14f;

        class Aim
        {
            public Vector3 point;
            public readonly List<int> targets = new List<int>();
        }

        RaidStage _stage;
        float _focusX;
        int _falling;
        bool _dropped;

        public Vector3 CameraPosition { get; private set; }

        public static ZeppelinRaid Begin(RaidStage stage)
        {
            var raid = new GameObject("Zeppelin Raid").AddComponent<ZeppelinRaid>();
            raid._stage = stage;
            raid.CameraPosition = stage.camera;
            raid._focusX = raid.FocusX();

            if (stage.zeppelin != null) stage.zeppelin.Raid();
            raid.StartCoroutine(raid.Play());
            return raid;
        }

        float FocusX()
        {
            float from = _stage.fieldMinX;
            float to = _stage.fieldMaxX;

            if (BuildingSpan(out float min, out float max))
            {
                from = Mathf.Max(from, min);
                to = Mathf.Min(to, max);
            }

            if (to < from)
            {
                from = _stage.fieldMinX;
                to = _stage.fieldMaxX;
            }

            return Mathf.Clamp((from + to) * 0.5f, _stage.minX, _stage.maxX);
        }

        bool BuildingSpan(out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            if (_stage.blaze == null || _stage.blaze.Count == 0) return false;

            for (int i = 0; i < _stage.blaze.Count; i++)
            {
                Bounds footprint = _stage.blaze.Footprint(i);
                min = Mathf.Min(min, footprint.min.x);
                max = Mathf.Max(max, footprint.max.x);
            }
            return true;
        }

        Vector3 ZeppelinShot()
        {
            Vector3 target = _stage.zeppelin != null
                ? _stage.zeppelin.transform.position
                : new Vector3(_focusX, _stage.ceilY, 0f);

            return new Vector3(
                Mathf.Clamp(target.x, _stage.minX, _stage.maxX),
                Mathf.Clamp(target.y - _stage.halfViewHeight * ZeppelinFrameDrop,
                    _stage.floorY, _stage.ceilY),
                _stage.camera.z);
        }

        Vector3 FieldShot(float push) => new Vector3(
            _focusX,
            Mathf.Clamp(_stage.floorY + FieldLift, _stage.floorY, _stage.ceilY),
            _stage.camera.z + _stage.distance * PushIn * push);

        IEnumerator Play()
        {
            Vector3 from = CameraPosition;
            float t = 0f;
            while (t < FocusSec)
            {
                t += Time.deltaTime;
                CameraPosition = Vector3.Lerp(from, ZeppelinShot(),
                    Mathf.SmoothStep(0f, 1f, t / FocusSec));
                yield return null;
            }

            if (_stage.zeppelin != null) _stage.zeppelin.Glide(_focusX, GlideSec);
            t = 0f;
            while (t < GlideSec)
            {
                t += Time.deltaTime;
                CameraPosition = ZeppelinShot();
                yield return null;
            }

            StartCoroutine(Drop());

            from = CameraPosition;
            t = 0f;
            while (t < DescentSec)
            {
                t += Time.deltaTime;
                CameraPosition = Vector3.Lerp(from, FieldShot(0f),
                    Mathf.SmoothStep(0f, 1f, t / DescentSec));
                yield return null;
            }

            StartCoroutine(Push());
            while (!_dropped || _falling > 0) yield return null;

            yield return Wait(HangSec);
            _stage.finished?.Invoke();
        }

        IEnumerator Push()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / PushSec);
                CameraPosition = FieldShot(1f - k * k);
                yield return null;
            }
        }

        IEnumerator Drop()
        {
            if (_stage.zeppelin == null)
            {
                _dropped = true;
                yield break;
            }

            List<Aim> aims = Aims();
            GameObject prop = Bomb.BuildProp();
            float gap = StreamSec / Mathf.Max(1, aims.Count - 1);

            for (int i = 0; i < aims.Count; i++)
            {
                Aim aim = aims[i];
                float along = aims.Count > 1 ? i / (aims.Count - 1f) : 0.5f;
                Vector3 from = _stage.zeppelin.BayPoint(along);

                _falling++;
                RaidBomb.Drop(prop, from, aim.point, BombGravity, BombScale,
                    point => Land(aim, point));

                if (i < aims.Count - 1)
                    yield return Wait(gap * Random.Range(1f - StreamJitter, 1f + StreamJitter));
            }

            Destroy(prop);
            _dropped = true;
            _stage.zeppelin.DriftAway(DriftSpeed);
        }

        List<Aim> Aims()
        {
            float half = _stage.halfViewWidth * AimReach;
            float from = _focusX - half;
            float to = _focusX + half;

            if (BuildingSpan(out float min, out float max))
            {
                from = Mathf.Max(from, min);
                to = Mathf.Min(to, max);
            }

            if (to - from < MinAimSpan)
            {
                from = _focusX - MinAimSpan * 0.5f;
                to = _focusX + MinAimSpan * 0.5f;
            }

            float nearZ = _stage.playZ + DepthLead;
            float farZ = nearZ + MaxDepth;

            var aims = new List<Aim>();
            for (int i = 0; i < BombCount; i++)
            {
                float k = (i + 0.5f + Random.Range(-AimJitter, AimJitter)) / BombCount;
                float x = Mathf.Lerp(to, from, k);

                int roof = RoofUnder(x, nearZ, farZ);
                Vector3 point;
                if (roof >= 0)
                {
                    Bounds footprint = _stage.blaze.Footprint(roof);
                    point = new Vector3(x, footprint.min.y + footprint.size.y * RoofHit,
                        footprint.center.z);
                }
                else
                {
                    point = new Vector3(x, _stage.groundY, Random.Range(nearZ, farZ));
                }

                aims.Add(new Aim { point = point });
            }

            AssignTargets(aims);
            return aims;
        }

        int RoofUnder(float x, float nearZ, float farZ)
        {
            if (_stage.blaze == null) return -1;

            var under = new List<int>();
            for (int i = 0; i < _stage.blaze.Count; i++)
            {
                Bounds footprint = _stage.blaze.Footprint(i);
                if (footprint.center.z < nearZ || footprint.center.z > farZ) continue;
                if (x >= footprint.min.x && x <= footprint.max.x) under.Add(i);
            }

            return under.Count > 0 ? under[Random.Range(0, under.Count)] : -1;
        }

        void AssignTargets(List<Aim> aims)
        {
            if (_stage.blaze == null || aims.Count == 0) return;

            for (int i = 0; i < _stage.blaze.Count; i++)
            {
                Vector3 centre = _stage.blaze.Footprint(i).center;
                Aim nearest = aims[0];
                float best = float.MaxValue;

                foreach (Aim aim in aims)
                {
                    float dx = aim.point.x - centre.x;
                    float dz = aim.point.z - centre.z;
                    float distance = dx * dx + dz * dz;
                    if (distance >= best) continue;

                    best = distance;
                    nearest = aim;
                }

                nearest.targets.Add(i);
            }
        }

        void Land(Aim aim, Vector3 point)
        {
            _falling--;

            float size = Random.Range(BlastMin, BlastMax);
            Vector3 listener = _stage.cam != null ? _stage.cam.transform.position : point;

            Explosion.Spawn(point, size);
            GroundBlast.Spawn(point, size, listener);
            if (Battlefield.Current != null)
                Battlefield.Current.KillPeopleWithin(point, size * KillShare);

            if (_stage.blaze != null)
                foreach (int target in aim.targets) _stage.blaze.Ignite(target);

            _stage.shake?.Invoke(ShakePerBlast);
        }

        static IEnumerator Wait(float seconds)
        {
            float left = seconds;
            while (left > 0f)
            {
                left -= Time.deltaTime;
                yield return null;
            }
        }
    }
}
