using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class PlaneWreck : MonoBehaviour
    {
        public const float ViewHold = 2f;

        const int MaxAlive = 16;
        const float MaxAirGap = 0.6f;
        const float GroundProbe = 20000f;
        const float SlopeProbe = 0.5f;
        const float MaxSlopeDeg = 25f;
        static readonly Vector3 BakeSpot = new Vector3(0f, -20000f, 0f);

        const float WireThin = 0.006f;
        const float SnapChance = 0.5f;
        const float SnapMargin = 0.1f;
        const float ClusterReach = 0.25f;

        const float BodyYawMax = 25f;
        const float BodyPitchMax = 3f;
        const float BodyRollMax = 6f;
        const float BellyDig = 0.05f;
        const float PieceDig = 0.06f;

        const float WingFlipChance = 0.3f;
        const float WingTiltMin = 3f, WingTiltMax = 10f;
        const float TailRollMax = 25f;
        const float GroundSink = 0.005f;
        const float PropLieMin = 75f, PropLieMax = 90f;

        const float ScatterMin = 0.3f, ScatterMax = 1f;
        const int ScatterTries = 24;
        const float PrintShrink = 0.85f;
        const int PrintCirclesMax = 6;

        const float CrumpleBody = 0.025f, CrumplePiece = 0.03f;
        const float CrumpleCell = 0.22f;
        const float CrumpleFine = 2.7f;
        const float NoseZone = 0.18f, NoseCrush = 0.05f, NoseRumple = 1.5f;
        const float PropBendStart = 0.3f, PropBendDeg = 55f, PropBendLow = 0.5f;

        const float DarkMin = 0.55f, DarkMax = 0.95f;

        const float SmokeScale = 0.55f;
        const float WispScale = 0.2f;
        const int MaxWisps = 4;
        const int WispSeed = 31;

        const int FireSpotsMin = 8, FireSpotsMax = 10;
        const int FireTries = 400;
        const int FireFloor = 4;
        const float FireClearMetres = 0.65f;
        const float FireRelax = 0.5f;
        const float FireSpacing = 0.7f;
        const int FireSeed = 7;

        const float ViewReach = 1.5f;
        const float FarViews = 2f;

        static readonly Vector3 FieldShiftA = new Vector3(31.7f, 11.3f, 5.9f);
        static readonly Vector3 FieldShiftB = new Vector3(7.1f, 47.9f, 23.3f);

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static readonly List<PlaneWreck> Alive = new List<PlaneWreck>();
        static readonly List<Vector3> Scratch = new List<Vector3>(1024);

        enum Role { Body, Wing, Tail, Engine, Strut, Wheel, Prop, Gun, Wire }

        class Part
        {
            public string name;
            public Role role;
            public List<Vector3> vertices = new List<Vector3>();
            public List<Vector4> tangents = new List<Vector4>();
            public List<Vector2> uv = new List<Vector2>();
            public List<Vector2> uv2 = new List<Vector2>();
            public List<int>[] triangles;
            public Material[] materials;
            public Texture[] textures;
            public Color[] colors;
            public uint layerMask;
            public float dark = 1f;
            public Bounds bounds;
            public MeshFilter filter;

            public bool Empty => vertices.Count == 0;

            public Part Blank() => new Part
            {
                name = name,
                role = role,
                materials = materials,
                textures = textures,
                colors = colors,
                layerMask = layerMask,
                dark = dark,
            };
        }

        class Piece
        {
            public Role role;
            public List<Part> parts;
            public Transform group;
            public Vector3 pivot;
        }

        struct Print
        {
            public List<Vector2> centres;
            public float radius;
        }

        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<Print> _placed = new List<Print>();
        readonly List<Transform> _groups = new List<Transform>();
        readonly List<Vector3> _groupScales = new List<Vector3>();
        readonly List<SmokeColumn> _smokes = new List<SmokeColumn>();
        GroundFire _fire;
        System.Random _rng;
        GameObject _content;
        Vector3 _noiseA, _noiseB;
        float _length;
        float _metre;
        float _delay;
        float _age;
        bool _revealed;
        bool _seen;
        bool _kept;
        bool _fading;
        float _fadeAge;

        public static void FadeAll()
        {
            foreach (PlaneWreck wreck in Alive)
                if (wreck != null && !wreck._kept) wreck.BeginFade();
        }

        public static void KeepLatest()
        {
            PlaneWreck latest = Alive.Count > 0 ? Alive[Alive.Count - 1] : null;
            if (latest != null) latest._kept = true;
        }

        public static bool Spawn(GameObject body, Vector3 impact, Vector3 velocity, bool grounded)
        {
            if (body == null) return false;

            var source = body.GetComponent<WreckSource>();
            if (source == null || source.Plane == null) return false;

            if (!Ground(impact.x, impact.z, out float groundY)) return false;
            float reach = MaxAirGap * source.Plane.LengthUnits * PlaneModelConfig.LevelScale;
            if (!grounded && impact.y - groundY > reach) return false;

            float facing = Mathf.Abs(velocity.x) > 0.01f ? velocity.x : body.transform.right.x;
            facing = facing < 0f ? -1f : 1f;

            List<Part> parts = BakeModel(source);
            if (parts.Count == 0) return false;

            while (Alive.Count >= MaxAlive)
            {
                PlaneWreck oldest = Alive[0];
                Alive.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            var go = new GameObject(source.Plane.resourceName + " Wreck");
            var wreck = go.AddComponent<PlaneWreck>();
            Alive.Add(wreck);
            return wreck.Build(parts, new Vector3(impact.x, groundY, impact.z), facing, Explosion.PeakTime,
                source.Plane.lengthMeters, Random.Range(int.MinValue, int.MaxValue));
        }

        bool Build(List<Part> parts, Vector3 origin, float facing, float delay, float lengthMeters, int seed)
        {
            _rng = new System.Random(seed);
            _delay = delay;
            _noiseA = new Vector3(Range(0f, 100f), Range(0f, 100f), Range(0f, 100f));
            _noiseB = new Vector3(Range(0f, 100f), Range(0f, 100f), Range(0f, 100f));

            Bounds whole = Union(parts);
            float length = Mathf.Max(1f, whole.size.x);
            foreach (Part part in parts)
                if (part.role == Role.Body && Thin(part.bounds, length))
                    part.role = Role.Wire;
            parts.RemoveAll(p => p.Empty || p.role == Role.Wire || p.role == Role.Strut || p.role == Role.Wheel);
            if (parts.Count == 0)
            {
                Destroy(gameObject);
                return false;
            }

            whole = Union(parts);
            _length = Mathf.Max(1f, whole.size.x);
            _metre = _length / Mathf.Max(0.5f, lengthMeters);

            transform.position = origin;
            transform.rotation = SlopeTilt(origin, SlopeProbe * _length)
                                 * Quaternion.Euler(0f, facing > 0f ? 0f : 180f, 0f);

            _content = new GameObject("Pieces");
            _content.SetActive(false);
            _content.transform.SetParent(transform, false);

            List<Piece> pieces = Separate(parts, whole);
            if (parts.Count == 0 && pieces.Count > 0)
            {
                parts.AddRange(pieces[0].parts);
                pieces.RemoveAt(0);
            }

            Bounds bodyBounds = Union(parts);
            foreach (Part part in parts) Crumple(part, CrumpleBody, bodyBounds.max.x);
            foreach (Piece piece in pieces)
            {
                if (piece.role == Role.Prop) BendProp(piece.parts);
                foreach (Part part in piece.parts) Crumple(part, CrumplePiece, float.NaN);
            }

            bodyBounds = Union(parts);
            Transform body = Group("Body", parts, out Vector3 bodyPivot);
            Quaternion bodyLie = Quaternion.AngleAxis(Range(-BodyPitchMax, BodyPitchMax), Vector3.forward)
                                 * Quaternion.AngleAxis(Sign() * Range(0f, BodyRollMax), Vector3.right);
            float bodyYaw = Sign() * Range(0f, BodyYawMax);
            body.localRotation = Quaternion.AngleAxis(bodyYaw, Vector3.up) * bodyLie;
            Settle(parts, body, BellyDig * bodyBounds.size.y);
            _placed.Add(Placed(Footprint(bodyBounds, bodyLie), Flat(bodyPivot), bodyYaw));

            pieces.Sort((a, b) => Area(b).CompareTo(Area(a)));
            foreach (Piece piece in pieces) Scatter(piece, bodyPivot);

            Ignite(body, bodyPivot, bodyBounds, pieces, seed);
            _placed.Clear();
            return true;
        }

        List<Piece> Separate(List<Part> parts, Bounds whole)
        {
            var pieces = new List<Piece>();

            foreach (Part wing in TakeAll(parts, p => p.role == Role.Wing))
            {
                Part half = null;
                bool spans = wing.bounds.min.z < whole.center.z - SnapMargin * whole.extents.z
                             && wing.bounds.max.z > whole.center.z + SnapMargin * whole.extents.z;
                if (spans && Range(0f, 1f) < SnapChance)
                {
                    float centre = whole.center.z;
                    half = Slice(wing, c => c.z > centre);
                }

                if (!wing.Empty) pieces.Add(new Piece { role = Role.Wing, parts = new List<Part> { wing } });
                if (half != null && !half.Empty) pieces.Add(new Piece { role = Role.Wing, parts = new List<Part> { half } });
            }

            List<Part> tail = TakeAll(parts, p => p.role == Role.Tail);
            if (tail.Count > 0) pieces.Add(new Piece { role = Role.Tail, parts = tail });

            foreach (List<Part> engine in Cluster(TakeAll(parts, p => p.role == Role.Engine), ClusterReach * _length))
                pieces.Add(new Piece { role = Role.Engine, parts = engine });

            foreach (List<Part> prop in Cluster(TakeAll(parts, p => p.role == Role.Prop), ClusterReach * _length))
                pieces.Add(new Piece { role = Role.Prop, parts = prop });

            return pieces;
        }

        void Scatter(Piece piece, Vector3 bodyPivot)
        {
            piece.group = Group(piece.role.ToString(), piece.parts, out piece.pivot);
            Bounds bounds = Union(piece.parts);
            Quaternion lie = Lie(piece.role);
            Print print = Footprint(bounds, lie);

            Vector2 centre = Flat(bodyPivot);
            Vector2 best = centre;
            float bestYaw = 0f;
            float bestScore = float.MaxValue;
            for (int i = 0; i < ScatterTries; i++)
            {
                float angle = Range(0f, Mathf.PI * 2f);
                float reach = Range(ScatterMin, ScatterMax) * _length;
                Vector2 spot = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                float yaw = Range(0f, 360f);
                float score = Overlap(Placed(print, spot, yaw));
                if (score >= bestScore) continue;
                bestScore = score;
                best = spot;
                bestYaw = yaw;
            }

            piece.group.localRotation = Quaternion.AngleAxis(bestYaw, Vector3.up) * lie;
            piece.group.localPosition = new Vector3(best.x, piece.pivot.y, best.y);
            Vector3 size = bounds.size;
            float thickness = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            Settle(piece.parts, piece.group, PieceDig * thickness + GroundSink * _length);
            _placed.Add(Placed(print, best, bestYaw));
        }

        Quaternion Lie(Role role)
        {
            switch (role)
            {
                case Role.Wing:
                {
                    Quaternion lie = Quaternion.AngleAxis(Sign() * Range(WingTiltMin, WingTiltMax), Vector3.right);
                    if (Range(0f, 1f) < WingFlipChance) lie *= Quaternion.AngleAxis(180f, Vector3.right);
                    return lie;
                }
                case Role.Tail:
                    return Quaternion.AngleAxis(Range(-TailRollMax, TailRollMax), Vector3.right);
                case Role.Prop:
                    return Quaternion.AngleAxis(Sign() * Range(PropLieMin, PropLieMax), Vector3.forward)
                           * Quaternion.AngleAxis(Range(0f, 360f), Vector3.right);
                default:
                    return Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));
            }
        }

        static Print Footprint(Bounds bounds, Quaternion lie)
        {
            float ex = 0f, ez = 0f;
            Vector3 e = bounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 c = lie * new Vector3(
                    (corner & 1) == 0 ? -e.x : e.x,
                    (corner & 2) == 0 ? -e.y : e.y,
                    (corner & 4) == 0 ? -e.z : e.z);
                ex = Mathf.Max(ex, Mathf.Abs(c.x));
                ez = Mathf.Max(ez, Mathf.Abs(c.z));
            }

            bool alongX = ex >= ez;
            float longSide = 2f * Mathf.Max(ex, ez), shortSide = 2f * Mathf.Min(ex, ez);
            int count = Mathf.Clamp(Mathf.CeilToInt(longSide / Mathf.Max(0.0001f, shortSide)), 1, PrintCirclesMax);
            var print = new Print
            {
                centres = new List<Vector2>(count),
                radius = Mathf.Max(shortSide * 0.5f, longSide / (2f * count)) * PrintShrink,
            };
            for (int i = 0; i < count; i++)
            {
                float t = ((i + 0.5f) / count - 0.5f) * longSide;
                print.centres.Add(alongX ? new Vector2(t, 0f) : new Vector2(0f, t));
            }
            return print;
        }

        static Print Placed(Print print, Vector2 at, float yawDeg)
        {
            Quaternion turn = Quaternion.AngleAxis(yawDeg, Vector3.up);
            var placed = new Print { centres = new List<Vector2>(print.centres.Count), radius = print.radius };
            foreach (Vector2 c in print.centres)
            {
                Vector3 r = turn * new Vector3(c.x, 0f, c.y);
                placed.centres.Add(at + new Vector2(r.x, r.z));
            }
            return placed;
        }

        float Overlap(Print print)
        {
            float sum = 0f;
            foreach (Print other in _placed)
                foreach (Vector2 a in print.centres)
                    foreach (Vector2 b in other.centres)
                        sum += Mathf.Max(0f, print.radius + other.radius - Vector2.Distance(a, b));
            return sum;
        }

        static float Area(Piece piece)
        {
            Vector3 s = Union(piece.parts).size;
            return s.x * s.z + s.x * s.y + s.y * s.z;
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        void Ignite(Transform body, Vector3 bodyPivot, Bounds bodyBounds, List<Piece> pieces, int seed)
        {
            var fires = new GameObject("Fires").transform;
            fires.SetParent(_content.transform, false);
            fires.rotation = Quaternion.identity;

            _smokes.Add(SmokeColumn.Begin(fires, body.position + Vector3.up * bodyBounds.extents.y, seed,
                SmokeScale, false, false));

            int wisps = 0;
            foreach (Role role in new[] { Role.Engine, Role.Wing })
                foreach (Piece piece in pieces)
                {
                    if (wisps >= MaxWisps) break;
                    if (piece.role != role || piece.group == null) continue;
                    Vector3 top = piece.group.position + Vector3.up * Union(piece.parts).extents.y;
                    _smokes.Add(SmokeColumn.Begin(fires, top, seed + WispSeed * ++wisps, WispScale, false, false));
                }

            Vector2 centre = Flat(bodyPivot);
            List<Vector3> spots = FireSpotsAround(centre, out float reach);
            if (spots.Count == 0) return;

            Vector3 middle = transform.TransformPoint(new Vector3(centre.x, 0f, centre.y));
            middle.y = GroundAt(middle.x, middle.z, transform.position.y);
            _fire = GroundFire.Begin(fires, middle, spots, _metre, reach, seed + FireSeed);
        }

        List<Vector3> FireSpotsAround(Vector2 centre, out float reach)
        {
            var points = new List<Vector2>();
            foreach (Print print in _placed) points.AddRange(print.centres);
            List<Vector2> hull = Hull(points);

            reach = 0f;
            Vector2 min = centre, max = centre;
            foreach (Vector2 p in hull)
            {
                reach = Mathf.Max(reach, Vector2.Distance(p, centre));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            int want = _rng.Next(FireSpotsMin, FireSpotsMax + 1);
            float spacing = FireSpacing * Mathf.Sqrt(HullArea(hull) / want);
            var picked = new List<Vector2>(want);
            Pick(picked, want, hull, min, max, FireClearMetres * _metre, spacing);
            if (picked.Count < FireFloor)
                Pick(picked, FireFloor, hull, min, max, FireClearMetres * FireRelax * _metre, spacing);

            var spots = new List<Vector3>(picked.Count);
            foreach (Vector2 spot in picked)
            {
                Vector3 w = transform.TransformPoint(new Vector3(spot.x, 0f, spot.y));
                if (Ground(w.x, w.z, out float y)) spots.Add(new Vector3(w.x, y, w.z));
            }
            return spots;
        }

        void Pick(List<Vector2> picked, int want, List<Vector2> hull, Vector2 min, Vector2 max, float clear,
            float spacing)
        {
            for (int i = 0; i < FireTries && picked.Count < want && hull.Count >= 3; i++)
            {
                var spot = new Vector2(Range(min.x, max.x), Range(min.y, max.y));
                if (!InsideHull(hull, spot) || Covered(spot, clear)) continue;

                bool crowded = false;
                foreach (Vector2 other in picked)
                    if ((other - spot).sqrMagnitude < spacing * spacing) { crowded = true; break; }
                if (!crowded) picked.Add(spot);
            }
        }

        static List<Vector2> Hull(List<Vector2> points)
        {
            var sorted = new List<Vector2>(points);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (sorted.Count < 3) return sorted;

            var hull = new List<Vector2>(sorted.Count * 2);
            foreach (Vector2 p in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }

            int lower = hull.Count + 1;
            for (int i = sorted.Count - 2; i >= 0; i--)
            {
                Vector2 p = sorted[i];
                while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }

            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        static bool InsideHull(List<Vector2> hull, Vector2 p)
        {
            if (hull.Count < 3) return false;
            for (int i = 0; i < hull.Count; i++)
                if (Cross(hull[i], hull[(i + 1) % hull.Count], p) < 0f) return false;
            return true;
        }

        static float HullArea(List<Vector2> hull)
        {
            float sum = 0f;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i], b = hull[(i + 1) % hull.Count];
                sum += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(sum) * 0.5f;
        }

        bool Covered(Vector2 spot, float margin)
        {
            foreach (Print print in _placed)
            {
                float r = print.radius / PrintShrink + margin;
                foreach (Vector2 c in print.centres)
                    if ((c - spot).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        void BeginFade()
        {
            if (_fading) return;
            if (!_revealed)
            {
                Destroy(gameObject);
                return;
            }

            _fading = true;
            _fadeAge = 0f;
            _groupScales.Clear();
            foreach (Transform group in _groups) _groupScales.Add(group != null ? group.localScale : Vector3.one);
        }

        void TickFade()
        {
            _fadeAge += CutscenePause.Delta;
            float k = 1f - Mathf.SmoothStep(0f, 1f, _fadeAge / CutsceneVanish.FadeSec);
            if (k <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < _groups.Count; i++)
                if (_groups[i] != null) _groups[i].localScale = _groupScales[i] * k;
            if (_fire != null) _fire.Fade(k);
            foreach (SmokeColumn smoke in _smokes)
                if (smoke != null) smoke.Fade(k);
        }

        void Update()
        {
            if (_fading)
            {
                TickFade();
                return;
            }

            if (!_revealed)
            {
                _age += Time.deltaTime;
                if (_age < _delay) return;
                _revealed = true;
                if (_content != null) _content.SetActive(true);
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 here = transform.position;
            float half = HalfViewWidth(cam, here);
            float gap = Mathf.Abs(here.x - cam.transform.position.x);
            if (gap <= half + ViewReach * _length)
            {
                _seen = true;
                return;
            }
            if (_seen || gap > half * (1f + 2f * FarViews)) Destroy(gameObject);
        }

        void OnDestroy()
        {
            Alive.Remove(this);
            foreach (Mesh mesh in _meshes)
                if (mesh != null) Destroy(mesh);
            _meshes.Clear();
        }

        static float HalfViewWidth(Camera cam, Vector3 at)
        {
            if (cam.orthographic) return cam.orthographicSize * cam.aspect;
            float depth = Mathf.Max(0f, Vector3.Dot(at - cam.transform.position, cam.transform.forward));
            return depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
        }

        static List<Part> BakeModel(WreckSource source)
        {
            var parts = new List<Part>();
            var temp = new GameObject("Wreck Bake");
            temp.transform.position = BakeSpot;

            Transform model = PlaneFactory.BuildPlaneModel(temp.transform, source.Plane, false, source.Skin);
            if (source.Model != null) model.localScale = source.Model.localScale;
            foreach (Collider hit in temp.GetComponentsInChildren<Collider>(true)) hit.enabled = false;

            Matrix4x4 toRoot = temp.transform.worldToLocalMatrix;
            var block = new MaterialPropertyBlock();
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || !renderer.TryGetComponent(out MeshFilter filter)) continue;
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;

                Part part = Bake(mesh, toRoot * renderer.localToWorldMatrix, renderer, block,
                    Chain(renderer.transform, model));
                if (!part.Empty) parts.Add(part);
            }

            temp.SetActive(false);
            Destroy(temp);
            return parts;
        }

        static Part Bake(Mesh mesh, Matrix4x4 m, MeshRenderer renderer, MaterialPropertyBlock block, string chain)
        {
            var part = new Part
            {
                name = renderer.name.ToLowerInvariant(),
                layerMask = renderer.renderingLayerMask,
            };

            mesh.GetVertices(part.vertices);
            mesh.GetTangents(part.tangents);
            mesh.GetUVs(0, part.uv);
            mesh.GetUVs(1, part.uv2);

            for (int i = 0; i < part.vertices.Count; i++)
                part.vertices[i] = m.MultiplyPoint3x4(part.vertices[i]);
            for (int i = 0; i < part.tangents.Count; i++)
            {
                Vector4 t = part.tangents[i];
                Vector3 d = m.MultiplyVector(t).normalized;
                part.tangents[i] = new Vector4(d.x, d.y, d.z, t.w);
            }

            Material[] mats = renderer.sharedMaterials;
            int subs = Mathf.Min(mesh.subMeshCount, mats.Length);
            part.triangles = new List<int>[subs];
            part.materials = new Material[subs];
            part.textures = new Texture[subs];
            part.colors = new Color[subs];

            renderer.GetPropertyBlock(block);
            Texture skin = block.HasTexture(BaseMapId) ? block.GetTexture(BaseMapId) : null;
            bool flip = m.determinant < 0f;

            for (int s = 0; s < subs; s++)
            {
                var tris = new List<int>();
                if (mesh.GetTopology(s) == MeshTopology.Triangles) mesh.GetTriangles(tris, s);
                if (flip)
                    for (int i = 0; i + 2 < tris.Count; i += 3)
                        (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                part.triangles[s] = tris;

                Material mat = mats[s];
                part.materials[s] = mat;
                part.textures[s] = skin != null ? skin
                    : mat != null && mat.HasProperty(BaseMapId) ? mat.GetTexture(BaseMapId) : null;
                part.colors[s] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            }

            part.role = RoleOf(part.name, chain);
            part.bounds = Measure(part.vertices);
            return part;
        }

        static string Chain(Transform leaf, Transform root)
        {
            var chain = new System.Text.StringBuilder();
            for (Transform t = leaf.parent; t != null && t != root; t = t.parent)
                chain.Append(t.name.ToLowerInvariant()).Append('/');
            return chain.ToString();
        }

        static Role RoleOf(string leaf, string chain)
        {
            if (Has(leaf, "wire") || leaf.StartsWith("rig") || Has(leaf, "centrerig") || Has(leaf, "overhang")
                || Has(leaf, "brace") || Has(leaf, "control") || Has(chain, "rigging"))
                return Role.Wire;
            if (Has(leaf, "prop") || Has(leaf, "blade") || Has(chain, "prop"))
                return Role.Prop;
            if (Has(leaf, "strut") || Has(leaf, "gear") || Has(leaf, "axle") || Has(leaf, "cabane")
                || Has(leaf, "interplane") || Has(leaf, "pylon"))
                return Role.Strut;
            if (Has(leaf, "wheel") || Has(leaf, "tyre") || Has(leaf, "tire") || Has(leaf, "rubber")
                || Has(leaf, "disk") || Has(leaf, "disc"))
                return Role.Wheel;
            if (Has(leaf, "wing"))
                return Role.Wing;
            if (Has(leaf, "tailplane") || Has(leaf, "fin") || Has(leaf, "rudder") || Has(chain, "hstab"))
                return Role.Tail;
            if (Has(leaf, "cowling") || Has(leaf, "engine") || Has(leaf, "crankcase") || Has(leaf, "nacelle")
                || Has(leaf, "radiator") || Has(leaf, "exhaust"))
                return Role.Engine;
            if (Has(leaf, "gun") || Has(chain, "gun"))
                return Role.Gun;
            return Role.Body;
        }

        static bool Has(string text, string token) => text.Contains(token);

        static bool Thin(Bounds b, float length)
        {
            Vector3 s = b.size;
            float big = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            float small = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            float mid = s.x + s.y + s.z - big - small;
            return mid < WireThin * length;
        }

        static List<Part> TakeAll(List<Part> parts, System.Predicate<Part> match)
        {
            var taken = parts.FindAll(match);
            parts.RemoveAll(match);
            return taken;
        }

        static List<List<Part>> Cluster(List<Part> parts, float reach)
        {
            var clusters = new List<List<Part>>();
            var left = new List<Part>(parts);
            while (left.Count > 0)
            {
                var cluster = new List<Part> { left[0] };
                left.RemoveAt(0);
                for (int i = 0; i < cluster.Count; i++)
                {
                    Vector3 c = cluster[i].bounds.center;
                    for (int j = left.Count - 1; j >= 0; j--)
                    {
                        if ((left[j].bounds.center - c).magnitude > reach) continue;
                        cluster.Add(left[j]);
                        left.RemoveAt(j);
                    }
                }
                clusters.Add(cluster);
            }
            return clusters;
        }

        static Part Slice(Part source, System.Func<Vector3, bool> take)
        {
            int subs = source.triangles.Length;
            var kept = new List<int>[subs];
            var moved = new List<int>[subs];
            bool any = false;
            List<Vector3> v = source.vertices;

            for (int s = 0; s < subs; s++)
            {
                kept[s] = new List<int>();
                moved[s] = new List<int>();
                List<int> tris = source.triangles[s];
                for (int i = 0; i + 2 < tris.Count; i += 3)
                {
                    Vector3 c = (v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f;
                    bool move = take(c);
                    List<int> into = move ? moved[s] : kept[s];
                    into.Add(tris[i]);
                    into.Add(tris[i + 1]);
                    into.Add(tris[i + 2]);
                    any |= move;
                }
            }

            if (!any) return null;

            Part piece = Compact(source, moved);
            Part rest = Compact(source, kept);
            source.vertices = rest.vertices;
            source.tangents = rest.tangents;
            source.uv = rest.uv;
            source.uv2 = rest.uv2;
            source.triangles = rest.triangles;
            source.bounds = rest.bounds;
            return piece;
        }

        static Part Compact(Part source, List<int>[] tris)
        {
            int n = source.vertices.Count;
            bool uv = source.uv.Count == n, uv2 = source.uv2.Count == n, tan = source.tangents.Count == n;
            var map = new int[n];
            for (int i = 0; i < n; i++) map[i] = -1;

            Part part = source.Blank();
            part.triangles = new List<int>[tris.Length];
            for (int s = 0; s < tris.Length; s++)
            {
                var list = new List<int>(tris[s].Count);
                foreach (int index in tris[s])
                {
                    if (map[index] < 0)
                    {
                        map[index] = part.vertices.Count;
                        part.vertices.Add(source.vertices[index]);
                        if (uv) part.uv.Add(source.uv[index]);
                        if (uv2) part.uv2.Add(source.uv2[index]);
                        if (tan) part.tangents.Add(source.tangents[index]);
                    }
                    list.Add(map[index]);
                }
                part.triangles[s] = list;
            }

            part.bounds = Measure(part.vertices);
            return part;
        }

        void BendProp(List<Part> props)
        {
            if (props.Count == 0) return;

            Vector3 hub = Union(props).center;
            float reach = 0f;
            foreach (Part part in props)
                foreach (Vector3 v in part.vertices)
                    reach = Mathf.Max(reach, new Vector2(v.y - hub.y, v.z - hub.z).magnitude);
            if (reach <= 0f) return;

            float start = PropBendStart * reach;
            float bend = Range(PropBendLow, 1f) * PropBendDeg;
            foreach (Part part in props)
            {
                for (int i = 0; i < part.vertices.Count; i++)
                {
                    Vector3 v = part.vertices[i];
                    var radial = new Vector3(0f, v.y - hub.y, v.z - hub.z);
                    float r = radial.magnitude;
                    if (r <= start) continue;

                    Vector3 dir = radial / r;
                    float k = (r - start) / Mathf.Max(0.0001f, reach - start);
                    Vector3 pivot = new Vector3(v.x, hub.y, hub.z) + dir * start;
                    Quaternion turn = Quaternion.AngleAxis(bend * k * k, Vector3.Cross(Vector3.right, dir));
                    part.vertices[i] = pivot + turn * (v - pivot);
                }
                part.bounds = Measure(part.vertices);
            }
        }

        void Crumple(Part part, float amount, float noseX)
        {
            float freq = 1f / (CrumpleCell * _length);
            float zone = NoseZone * _length;
            bool nose = !float.IsNaN(noseX);

            for (int i = 0; i < part.vertices.Count; i++)
            {
                Vector3 v = part.vertices[i];
                float amp = amount * _length;
                if (nose)
                {
                    float k = Mathf.Clamp01((v.x - (noseX - zone)) / zone);
                    v.x -= k * k * NoseCrush * _length;
                    amp *= 1f + NoseRumple * k;
                }

                Vector3 p = v * freq;
                Vector3 push = Field(p + _noiseA) + 0.5f * Field(p * CrumpleFine + _noiseB);
                part.vertices[i] = v + push * amp;
            }
            part.bounds = Measure(part.vertices);
        }

        Transform Group(string name, List<Part> parts, out Vector3 pivot)
        {
            pivot = Union(parts).center;

            var go = new GameObject(name);
            go.transform.SetParent(_content.transform, false);
            go.transform.localPosition = pivot;
            _groups.Add(go.transform);

            foreach (Part part in parts)
            {
                part.dark = Range(DarkMin, DarkMax);
                part.filter = MakePiece(part, go.transform, pivot);
            }
            return go.transform;
        }

        MeshFilter MakePiece(Part part, Transform group, Vector3 pivot)
        {
            var go = new GameObject(part.name);
            go.transform.SetParent(group, false);

            var mesh = new Mesh { name = part.name + " (wreck)" };
            if (part.vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

            Scratch.Clear();
            foreach (Vector3 v in part.vertices) Scratch.Add(v - pivot);
            mesh.SetVertices(Scratch);
            int n = part.vertices.Count;
            if (part.uv.Count == n) mesh.SetUVs(0, part.uv);
            if (part.uv2.Count == n) mesh.SetUVs(1, part.uv2);
            if (part.tangents.Count == n) mesh.SetTangents(part.tangents);
            mesh.subMeshCount = part.triangles.Length;
            for (int s = 0; s < part.triangles.Length; s++) mesh.SetTriangles(part.triangles[s], s, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = part.materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.renderingLayerMask = part.layerMask;

            var block = new MaterialPropertyBlock();
            for (int s = 0; s < part.materials.Length; s++)
            {
                block.Clear();
                Texture soot = WreckSoot.For(part.textures[s]);
                block.SetTexture(BaseMapId, soot);
                block.SetTexture(MainTexId, soot);
                Color tint = part.colors[s] * part.dark;
                tint.a = part.colors[s].a;
                block.SetColor(BaseColorId, tint);
                renderer.SetPropertyBlock(block, s);
            }
            return filter;
        }

        void Settle(List<Part> parts, Transform group, float dig)
        {
            float low = float.MaxValue;
            Vector3 lowest = group.position;
            bool found = false;

            foreach (Part part in parts)
            {
                if (part.filter == null) continue;

                Matrix4x4 m = part.filter.transform.localToWorldMatrix;
                part.filter.sharedMesh.GetVertices(Scratch);
                foreach (Vector3 v in Scratch)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (w.y >= low) continue;
                    low = w.y;
                    lowest = w;
                    found = true;
                }
            }
            if (!found) return;

            float ground = GroundAt(lowest.x, lowest.z, transform.position.y);
            group.position += Vector3.up * (ground - dig - low);
        }

        static Quaternion SlopeTilt(Vector3 at, float probe)
        {
            float x0 = GroundAt(at.x - probe, at.z, at.y), x1 = GroundAt(at.x + probe, at.z, at.y);
            float z0 = GroundAt(at.x, at.z - probe, at.y), z1 = GroundAt(at.x, at.z + probe, at.y);
            var normal = new Vector3(-(x1 - x0) / (2f * probe), 1f, -(z1 - z0) / (2f * probe)).normalized;
            normal = Vector3.RotateTowards(Vector3.up, normal, MaxSlopeDeg * Mathf.Deg2Rad, 0f);
            return Quaternion.FromToRotation(Vector3.up, normal);
        }

        static float GroundAt(float x, float z, float fallback) => Ground(x, z, out float y) ? y : fallback;

        static bool Ground(float x, float z, out float y)
        {
            Battlefield field = Battlefield.Current;
            if (field != null && field.Surface(x, z, out y, out bool water))
                return !water && !BelowSea(y);

            if (Physics.Raycast(new Vector3(x, GroundProbe, z), Vector3.down, out RaycastHit hit,
                    GroundProbe * 2f, 1 << ProceduralTerrain.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return !BelowSea(y);
            }

            y = 0f;
            return false;
        }

        static bool BelowSea(float y) => SeaSurface.Current != null && y < SeaSurface.Level;

        static Bounds Measure(List<Vector3> vertices)
        {
            if (vertices.Count == 0) return default;
            var b = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++) b.Encapsulate(vertices[i]);
            return b;
        }

        static Bounds Union(List<Part> parts)
        {
            bool any = false;
            Bounds b = default;
            foreach (Part part in parts)
            {
                if (part.Empty) continue;
                if (any) b.Encapsulate(part.bounds);
                else { b = part.bounds; any = true; }
            }
            return b;
        }

        static Vector3 Field(Vector3 p) =>
            new Vector3(Value(p), Value(p + FieldShiftA), Value(p + FieldShiftB)) * 2f - Vector3.one;

        static float Value(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x), fy = Smooth(p.y - y), fz = Smooth(p.z - z);

            float a = Mathf.Lerp(Lattice(x, y, z), Lattice(x + 1, y, z), fx);
            float b = Mathf.Lerp(Lattice(x, y + 1, z), Lattice(x + 1, y + 1, z), fx);
            float c = Mathf.Lerp(Lattice(x, y, z + 1), Lattice(x + 1, y, z + 1), fx);
            float d = Mathf.Lerp(Lattice(x, y + 1, z + 1), Lattice(x + 1, y + 1, z + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static float Lattice(int x, int y, int z)
        {
            unchecked
            {
                int h = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xffff) / 65535f;
            }
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        float Sign() => _rng.NextDouble() < 0.5 ? -1f : 1f;
    }
}
