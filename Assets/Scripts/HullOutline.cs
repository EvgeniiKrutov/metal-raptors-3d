using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class HullOutline
    {
        const float WeldFraction = 1e-4f;
        const float PlaneFraction = 1e-5f;
        const float FlatDot = 0.9999f;

        struct Face
        {
            public int A, B, C;
            public Vector3 Normal;
            public float Offset;

            public float Distance(Vector3 point) => Vector3.Dot(Normal, point) - Offset;
        }

        public static Vector3[] Build(Vector3[] source)
        {
            if (source == null || source.Length == 0) return new Vector3[0];

            Bounds bounds = BoundsOf(source);
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size <= 0f) return new Vector3[0];

            List<Vector3> points = Weld(source, size * WeldFraction);
            float eps = size * PlaneFraction;

            if (points.Count < 4 || !Seed(points, eps, out int i0, out int i1, out int i2, out int i3))
                return Box(bounds);

            Vector3 inside = (points[i0] + points[i1] + points[i2] + points[i3]) * 0.25f;
            var faces = new List<Face>
            {
                MakeFace(points, i0, i1, i2, inside),
                MakeFace(points, i0, i1, i3, inside),
                MakeFace(points, i0, i2, i3, inside),
                MakeFace(points, i1, i2, i3, inside),
            };

            var visible = new List<int>();
            var rim = new Dictionary<long, int>();

            for (int p = 0; p < points.Count; p++)
            {
                if (p == i0 || p == i1 || p == i2 || p == i3) continue;

                Vector3 point = points[p];
                visible.Clear();
                for (int f = 0; f < faces.Count; f++)
                    if (faces[f].Distance(point) > eps) visible.Add(f);
                if (visible.Count == 0) continue;

                rim.Clear();
                foreach (int f in visible)
                {
                    Face face = faces[f];
                    Count(rim, face.A, face.B);
                    Count(rim, face.B, face.C);
                    Count(rim, face.C, face.A);
                }

                for (int v = visible.Count - 1; v >= 0; v--) faces.RemoveAt(visible[v]);

                foreach (KeyValuePair<long, int> edge in rim)
                {
                    if (edge.Value != 1) continue;
                    Unkey(edge.Key, out int a, out int b);
                    faces.Add(MakeFace(points, a, b, p, inside));
                }
            }

            return Edges(points, faces);
        }

        public static Vector3[] Box(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            var lines = new Vector3[24];
            int n = 0;

            for (int corner = 0; corner < 8; corner++)
            {
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((corner & bit) != 0) continue;
                    lines[n++] = Corner(min, max, corner);
                    lines[n++] = Corner(min, max, corner | bit);
                }
            }
            return lines;
        }

        static Vector3 Corner(Vector3 min, Vector3 max, int corner) => new Vector3(
            (corner & 1) == 0 ? min.x : max.x,
            (corner & 2) == 0 ? min.y : max.y,
            (corner & 4) == 0 ? min.z : max.z);

        static Bounds BoundsOf(Vector3[] source)
        {
            var bounds = new Bounds(source[0], Vector3.zero);
            for (int i = 1; i < source.Length; i++) bounds.Encapsulate(source[i]);
            return bounds;
        }

        static List<Vector3> Weld(Vector3[] source, float cell)
        {
            var seen = new HashSet<Vector3Int>();
            var points = new List<Vector3>();
            float inverse = 1f / cell;

            foreach (Vector3 point in source)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(point.x * inverse),
                    Mathf.RoundToInt(point.y * inverse),
                    Mathf.RoundToInt(point.z * inverse));
                if (seen.Add(key)) points.Add(point);
            }
            return points;
        }

        static bool Seed(List<Vector3> points, float eps,
            out int i0, out int i1, out int i2, out int i3)
        {
            i0 = 0;
            for (int i = 1; i < points.Count; i++)
                if (points[i].x < points[i0].x) i0 = i;

            i1 = i0;
            float best = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - points[i0]).sqrMagnitude;
                if (d > best) { best = d; i1 = i; }
            }

            i2 = i0;
            i3 = i0;
            if (best <= eps * eps) return false;

            Vector3 axis = (points[i1] - points[i0]).normalized;
            best = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float d = Vector3.Cross(points[i] - points[i0], axis).sqrMagnitude;
                if (d > best) { best = d; i2 = i; }
            }
            if (best <= eps * eps) return false;

            Vector3 normal = Vector3.Cross(points[i1] - points[i0], points[i2] - points[i0]).normalized;
            best = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float d = Mathf.Abs(Vector3.Dot(points[i] - points[i0], normal));
                if (d > best) { best = d; i3 = i; }
            }
            return best > eps;
        }

        static Face MakeFace(List<Vector3> points, int a, int b, int c, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
            float length = normal.magnitude;
            normal = length > 0f ? normal / length : Vector3.up;

            float offset = Vector3.Dot(normal, points[a]);
            if (Vector3.Dot(normal, inside) - offset > 0f)
            {
                normal = -normal;
                offset = -offset;
            }
            return new Face { A = a, B = b, C = c, Normal = normal, Offset = offset };
        }

        static Vector3[] Edges(List<Vector3> points, List<Face> faces)
        {
            var sides = new Dictionary<long, Vector2Int>();
            for (int f = 0; f < faces.Count; f++)
            {
                Face face = faces[f];
                Side(sides, face.A, face.B, f);
                Side(sides, face.B, face.C, f);
                Side(sides, face.C, face.A, f);
            }

            var lines = new List<Vector3>();
            foreach (KeyValuePair<long, Vector2Int> edge in sides)
            {
                Vector2Int pair = edge.Value;
                if (pair.y >= 0 && Vector3.Dot(faces[pair.x].Normal, faces[pair.y].Normal) > FlatDot)
                    continue;

                Unkey(edge.Key, out int a, out int b);
                lines.Add(points[a]);
                lines.Add(points[b]);
            }
            return lines.ToArray();
        }

        static void Side(Dictionary<long, Vector2Int> sides, int a, int b, int face)
        {
            long key = Key(a, b);
            if (sides.TryGetValue(key, out Vector2Int pair)) sides[key] = new Vector2Int(pair.x, face);
            else sides[key] = new Vector2Int(face, -1);
        }

        static void Count(Dictionary<long, int> rim, int a, int b)
        {
            long key = Key(a, b);
            rim.TryGetValue(key, out int count);
            rim[key] = count + 1;
        }

        static long Key(int a, int b) =>
            a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        static void Unkey(long key, out int a, out int b)
        {
            a = (int)(key >> 32);
            b = (int)(key & 0xffffffffL);
        }
    }
}
