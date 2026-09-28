using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class SmokeFlyby : MonoBehaviour
    {
        public const float Seconds = VeilEnd;

        const float SpeedFactor = 2.4f;
        const float Depth = -50f;
        const float DepthJitter = 14f;
        const float EntryMargin = 0.3f;
        const float ExitMargin = 0.4f;
        const float WobbleHz = 1.3f;
        const float TailOffset = 0.08f;

        static readonly float[] Lanes = { 0.08f, 0.64f, -0.58f };
        static readonly float[] Delays = { 0f, 0.3f, 0.55f };
        static readonly float[] Wobble = { 0.03f, 0.04f, 0.03f };

        const float PuffStep = 0.07f;
        const float PuffStart = 0.14f;
        const float PuffEndMin = 0.7f, PuffEndMax = 0.95f;
        const float PuffGrowSec = 1.9f;
        const float PuffDrift = 0.05f;
        const float PuffRise = 0.03f;
        const float PuffSpin = 12f;
        const float TrailJitter = 0.04f;

        const float FillStart = 1.2f;
        const float FillSec = 2f;
        const int FillColumns = 8, FillRows = 5;
        const float FillReach = 1.08f;
        const float FillSize = 1.25f;

        const float DarkenStart = 0.8f, DarkenEnd = 4.4f;
        const float VeilStart = 3f, VeilEnd = 4.8f;
        const int VeilOrder = 140;

        static readonly Color Smoke = new Color(0.22f, 0.22f, 0.23f, 0.9f);
        static readonly Color Soot = new Color(0.04f, 0.04f, 0.045f, 0.97f);
        static readonly Color VeilColor = new Color(0.03f, 0.03f, 0.035f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        class Flyer
        {
            public Transform tr;
            public float lane;
            public float wobble;
            public float delay;
            public float x;
            public float lastPuff;
        }

        class Puff
        {
            public Transform tr;
            public float age;
            public float from;
            public float to;
            public Vector3 drift;
            public Vector3 spinAxis;
        }

        readonly List<Flyer> _flyers = new List<Flyer>();
        readonly List<Puff> _puffs = new List<Puff>();
        readonly List<Vector2> _fill = new List<Vector2>();

        Material _mat;
        Canvas _veilCanvas;
        Image _veil;
        float _hw, _hh;
        float _z;
        float _speed;
        float _time;
        int _filled;

        public bool Covered => _time >= VeilEnd;

        public static SmokeFlyby Begin(PlaneModelConfig plane, Vector3 camPos, float halfViewWidth,
            float halfViewHeight, float cameraDistance, float playPlaneZ, float speed)
        {
            var flyby = new GameObject("Smoke Flyby").AddComponent<SmokeFlyby>();

            float k = cameraDistance > 1f ? (cameraDistance + Depth) / cameraDistance : 1f;
            flyby._hw = halfViewWidth * k;
            flyby._hh = halfViewHeight * k;
            flyby._z = playPlaneZ + Depth;
            flyby._speed = speed * SpeedFactor * k;
            flyby._mat = CloudSystem.CloudMaterial(Smoke, Color.black, Smoke.a);

            flyby.Follow(camPos);
            flyby.BuildVeil();
            flyby.PlanFill();
            for (int i = 0; i < Lanes.Length; i++) flyby.AddFlyer(plane, i);
            return flyby;
        }

        void Follow(Vector3 camPos) => transform.position = new Vector3(camPos.x, camPos.y, 0f);

        void BuildVeil()
        {
            _veilCanvas = UIFactory.CreateCanvas("Smoke Veil");
            _veilCanvas.sortingOrder = VeilOrder;
            _veil = UIFactory.CreateBackground(_veilCanvas.transform, Color.clear);
            _veil.raycastTarget = false;
        }

        void PlanFill()
        {
            float cellW = 2f * FillReach / FillColumns;
            float cellH = 2f * FillReach / FillRows;

            for (int c = 0; c < FillColumns; c++)
            for (int r = 0; r < FillRows; r++)
                _fill.Add(new Vector2(
                    -FillReach + (c + Random.Range(0.2f, 0.8f)) * cellW,
                    -FillReach + (r + Random.Range(0.2f, 0.8f)) * cellH));

            for (int i = _fill.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_fill[i], _fill[j]) = (_fill[j], _fill[i]);
            }
        }

        void AddFlyer(PlaneModelConfig plane, int index)
        {
            var root = new GameObject($"Smoking {plane.resourceName}").transform;
            root.SetParent(transform, false);
            root.localRotation = Quaternion.Euler(0f, 0f, 180f);

            PlaneFactory.BuildPlaneModel(root, plane, mirrored: true, skin: PlaneSkins.Default(plane));
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                collider.enabled = false;

            var flyer = new Flyer
            {
                tr = root,
                lane = Lanes[index],
                wobble = Wobble[index],
                delay = Delays[index],
                x = _hw * (1f + EntryMargin),
            };
            flyer.lastPuff = flyer.x;
            root.localPosition = new Vector3(flyer.x, flyer.lane * _hh, _z);
            _flyers.Add(flyer);
        }

        public void Tick(Vector3 camPos, float dt)
        {
            _time += dt;
            Follow(camPos);

            TickFlyers(dt);
            TickFill();
            TickPuffs(dt);

            float dark = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(DarkenStart, DarkenEnd, _time));
            if (_mat != null) _mat.SetColor(BaseColorId, Color.Lerp(Smoke, Soot, dark));

            float veil = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VeilStart, VeilEnd, _time));
            if (_veil != null)
                _veil.color = new Color(VeilColor.r, VeilColor.g, VeilColor.b, veil);
        }

        void TickFlyers(float dt)
        {
            float step = PuffStep * _hh;

            for (int i = _flyers.Count - 1; i >= 0; i--)
            {
                Flyer flyer = _flyers[i];
                if (_time < flyer.delay) continue;

                flyer.x -= _speed * dt;
                float y = (flyer.lane
                           + Mathf.Sin((_time + flyer.delay * 3f) * WobbleHz) * flyer.wobble) * _hh;
                flyer.tr.localPosition = new Vector3(flyer.x, y, _z);

                while (flyer.lastPuff - flyer.x >= step)
                {
                    flyer.lastPuff -= step;
                    Emit(new Vector2(flyer.lastPuff + TailOffset * _hh,
                            y + Random.Range(-TrailJitter, TrailJitter) * _hh),
                        PuffStart * _hh, Random.Range(PuffEndMin, PuffEndMax) * _hh);
                }

                if (flyer.x >= -_hw * (1f + ExitMargin)) continue;

                Destroy(flyer.tr.gameObject);
                _flyers.RemoveAt(i);
            }
        }

        void TickFill()
        {
            if (_time < FillStart || _filled >= _fill.Count) return;

            float share = Mathf.Clamp01((_time - FillStart) / FillSec);
            int target = Mathf.CeilToInt(share * _fill.Count);

            while (_filled < target)
            {
                Vector2 at = _fill[_filled++];
                Emit(new Vector2(at.x * _hw, at.y * _hh), 0f,
                    Random.Range(PuffEndMin, PuffEndMax) * FillSize * _hh);
            }
        }

        void Emit(Vector2 at, float from, float to)
        {
            var go = new GameObject("Thick Smoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(at.x, at.y,
                _z + Random.Range(-DepthJitter, DepthJitter));
            go.transform.localRotation = Random.rotation;
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, from);

            go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
            var renderer = go.AddComponent<MeshRenderer>();
            if (_mat != null) renderer.sharedMaterial = _mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _puffs.Add(new Puff
            {
                tr = go.transform,
                from = from,
                to = to,
                drift = new Vector3(Random.Range(-PuffDrift, PuffDrift),
                    Random.Range(-PuffDrift, PuffDrift) + PuffRise, 0f) * _hh,
                spinAxis = Random.onUnitSphere,
            });
        }

        void TickPuffs(float dt)
        {
            foreach (Puff puff in _puffs)
            {
                puff.age += dt;
                float t = Mathf.Clamp01(puff.age / PuffGrowSec);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);

                puff.tr.localScale = Vector3.one * Mathf.Max(0.01f, Mathf.Lerp(puff.from, puff.to, ease));
                puff.tr.localPosition += puff.drift * dt;
                puff.tr.Rotate(puff.spinAxis, PuffSpin * dt, Space.World);
            }
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_veilCanvas != null) Destroy(_veilCanvas.gameObject);
        }
    }
}
