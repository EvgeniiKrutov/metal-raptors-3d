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

        static readonly GameObject[] Prefabs = new GameObject[Models];
        static readonly Bounds[] Boxes = new Bounds[Models];
        static readonly Vector3[] Churches = new Vector3[Models];
        static readonly WalkGraph[] Graphs = new WalkGraph[Models];
        static readonly ModelCover[] Covers = new ModelCover[Models];
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
                Covers[i] = ModelCover.Build(view, box);
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
            ModelCover cover = Covers[model];
            return cover == null || cover.Covered(local);
        }

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

        public static WalkGraph BuildGraph(GameObject view, string skipPrefix = null)
        {
            var points = new List<Vector3>();

            foreach (MeshFilter filter in view.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!Walkable(filter.name, skipPrefix)) continue;
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

        static bool Walkable(string name, string skipPrefix)
        {
            if (!string.IsNullOrEmpty(skipPrefix) && name.StartsWith(skipPrefix)) return false;
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
