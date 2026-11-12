using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class WalkGraph
    {
        public Vector3[] nodes;
        public int[][] links;
    }

    public static class VosgesVillage
    {
        public const string ModelResource = "objects/villages/austrian_village_";
        public const int Models = 7;

        const string ChurchNode = "tower";
        static readonly string[] WalkPrefixes = { "road_", "lane_", "path_" };
        const float TopNormal = 0.7f;
        const float TopBand = 0.02f;
        const float Snap = 100f;
        const float MergeRadius = 0.3f;
        const float LinkRadius = 5.5f;
        const float CoverCell = 0.5f;
        const float MinArea = 1e-6f;

        class Cover
        {
            public float minX, minZ;
            public int width, height;
            public bool[] cells;
        }

        static readonly GameObject[] Prefabs = new GameObject[Models];
        static readonly Bounds[] Boxes = new Bounds[Models];
        static readonly Vector3[] Churches = new Vector3[Models];
        static readonly WalkGraph[] Graphs = new WalkGraph[Models];
        static readonly Cover[] Covers = new Cover[Models];
        static bool _measured;
        static bool _warnedReadable;

        public static void Measure()
        {
            if (_measured) return;
            _measured = true;

            for (int i = 0; i < Models; i++)
            {
                string path = ModelResource + i;
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"VosgesVillage: {path} not found in Resources.");
                    continue;
                }

                GameObject view = ModelProbe.Open(prefab);
                if (!ModelProbe.Bounds(view, out Bounds box))
                {
                    Debug.LogError($"VosgesVillage: {path} has no renderers; it is skipped.");
                    ModelProbe.Close(view);
                    continue;
                }

                Transform tower = PlaneFactory.FindDeep(view.transform, ChurchNode);
                Vector3 church = box.center;
                if (tower != null && tower.TryGetComponent(out Renderer towerRenderer))
                    church = ModelProbe.Local(towerRenderer.bounds.center);

                Prefabs[i] = prefab;
                Boxes[i] = box;
                Churches[i] = church;
                Graphs[i] = BuildGraph(view);
                Covers[i] = BuildCover(view, box);
                ModelProbe.Close(view);
            }
        }

        public static bool Has(int model) =>
            model >= 0 && model < Models && Prefabs[model] != null;

        public static Bounds Footprint(int model) => Boxes[model];

        public static Vector3 Church(int model) => Churches[model];

        public static WalkGraph Graph(int model) => Has(model) ? Graphs[model] : null;

        public static bool Covered(int model, Vector3 local)
        {
            if (!Has(model)) return true;
            Cover cover = Covers[model];
            if (cover == null) return true;

            int ix = Mathf.FloorToInt((local.x - cover.minX) / CoverCell);
            int iz = Mathf.FloorToInt((local.z - cover.minZ) / CoverCell);
            if (ix < 0 || iz < 0 || ix >= cover.width || iz >= cover.height) return false;
            return cover.cells[iz * cover.width + ix];
        }

        static Cover BuildCover(GameObject view, Bounds box)
        {
            var cover = new Cover
            {
                minX = box.min.x - CoverCell,
                minZ = box.min.z - CoverCell,
                width = Mathf.CeilToInt(box.size.x / CoverCell) + 3,
                height = Mathf.CeilToInt(box.size.z / CoverCell) + 3,
            };
            var marks = new bool[cover.width * cover.height];

            foreach (MeshFilter filter in view.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable) return null;

                Vector3[] verts = mesh.vertices;
                int[] tris = mesh.triangles;
                var flat = new Vector2[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = ModelProbe.Local(filter.transform.TransformPoint(verts[i]));
                    flat[i] = new Vector2(p.x, p.z);
                    Mark(cover, marks, flat[i]);
                }

                for (int t = 0; t + 2 < tris.Length; t += 3)
                    Fill(cover, marks, flat[tris[t]], flat[tris[t + 1]], flat[tris[t + 2]]);
            }

            cover.cells = new bool[marks.Length];
            for (int iz = 0; iz < cover.height; iz++)
                for (int ix = 0; ix < cover.width; ix++)
                {
                    bool hit = false;
                    for (int dz = -1; dz <= 1 && !hit; dz++)
                        for (int dx = -1; dx <= 1 && !hit; dx++)
                        {
                            int x = ix + dx, z = iz + dz;
                            if (x < 0 || z < 0 || x >= cover.width || z >= cover.height) continue;
                            hit = marks[z * cover.width + x];
                        }
                    cover.cells[iz * cover.width + ix] = hit;
                }

            return cover;
        }

        static void Mark(Cover cover, bool[] marks, Vector2 p)
        {
            int ix = Mathf.FloorToInt((p.x - cover.minX) / CoverCell);
            int iz = Mathf.FloorToInt((p.y - cover.minZ) / CoverCell);
            if (ix < 0 || iz < 0 || ix >= cover.width || iz >= cover.height) return;
            marks[iz * cover.width + ix] = true;
        }

        static void Fill(Cover cover, bool[] marks, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < MinArea) return;

            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - cover.minX) / CoverCell));
            int x1 = Mathf.Min(cover.width - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - cover.minX) / CoverCell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - cover.minZ) / CoverCell));
            int z1 = Mathf.Min(cover.height - 1, Mathf.FloorToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - cover.minZ) / CoverCell));
            float sign = Mathf.Sign(area);

            for (int iz = z0; iz <= z1; iz++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    var p = new Vector2(cover.minX + (ix + 0.5f) * CoverCell, cover.minZ + (iz + 0.5f) * CoverCell);
                    if (Cross(b - a, p - a) * sign < 0f) continue;
                    if (Cross(c - b, p - b) * sign < 0f) continue;
                    if (Cross(a - c, p - c) * sign < 0f) continue;
                    marks[iz * cover.width + ix] = true;
                }
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        public static Transform Place(Transform parent, VosgesLayout.VillageSpot spot)
        {
            if (!Has(spot.model)) return null;

            var root = new GameObject($"Village {spot.model}");
            root.layer = BattlefieldProps.Layer;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(spot.origin, spot.rotation);
            root.transform.localScale = Vector3.one * PlaneModelConfig.UnitsPerMeter;

            var view = Object.Instantiate(Prefabs[spot.model], root.transform, false);
            view.name = Prefabs[spot.model].name;
            NightReceivers.Adopt(view);
            NightLights.MarkCaster(view);
            return root.transform;
        }

        static WalkGraph BuildGraph(GameObject view)
        {
            var points = new List<Vector3>();

            foreach (MeshFilter filter in view.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!Walkable(filter.name)) continue;
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    if (!_warnedReadable)
                        Debug.LogWarning("VosgesVillage: village meshes are not readable; villagers stay home. "
                                         + "Reimport the austrian_village models.");
                    _warnedReadable = true;
                    continue;
                }

                AddSurface(filter.transform, mesh, points);
            }

            var nodes = new List<Vector3>();
            foreach (var p in points)
            {
                bool merged = false;
                foreach (var n in nodes)
                    if ((n - p).sqrMagnitude < MergeRadius * MergeRadius) { merged = true; break; }
                if (!merged) nodes.Add(p);
            }

            var links = new int[nodes.Count][];
            var scratch = new List<int>();
            for (int a = 0; a < nodes.Count; a++)
            {
                scratch.Clear();
                for (int b = 0; b < nodes.Count; b++)
                {
                    if (a == b) continue;
                    Vector3 d = nodes[b] - nodes[a];
                    d.y = 0f;
                    if (d.sqrMagnitude <= LinkRadius * LinkRadius) scratch.Add(b);
                }
                links[a] = scratch.ToArray();
            }

            return new WalkGraph { nodes = nodes.ToArray(), links = links };
        }

        static bool Walkable(string name)
        {
            foreach (string prefix in WalkPrefixes)
                if (name.StartsWith(prefix)) return true;
            return false;
        }

        static void AddSurface(Transform tr, Mesh mesh, List<Vector3> points)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            var world = new Vector3[verts.Length];
            float top = float.MinValue;
            for (int i = 0; i < verts.Length; i++)
            {
                world[i] = ModelProbe.Local(tr.TransformPoint(verts[i]));
                top = Mathf.Max(top, world[i].y);
            }

            var faces = new List<((Vector3Int, Vector3Int) key, Vector3 middle, Vector3 centroid)>();
            var counts = new Dictionary<(Vector3Int, Vector3Int), int>();

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = world[tris[t]], b = world[tris[t + 1]], c = world[tris[t + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-8f) continue;
                if (Mathf.Abs(normal.normalized.y) < TopNormal) continue;
                if ((a.y + b.y + c.y) / 3f < top - TopBand) continue;

                Longest(a, b, c, out Vector3 p, out Vector3 q);
                var key = Key(p, q);
                counts[key] = counts.TryGetValue(key, out int seen) ? seen + 1 : 1;
                faces.Add((key, (p + q) * 0.5f, (a + b + c) / 3f));
            }

            var used = new HashSet<(Vector3Int, Vector3Int)>();
            foreach (var face in faces)
            {
                if (counts[face.key] < 2)
                {
                    points.Add(face.centroid);
                    continue;
                }

                if (used.Add(face.key)) points.Add(face.middle);
            }
        }

        static void Longest(Vector3 a, Vector3 b, Vector3 c, out Vector3 p, out Vector3 q)
        {
            float ab = (a - b).sqrMagnitude, bc = (b - c).sqrMagnitude, ca = (c - a).sqrMagnitude;
            if (ab >= bc && ab >= ca) { p = a; q = b; }
            else if (bc >= ca) { p = b; q = c; }
            else { p = c; q = a; }
        }

        static (Vector3Int, Vector3Int) Key(Vector3 p, Vector3 q)
        {
            var a = Vector3Int.RoundToInt(p * Snap);
            var b = Vector3Int.RoundToInt(q * Snap);
            if (a.x < b.x || (a.x == b.x && (a.z < b.z || (a.z == b.z && a.y <= b.y)))) return (a, b);
            return (b, a);
        }
    }
}
