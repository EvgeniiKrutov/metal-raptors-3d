using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class RoadsideCrash
    {
        const float U = PlaneModelConfig.UnitsPerMeter;

        const string SkinId = "mahogany";
        const string UndercarriageNode = "Undercarriage";
        const string GearWiresNode = "wheels_wires_mesh";
        const string WingNode = "upper_wing";
        const string CowlingNode = "cowling";

        const float YawDeg = 30f;
        const float RollDeg = 4f;
        const float PitchDeg = -1.5f;
        const float Sink = 0.12f * U;

        const float LeftShare = 0.15f, RightShare = 1.3f, PreferShare = 0.55f;
        const float EdgeMargin = 2f * U;
        const float ScanStep = 6f;
        const float Clearance = 0.4f * U;
        const float SampleCell = 0.8f * U;
        const int SamplesPerMesh = 600;
        const float GuardMargin = 1.5f * U;

        const float RousselAhead = 0.9f * U, RousselOut = 0.30f;
        const float MarchandAhead = 1.5f * U, MarchandOut = 0.12f;
        const float FigureRadius = 0.6f * U;
        const float RousselTurn = 15f, MarchandTurn = -20f;

        const float SmokeScale = 0.45f;
        const int SmokeSalt = 0x2B41;

        const float Height = 13f;
        const float Width = 4.6f;
        const float Thickness = 3f;

        static readonly Color Uniform = new Color(0.40f, 0.47f, 0.56f);
        static readonly Color OfficerCap = new Color(0.22f, 0.26f, 0.36f);
        static readonly Color Boots = new Color(0.20f, 0.14f, 0.10f);
        static readonly Color Bandage = new Color(0.86f, 0.84f, 0.78f);
        static readonly Color RousselSkin = new Color(0.78f, 0.62f, 0.48f);
        static readonly Color MarchandSkin = new Color(0.70f, 0.53f, 0.40f);

        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        const float BumpCooldown = 0.5f;
        const float BumpDepthBand = 40f;

        Vector2 _centre;
        float _radius;
        Transform _root;
        float _bumpTime = float.NegativeInfinity;

        public Rect Area { get; private set; }

        public float GroundY { get; private set; }

        public bool Blocks(float x, float z, float margin)
        {
            float dx = x - _centre.x, dz = z - _centre.y;
            float reach = _radius + margin;
            return dx * dx + dz * dz < reach * reach;
        }

        public bool CheckBump(Transform playerTr, float reach)
        {
            if (_root == null || playerTr == null) return false;
            if (Time.time - _bumpTime < BumpCooldown) return false;

            Vector3 pos = _root.position;
            if (Mathf.Abs(pos.z - playerTr.position.z) > BumpDepthBand) return false;

            Vector2 gap = (Vector2)pos - (Vector2)playerTr.position;
            if (gap.sqrMagnitude > reach * reach) return false;

            _bumpTime = Time.time;
            return true;
        }

        public System.Func<float, float, float, bool> Guard(System.Func<float, float, float, bool> occupied) =>
            (x, z, margin) => Blocks(x, z, margin) || (occupied != null && occupied(x, z, margin));

        public static RoadsideCrash Build(VosgesArena arena, CampaignTerrain land, float halfViewWidth,
            float worldWidth, int seed)
        {
            if (arena == null) return null;

            PlaneModelConfig plane = PlaneModels.Sopwith;
            var root = new GameObject("Roadside Crash");
            root.layer = BattlefieldProps.Layer;

            Transform model = PlaneFactory.BuildPlaneModel(root.transform, plane,
                skin: PlaneSkins.ById(plane, SkinId) ?? PlaneSkins.Default(plane));
            model.localScale *= PlaneModelConfig.LevelScale;
            Ground(model);

            float span = plane.OnScreenSize * PlaneModelConfig.LevelScale;
            float wingFront = FrontOf(root.transform, model, WingNode, plane.LengthUnits * PlaneModelConfig.LevelScale);
            var rousselSpot = new Vector3(wingFront + RousselAhead, 0f, -span * RousselOut);
            var marchandSpot = new Vector3(wingFront + MarchandAhead, 0f, -span * MarchandOut);

            Quaternion pose = Quaternion.Euler(RollDeg, YawDeg, PitchDeg);
            Quaternion flat = Quaternion.Euler(0f, YawDeg, 0f);
            root.transform.rotation = pose;

            List<Vector2> points = Footprint(root.transform, model);
            AddFigure(points, flat * rousselSpot, RousselTurn, Thickness + Height * 0.48f);
            AddFigure(points, flat * marchandSpot, MarchandTurn, 0f);

            Vector2 spot = Pick(arena, points, halfViewWidth, worldWidth);
            float ground = GroundAt(land, arena, spot.x, spot.y);
            float lowest = Lowest(root.transform, model);

            root.transform.position = new Vector3(spot.x, ground - lowest - Sink, spot.y);

            var origin = new Vector3(spot.x, ground, spot.y);
            Transform people = new GameObject("Downed Pilots").transform;
            people.gameObject.layer = BattlefieldProps.Layer;
            Place(SittingFigure(people), land, arena, origin + flat * rousselSpot, RousselTurn);
            Place(StandingFigure(people), land, arena, origin + flat * marchandSpot, MarchandTurn);

            Smoke(root.transform, model, seed);

            var crash = new RoadsideCrash { GroundY = ground, _root = root.transform };
            crash.Measure(points, spot);
            return crash;
        }

        void Measure(List<Vector2> points, Vector2 spot)
        {
            _centre = spot;
            _radius = 0f;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
            foreach (Vector2 p in points)
            {
                _radius = Mathf.Max(_radius, p.magnitude);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y);
                maxZ = Mathf.Max(maxZ, p.y);
            }
            _radius += GuardMargin;
            Area = Rect.MinMaxRect(spot.x + minX, spot.y + minZ, spot.x + maxX, spot.y + maxZ);
        }

        static void Ground(Transform model)
        {
            foreach (PropellerSpin spin in model.GetComponentsInChildren<PropellerSpin>(true))
                Object.Destroy(spin);
            foreach (ShakeEffect shake in model.GetComponentsInChildren<ShakeEffect>(true))
                Object.Destroy(shake);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            Transform gear = PlaneFactory.FindDeep(model, UndercarriageNode);
            if (gear != null) gear.gameObject.SetActive(false);
            Transform wires = PlaneFactory.FindDeep(model, GearWiresNode);
            if (wires != null) wires.gameObject.SetActive(false);
        }

        static float FrontOf(Transform root, Transform model, string node, float length)
        {
            Transform part = PlaneFactory.FindDeep(model, node);
            float front = float.MinValue;
            if (part != null)
                foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>())
                    foreach (Vector3 v in Vertices(filter))
                        front = Mathf.Max(front, root.InverseTransformPoint(filter.transform.TransformPoint(v)).x);

            return front > float.MinValue ? front : length * 0.2f;
        }

        static IEnumerable<Vector3> Vertices(MeshFilter filter)
        {
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable) yield break;

            Vector3[] verts = mesh.vertices;
            int step = Mathf.Max(1, verts.Length / SamplesPerMesh);
            for (int i = 0; i < verts.Length; i += step) yield return verts[i];
        }

        static List<Vector2> Footprint(Transform root, Transform model)
        {
            var cells = new HashSet<Vector2Int>();
            Vector3 origin = root.position;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.gameObject.activeInHierarchy) continue;
                foreach (Vector3 v in Vertices(filter))
                {
                    Vector3 p = filter.transform.TransformPoint(v) - origin;
                    cells.Add(new Vector2Int(Mathf.RoundToInt(p.x / SampleCell), Mathf.RoundToInt(p.z / SampleCell)));
                }
            }

            var points = new List<Vector2>(cells.Count);
            foreach (Vector2Int cell in cells) points.Add(new Vector2(cell.x * SampleCell, cell.y * SampleCell));
            return points;
        }

        static void AddFigure(List<Vector2> points, Vector3 offset, float turn, float reach)
        {
            Vector3 ahead = Facing(turn) * Vector3.forward;
            for (float along = 0f; ; along = Mathf.Min(along + SampleCell, reach))
            {
                Vector3 centre = offset + ahead * along;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    points.Add(new Vector2(centre.x + Mathf.Cos(angle) * FigureRadius,
                        centre.z + Mathf.Sin(angle) * FigureRadius));
                }
                points.Add(new Vector2(centre.x, centre.z));
                if (along >= reach) break;
            }
        }

        static Quaternion Facing(float turn) => Quaternion.Euler(0f, 180f + turn, 0f);

        static float Lowest(Transform root, Transform model)
        {
            float lowest = float.MaxValue;
            float baseY = root.position.y;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.gameObject.activeInHierarchy) continue;
                foreach (Vector3 v in Vertices(filter))
                    lowest = Mathf.Min(lowest, filter.transform.TransformPoint(v).y - baseY);
            }
            return lowest < float.MaxValue ? lowest : 0f;
        }

        static Vector2 Pick(VosgesArena arena, List<Vector2> points, float halfViewWidth, float worldWidth)
        {
            float reachLeft = 0f, reachRight = 0f;
            foreach (Vector2 p in points)
            {
                reachLeft = Mathf.Max(reachLeft, -p.x);
                reachRight = Mathf.Max(reachRight, p.x);
            }

            float minX = Mathf.Max(halfViewWidth * LeftShare, reachLeft + EdgeMargin);
            float maxX = Mathf.Min(halfViewWidth * RightShare, worldWidth - reachRight - EdgeMargin);
            float preferX = Mathf.Clamp(halfViewWidth * PreferShare, minX, Mathf.Max(minX, maxX));
            float z = arena.RoadZ;

            var best = new Vector2(preferX, z);
            int bestHits = int.MaxValue;
            float bestScore = float.MaxValue;

            for (float x = minX; x <= maxX; x += ScanStep)
            {
                float score = Mathf.Abs(x - preferX);
                if (bestHits == 0 && score >= bestScore) continue;

                int hits = 0;
                foreach (Vector2 p in points)
                {
                    if (arena.Solid(x + p.x, z + p.y, Clearance)) hits++;
                    if (hits > bestHits) break;
                }

                if (hits < bestHits || (hits == bestHits && score < bestScore))
                {
                    bestHits = hits;
                    bestScore = score;
                    best = new Vector2(x, z);
                }
            }

            if (bestHits > 0)
                Debug.LogWarning($"RoadsideCrash: no clear spot on the road; the plane overlaps {bestHits} village samples.");
            return best;
        }

        static float GroundAt(CampaignTerrain land, VosgesArena arena, float x, float z)
        {
            float ground = land != null && land.SampleHeight(x, z, out float y) ? y : arena.Origin.y;
            return Mathf.Abs(z - arena.RoadZ) < arena.RoadHalfWidth ? ground + arena.RoadLift : ground;
        }

        static void Smoke(Transform root, Transform model, int seed)
        {
            Transform cowling = PlaneFactory.FindDeep(model, CowlingNode);
            Renderer renderer = cowling != null ? cowling.GetComponentInChildren<Renderer>() : null;
            Vector3 at = renderer != null
                ? new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z)
                : root.position;

            SmokeColumn.Begin(root, at, seed ^ SmokeSalt, SmokeScale, prewarm: true, ember: false, wisp: true);
        }

        static void Place(Transform figure, CampaignTerrain land, VosgesArena arena, Vector3 at, float turn)
        {
            float y = GroundAt(land, arena, at.x, at.z);
            figure.SetPositionAndRotation(new Vector3(at.x, y, at.z), Facing(turn));
        }

        static Transform StandingFigure(Transform parent)
        {
            var root = new GameObject("Marchand");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);

            float legs = Height * 0.42f, coat = Height * 0.24f, face = Height * 0.17f, cap = Height * 0.17f;
            Box(root.transform, Boots, new Vector3(0f, Height * 0.05f, 0f), new Vector3(Width * 0.9f, Height * 0.1f, Thickness * 1.1f));
            Box(root.transform, Uniform, new Vector3(0f, Height * 0.1f + (legs - Height * 0.1f) * 0.5f, 0f),
                new Vector3(Width * 0.9f, legs - Height * 0.1f, Thickness));
            Box(root.transform, Uniform, new Vector3(0f, legs + coat * 0.5f, 0f), new Vector3(Width, coat, Thickness * 1.05f));
            Box(root.transform, MarchandSkin, new Vector3(0f, legs + coat + face * 0.5f, 0f),
                new Vector3(Width * 0.8f, face, Thickness * 0.9f));
            Box(root.transform, Uniform, new Vector3(0f, legs + coat + face + cap * 0.5f, 0f),
                new Vector3(Width * 1.05f, cap, Thickness * 1.05f));

            NightLights.MarkCaster(root);
            return root.transform;
        }

        static Transform SittingFigure(Transform parent)
        {
            var root = new GameObject("Roussel");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);

            float torso = Height * 0.40f, face = Height * 0.17f, cap = Height * 0.14f;
            float leg = Height * 0.14f, legLength = Height * 0.48f, legGap = Width * 0.26f;

            Box(root.transform, Uniform, new Vector3(0f, torso * 0.5f, 0f), new Vector3(Width, torso, Thickness * 1.05f));
            Box(root.transform, RousselSkin, new Vector3(0f, torso + face * 0.5f, 0f),
                new Vector3(Width * 0.8f, face, Thickness * 0.9f));
            Box(root.transform, OfficerCap, new Vector3(0f, torso + face + cap * 0.5f, 0f),
                new Vector3(Width * 1.05f, cap, Thickness * 1.05f));

            float legZ = Thickness * 0.5f + legLength * 0.5f;
            Box(root.transform, Uniform, new Vector3(-legGap, leg * 0.5f, legZ), new Vector3(Width * 0.42f, leg, legLength));
            Box(root.transform, Uniform, new Vector3(legGap, leg * 0.5f, legZ), new Vector3(Width * 0.42f, leg, legLength));

            float footZ = Thickness * 0.5f + legLength;
            Box(root.transform, Boots, new Vector3(-legGap, leg * 0.75f, footZ), new Vector3(Width * 0.44f, leg * 1.5f, Thickness * 0.5f));
            Box(root.transform, Bandage, new Vector3(legGap, leg * 0.55f, footZ - legLength * 0.3f),
                new Vector3(Width * 0.48f, leg * 1.15f, legLength * 0.45f));

            NightLights.MarkCaster(root);
            return root.transform;
        }

        static void Box(Transform parent, Color color, Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Segment";
            go.layer = parent.gameObject.layer;

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                Object.Destroy(col);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color);
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out Material mat) && mat != null) return mat;

            mat = new Material(NightReceivers.Lit) { name = "Downed pilot cloth" };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0f);
            Materials[color] = mat;
            return mat;
        }
    }
}
