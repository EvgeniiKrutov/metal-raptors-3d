using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Shared builder for the low-poly blob mesh used by the code-built volume effects
    /// (<see cref="Explosion"/>, <see cref="CloudSystem"/>): an icosphere with randomly
    /// displaced vertices, split per-face for flat shading. See docs/effects.md.
    /// </summary>
    public static class BlobMesh
    {
        public static Mesh Build()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var baseVerts = new List<Vector3>
            {
                new Vector3(-1f,  t, 0f), new Vector3(1f,  t, 0f),
                new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f,  t), new Vector3(0f,  1f,  t),
                new Vector3(0f, -1f, -t), new Vector3(0f,  1f, -t),
                new Vector3( t, 0f, -1f), new Vector3( t, 0f,  1f),
                new Vector3(-t, 0f, -1f), new Vector3(-t, 0f,  1f),
            };
            for (int i = 0; i < baseVerts.Count; i++) baseVerts[i] = baseVerts[i].normalized;

            int[] icoFaces =
            {
                0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
                1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
                3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
                4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1,
            };

            var midCache = new Dictionary<long, int>();
            var faces = new List<int>();
            for (int i = 0; i < icoFaces.Length; i += 3)
            {
                int a = icoFaces[i], b = icoFaces[i + 1], c = icoFaces[i + 2];
                int ab = Midpoint(baseVerts, midCache, a, b);
                int bc = Midpoint(baseVerts, midCache, b, c);
                int ca = Midpoint(baseVerts, midCache, c, a);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            // Displace the shared vertices (so faces stay stitched); 0.5 keeps localScale = diameter.
            for (int i = 0; i < baseVerts.Count; i++)
                baseVerts[i] *= 0.5f * Random.Range(0.72f, 1.3f);

            var verts = new Vector3[faces.Count];
            var tris = new int[faces.Count];
            for (int i = 0; i < faces.Count; i++)
            {
                verts[i] = baseVerts[faces[i]];
                tris[i] = i;
            }

            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }
    }
}
