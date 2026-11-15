using UnityEngine;

namespace MetalRaptors
{
    public class ModelCover
    {
        const float CoverCell = 0.5f;
        const float MinArea = 1e-6f;

        float _minX, _minZ;
        int _width, _height;
        bool[] _cells;

        public bool Covered(Vector3 local)
        {
            int ix = Mathf.FloorToInt((local.x - _minX) / CoverCell);
            int iz = Mathf.FloorToInt((local.z - _minZ) / CoverCell);
            if (ix < 0 || iz < 0 || ix >= _width || iz >= _height) return false;
            return _cells[iz * _width + ix];
        }

        public static ModelCover Build(GameObject view, Bounds box, float minTop = float.NegativeInfinity)
        {
            var cover = new ModelCover
            {
                _minX = box.min.x - CoverCell,
                _minZ = box.min.z - CoverCell,
                _width = Mathf.CeilToInt(box.size.x / CoverCell) + 3,
                _height = Mathf.CeilToInt(box.size.z / CoverCell) + 3,
            };
            var marks = new bool[cover._width * cover._height];

            foreach (MeshFilter filter in view.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable) return null;

                Vector3[] verts = mesh.vertices;
                int[] tris = mesh.triangles;
                var flat = new Vector2[verts.Length];
                float top = float.MinValue;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = ModelProbe.Local(filter.transform.TransformPoint(verts[i]));
                    flat[i] = new Vector2(p.x, p.z);
                    top = Mathf.Max(top, p.y);
                }
                if (top < minTop) continue;

                for (int i = 0; i < flat.Length; i++) cover.Mark(marks, flat[i]);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                    cover.Fill(marks, flat[tris[t]], flat[tris[t + 1]], flat[tris[t + 2]]);
            }

            cover._cells = new bool[marks.Length];
            for (int iz = 0; iz < cover._height; iz++)
                for (int ix = 0; ix < cover._width; ix++)
                {
                    bool hit = false;
                    for (int dz = -1; dz <= 1 && !hit; dz++)
                        for (int dx = -1; dx <= 1 && !hit; dx++)
                        {
                            int x = ix + dx, z = iz + dz;
                            if (x < 0 || z < 0 || x >= cover._width || z >= cover._height) continue;
                            hit = marks[z * cover._width + x];
                        }
                    cover._cells[iz * cover._width + ix] = hit;
                }

            return cover;
        }

        void Mark(bool[] marks, Vector2 p)
        {
            int ix = Mathf.FloorToInt((p.x - _minX) / CoverCell);
            int iz = Mathf.FloorToInt((p.y - _minZ) / CoverCell);
            if (ix < 0 || iz < 0 || ix >= _width || iz >= _height) return;
            marks[iz * _width + ix] = true;
        }

        void Fill(bool[] marks, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < MinArea) return;

            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - _minX) / CoverCell));
            int x1 = Mathf.Min(_width - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - _minX) / CoverCell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - _minZ) / CoverCell));
            int z1 = Mathf.Min(_height - 1, Mathf.FloorToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - _minZ) / CoverCell));
            float sign = Mathf.Sign(area);

            for (int iz = z0; iz <= z1; iz++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    var p = new Vector2(_minX + (ix + 0.5f) * CoverCell, _minZ + (iz + 0.5f) * CoverCell);
                    if (Cross(b - a, p - a) * sign < 0f) continue;
                    if (Cross(c - b, p - b) * sign < 0f) continue;
                    if (Cross(a - c, p - c) * sign < 0f) continue;
                    marks[iz * _width + ix] = true;
                }
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    }
}
