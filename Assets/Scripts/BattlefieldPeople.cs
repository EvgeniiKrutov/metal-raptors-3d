using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class BattlefieldPeople : MonoBehaviour
    {
        const float GroupSpacing = 235f;
        const int MaxGroups = 18;
        const int GroupSizeMin = 4, GroupSizeMax = 8;

        const float ZMin = 40f;
        const float ZEdgeMargin = 40f;
        const float ScrollerLeadMin = 500f, ScrollerLeadMax = 1100f;
        const float CullMargin = 460f;
        const float OffCameraMargin = 80f;

        const float Height = 13f;
        const float Width = 4.6f;
        const float Thickness = 3f;
        const float HatFraction = 0.20f;
        const float FaceFraction = 0.18f;

        const float WalkSpeedMin = 6f, WalkSpeedMax = 11f;
        const float GroupDriftSpeed = 4f;
        const float StepRateBase = 2.4f;
        const float StepSpeedReference = 8f;
        const float HopHeight = 0.9f;

        const float MoveTimeMin = 1.5f, MoveTimeMax = 4.5f;
        const float HaltTimeMin = 0.4f, HaltTimeMax = 1.8f;
        const float GroupMoveTimeMin = 3f, GroupMoveTimeMax = 7f;
        const float GroupHaltTimeMin = 1f, GroupHaltTimeMax = 3f;

        const float ResumeTurn = 90f;
        const float WanderRate = 40f;
        const float WanderNoiseRate = 0.25f;
        const float Leash = 42f;
        const float LeashTurnRate = 220f;
        const float SpawnSpreadX = 30f, SpawnSpreadZ = 20f;

        const float PropClearance = 18f;
        const float GroupPropClearance = 34f;
        const float PropTurnRate = 300f;

        static readonly Color[] UniformColors =
        {
            new Color(0.40f, 0.47f, 0.56f),
            new Color(0.36f, 0.39f, 0.32f),
        };

        static readonly Color[] SkinColors =
        {
            new Color(0.78f, 0.62f, 0.48f),
            new Color(0.70f, 0.53f, 0.40f),
            new Color(0.58f, 0.42f, 0.31f),
        };

        static Material[] _uniformMats;
        static Material[] _skinMats;

        class Figure
        {
            public Transform tr;
            public float x, z;
            public float headingDeg;
            public bool moving;
            public float stateTimer;
            public float noisePhase;
            public float speed;
            public float stepPhase;
        }

        class Group
        {
            public GameObject root;
            public int faction;
            public float x, z;
            public float headingDeg;
            public bool moving;
            public float stateTimer;
            public float noisePhase;
            public readonly List<Figure> figures = new List<Figure>();
        }

        readonly List<Group> _groups = new List<Group>();
        Battlefield _field;
        int _targetGroups;

        public static BattlefieldPeople Begin(Battlefield field)
        {
            var go = new GameObject("Battlefield People");
            go.transform.SetParent(field.transform, false);

            var people = go.AddComponent<BattlefieldPeople>();
            people._field = field;
            people._targetGroups = people.TargetGroupCount();

            for (int i = 0; i < people._targetGroups; i++)
                people.SpawnGroup(people.InitialX(i));

            return people;
        }

        float BandMinX => _field.MinX - _field.HalfViewWidth;
        float BandMaxX => _field.MaxX + _field.HalfViewWidth;
        float ZMax => _field.PeopleZMax;

        float ScrollerBehind => _field.HalfViewWidth + CullMargin;
        float ScrollerAhead => _field.HalfViewWidth + ScrollerLeadMax;

        int TargetGroupCount()
        {
            float width = _field.Bounded
                ? BandMaxX - BandMinX
                : ScrollerBehind + ScrollerAhead;
            int cap = Mathf.RoundToInt(MaxGroups * GraphicsOptions.PeopleScale);
            return Mathf.Clamp(Mathf.RoundToInt(width / GroupSpacing), 2, Mathf.Max(2, cap));
        }

        float InitialX(int index)
        {
            if (!_field.Bounded)
                return _field.CameraX + Random.Range(-ScrollerBehind, ScrollerAhead);

            return Mathf.Lerp(BandMinX, BandMaxX, (index + 0.5f) / _targetGroups)
                   + Random.Range(-GroupSpacing, GroupSpacing) * 0.35f;
        }

        public void Tick(float camX, float dt)
        {
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                var group = _groups[i];
                TickGroup(group, dt);
                if (!Expired(group, camX)) continue;

                Destroy(group.root);
                _groups.RemoveAt(i);
            }

            while (_groups.Count < _targetGroups)
                if (SpawnGroup(RespawnX(camX)) == null) break;
        }

        public void KillWithin(Vector3 centre, float radius)
        {
            float radiusSq = radius * radius;

            foreach (var group in _groups)
                for (int i = group.figures.Count - 1; i >= 0; i--)
                {
                    var figure = group.figures[i];
                    if (figure.tr == null) { group.figures.RemoveAt(i); continue; }

                    float dx = figure.x - centre.x;
                    float dz = figure.z - centre.z;
                    if (dx * dx + dz * dz > radiusSq) continue;

                    Destroy(figure.tr.gameObject);
                    group.figures.RemoveAt(i);
                }
        }

        bool Expired(Group group, float camX)
        {
            if (group.figures.Count == 0) return true;
            if (_field.Bounded) return false;

            return group.x < camX - ScrollerBehind || group.x > camX + ScrollerAhead + CullMargin;
        }

        float RespawnX(float camX)
        {
            if (!_field.Bounded)
                return camX + _field.HalfViewWidth + Random.Range(ScrollerLeadMin, ScrollerLeadMax);

            float left = camX - _field.HalfViewWidth - OffCameraMargin;
            float right = camX + _field.HalfViewWidth + OffCameraMargin;
            float leftSpan = Mathf.Max(0f, left - BandMinX);
            float rightSpan = Mathf.Max(0f, BandMaxX - right);

            if (leftSpan + rightSpan <= 0f) return Random.Range(BandMinX, BandMaxX);
            return Random.value * (leftSpan + rightSpan) < leftSpan
                ? Random.Range(BandMinX, left)
                : Random.Range(right, BandMaxX);
        }

        Group SpawnGroup(float x)
        {
            float z = Random.Range(ZMin + ZEdgeMargin, ZMax - ZEdgeMargin);
            if (!_field.SampleGround(x, z, out float groundY)) return null;
            if (_field.Props != null
                && _field.Props.Blocks(x, z, GroupPropClearance, out _)) return null;

            var root = new GameObject("Squad");
            root.transform.SetParent(transform, false);

            var group = new Group
            {
                root = root,
                faction = PickFaction(),
                x = x,
                z = z,
                headingDeg = Random.Range(0f, 360f),
                moving = true,
                stateTimer = Random.Range(GroupMoveTimeMin, GroupMoveTimeMax),
                noisePhase = Random.Range(0f, 100f),
            };

            int count = Mathf.Min(Random.Range(GroupSizeMin, GroupSizeMax + 1),
                GraphicsOptions.PeopleGroupCap);
            for (int i = 0; i < count; i++)
            {
                var tr = BuildFigure(root.transform, group.faction, Random.Range(0, SkinColors.Length));
                float fx = x + Random.Range(-SpawnSpreadX, SpawnSpreadX);
                float fz = Mathf.Clamp(z + Random.Range(-SpawnSpreadZ, SpawnSpreadZ), ZMin, ZMax);

                if (!_field.SampleGround(fx, fz, out float fy)) fy = groundY;
                tr.position = new Vector3(fx, fy, fz);

                group.figures.Add(new Figure
                {
                    tr = tr,
                    x = fx,
                    z = fz,
                    headingDeg = Random.Range(0f, 360f),
                    moving = true,
                    stateTimer = Random.Range(MoveTimeMin, MoveTimeMax),
                    noisePhase = Random.Range(0f, 100f),
                    speed = Random.Range(WalkSpeedMin, WalkSpeedMax),
                    stepPhase = Random.Range(0f, 2f),
                });
            }

            _groups.Add(group);
            return group;
        }

        int PickFaction()
        {
            int first = 0;
            foreach (var group in _groups)
                if (group.faction == 0) first++;
            return first * 2 <= _groups.Count ? 0 : 1;
        }

        void TickGroup(Group group, float dt)
        {
            group.stateTimer -= dt;
            if (group.stateTimer <= 0f)
            {
                group.moving = !group.moving;
                group.stateTimer = group.moving
                    ? Random.Range(GroupMoveTimeMin, GroupMoveTimeMax)
                    : Random.Range(GroupHaltTimeMin, GroupHaltTimeMax);
                if (group.moving) group.headingDeg += Random.Range(-ResumeTurn, ResumeTurn);
            }

            if (group.moving)
            {
                group.headingDeg += Wander(group.noisePhase) * dt;
                Deflect(group.x, group.z, ref group.headingDeg, GroupPropClearance, dt);
                float rad = group.headingDeg * Mathf.Deg2Rad;
                group.x += Mathf.Cos(rad) * GroupDriftSpeed * dt;
                group.z += Mathf.Sin(rad) * GroupDriftSpeed * dt;
                Confine(ref group.x, ref group.z, ref group.headingDeg);
            }

            for (int i = group.figures.Count - 1; i >= 0; i--)
            {
                var figure = group.figures[i];
                if (figure.tr == null) { group.figures.RemoveAt(i); continue; }
                TickFigure(group, figure, dt);
            }
        }

        void TickFigure(Group group, Figure figure, float dt)
        {
            figure.stateTimer -= dt;
            if (figure.stateTimer <= 0f)
            {
                figure.moving = !figure.moving;
                figure.stateTimer = figure.moving
                    ? Random.Range(MoveTimeMin, MoveTimeMax)
                    : Random.Range(HaltTimeMin, HaltTimeMax);
                if (figure.moving) figure.headingDeg += Random.Range(-ResumeTurn, ResumeTurn);
            }

            if (figure.moving)
            {
                figure.headingDeg += Wander(figure.noisePhase) * dt;

                float dx = group.x - figure.x;
                float dz = group.z - figure.z;
                if (dx * dx + dz * dz > Leash * Leash)
                {
                    float toCentre = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
                    figure.headingDeg = Mathf.MoveTowardsAngle(
                        figure.headingDeg, toCentre, LeashTurnRate * dt);
                }

                Deflect(figure.x, figure.z, ref figure.headingDeg, PropClearance, dt);

                float rad = figure.headingDeg * Mathf.Deg2Rad;
                figure.x += Mathf.Cos(rad) * figure.speed * dt;
                figure.z += Mathf.Sin(rad) * figure.speed * dt;
                Confine(ref figure.x, ref figure.z, ref figure.headingDeg);
            }

            if (!_field.SampleGround(figure.x, figure.z, out float y)) return;

            float hop = figure.moving
                ? Mathf.Abs(Mathf.Sin(Mathf.PI * (Time.time * StepRate(figure) + figure.stepPhase)))
                  * HopHeight
                : 0f;
            figure.tr.position = new Vector3(figure.x, y + hop, figure.z);
        }

        void Deflect(float x, float z, ref float headingDeg, float clearance, float dt)
        {
            var props = _field.Props;
            if (props == null) return;
            if (!props.Blocks(x, z, clearance, out Vector2 obstacle)) return;

            float away = Mathf.Atan2(z - obstacle.y, x - obstacle.x) * Mathf.Rad2Deg;
            headingDeg = Mathf.MoveTowardsAngle(headingDeg, away, PropTurnRate * dt);
        }

        void Confine(ref float x, ref float z, ref float headingDeg)
        {
            if (z < ZMin || z > ZMax)
            {
                z = Mathf.Clamp(z, ZMin, ZMax);
                headingDeg = -headingDeg;
            }

            if (!_field.Bounded) return;
            if (x >= BandMinX && x <= BandMaxX) return;

            x = Mathf.Clamp(x, BandMinX, BandMaxX);
            headingDeg = 180f - headingDeg;
        }

        static float StepRate(Figure figure) => StepRateBase * figure.speed / StepSpeedReference;

        static float Wander(float noisePhase) =>
            (Mathf.PerlinNoise(noisePhase, Time.time * WanderNoiseRate) - 0.5f) * 2f * WanderRate;

        static Transform BuildFigure(Transform parent, int faction, int skin)
        {
            var root = new GameObject("Soldier");
            root.transform.SetParent(parent, false);

            float hat = Height * HatFraction;
            float face = Height * FaceFraction;
            float body = Height - hat - face;

            AddSegment(root.transform, UniformMaterial(faction),
                body * 0.5f, Width, body, Thickness);
            AddSegment(root.transform, SkinMaterial(skin),
                body + face * 0.5f, Width * 0.85f, face, Thickness * 0.9f);
            AddSegment(root.transform, UniformMaterial(faction),
                body + face + hat * 0.5f, Width * 1.05f, hat, Thickness * 1.05f);

            return root.transform;
        }

        static void AddSegment(Transform parent, Material mat,
            float centreY, float width, float height, float thickness)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Segment";

            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, centreY, 0f);
            go.transform.localScale = new Vector3(width, height, thickness);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material UniformMaterial(int index)
        {
            if (_uniformMats == null) _uniformMats = BuildMaterials(UniformColors, "Uniform");
            return _uniformMats[Mathf.Clamp(index, 0, _uniformMats.Length - 1)];
        }

        static Material SkinMaterial(int index)
        {
            if (_skinMats == null) _skinMats = BuildMaterials(SkinColors, "Skin");
            return _skinMats[Mathf.Clamp(index, 0, _skinMats.Length - 1)];
        }

        static Material[] BuildMaterials(Color[] colors, string label)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                if (shader == null) continue;
                mats[i] = new Material(shader) { name = $"{label} {i}" };
                mats[i].SetColor("_BaseColor", colors[i]);
                mats[i].SetFloat("_Smoothness", 0f);
            }
            return mats;
        }
    }
}
