using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public static class VosgesFarGrass
    {
        public const float ZFrom = 260f, ZTo = 1450f;
        const float FadeInTo = 340f;
        const float BackFadeFrom = 1360f;
        const float SpacingNear = 1.5f, SpacingFar = 3f;
        const float SizeNear = 1.3f, SizeFar = 2.2f;
        const int Blades = 3;
        const float TuftWidth = 4f;
        const float DepthSpread = 0.5f;
        const float BladeHalfMin = 0.45f, BladeHalfMax = 0.8f;
        const float BladeMin = 2.2f, BladeMax = 4.5f;
        const float LeanMax = 1.2f;
        const float Sink = 0.3f;
        const float PatchSize = 70f;
        const int Shades = 8;
        const float BaseShade = 0.36f, TipShade = 0.81f;
        const float WaveAverage = 0.46f;

        static readonly Color WavingTint = new Color(0.95f, 0.93f, 0.85f);
        const int TuftsPerStep = 600;

        public const int Kinds = 3;
        static readonly float[] KindWidth = { 3.8f, 4.6f, 5.4f };
        static readonly float[] KindHeight = { 4.2f, 5.2f, 6.2f };
        const float KindJitter = 0.35f;
        const float FreshBelow = 0.42f, MidBelow = 0.6f;

        static Material _material;
        static Texture2D _fallbackBlades;

        public static float Dryness(float x, float z, float offset) =>
            Mathf.PerlinNoise(x / PatchSize + offset, z / PatchSize + offset * 0.61f);

        public static int Kind(float dryness, System.Random rng)
        {
            float v = dryness + ((float)rng.NextDouble() - 0.5f) * KindJitter;
            return v < FreshBelow ? 0 : v < MidBelow ? 1 : 2;
        }

        public static void SetupNear(TerrainData data, int detailRes, Color healthy, Color dry)
        {
            ProceduralTerrain.SetupGrassDetail(data, detailRes);

            DetailPrototype[] source = data.detailPrototypes;
            Texture2D blades = source != null && source.Length > 0 ? source[0].prototypeTexture : null;
            if (blades == null)
            {
                if (_fallbackBlades == null)
                    _fallbackBlades = ProceduralTerrain.GrassBladesTexture(new System.Random(ProceduralTerrain.GrassTextureSeed));
                blades = _fallbackBlades;
            }

            var kinds = new DetailPrototype[Kinds];
            for (int k = 0; k < Kinds; k++)
            {
                Color tone = DetailColor(Color.Lerp(healthy, dry, k / (Kinds - 1f)));
                DetailPrototype proto = ProceduralTerrain.CreateGrassPrototype(blades);
                proto.healthyColor = tone;
                proto.dryColor = tone;
                proto.minWidth = proto.maxWidth = KindWidth[k];
                proto.minHeight = proto.maxHeight = KindHeight[k];
                kinds[k] = proto;
            }
            data.detailPrototypes = kinds;
        }

        public static Color DetailColor(Color target)
        {
            Color linear = target.linear;
            Color boost = 2f * Color.Lerp(new Color(0.5f, 0.5f, 0.5f), WavingTint.linear, WaveAverage);
            return new Color(linear.r / boost.r, linear.g / boost.g, linear.b / boost.b, 1f);
        }

        public static Material SharedMaterial(Color healthy, Color dry)
        {
            if (_material != null) return _material;

            var palette = new Texture2D(Shades, 2, TextureFormat.RGBA32, false, true)
            {
                name = "Vosges far grass palette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int i = 0; i < Shades; i++)
            {
                Color tone = Color.Lerp(healthy, dry, i / (Shades - 1f)).linear;
                Color low = tone * BaseShade, high = tone * TipShade;
                low.a = high.a = 1f;
                palette.SetPixel(i, 0, low);
                palette.SetPixel(i, 1, high);
            }
            palette.Apply(false, true);

            _material = new Material(NightReceivers.Lit) { name = "Vosges far grass" };
            _material.SetTexture("_BaseMap", palette);
            _material.SetColor("_BaseColor", Color.white);
            if (_material.HasProperty("_Smoothness")) _material.SetFloat("_Smoothness", 0f);
            if (_material.HasProperty("_Cull")) _material.SetFloat("_Cull", (float)CullMode.Off);
            return _material;
        }

        public static IEnumerable<object> Build(TerrainData data, float x0, float width, float depth,
            System.Func<float, float, bool> skip, int seed, float density, float patchOffset,
            System.Action<Mesh> done, System.Func<float, float, bool> anySlope = null) =>
            Build(data, x0, width, depth, 0f, width, ZFrom, depth, skip, seed, density, patchOffset, done,
                anySlope);

        public static IEnumerable<object> Build(TerrainData data, float x0, float width, float depth,
            float xFrom, float xTo, float zFrom, float zTo,
            System.Func<float, float, bool> skip, int seed, float density, float patchOffset,
            System.Action<Mesh> done, System.Func<float, float, bool> anySlope = null)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float thin = 1f / Mathf.Sqrt(Mathf.Max(0.05f, density));
            int counted = 0;

            float z = Mathf.Max(ZFrom, zFrom);
            float end = Mathf.Min(ZTo, Mathf.Min(depth, zTo));
            float left = Mathf.Max(0f, xFrom);
            float right = Mathf.Min(width, xTo);
            while (z < end)
            {
                float t = Mathf.InverseLerp(ZFrom, ZTo, z);
                float spacing = ProceduralTerrain.GrassSpacing * Mathf.Lerp(SpacingNear, SpacingFar, t) * thin;
                float size = Mathf.Lerp(SizeNear, SizeFar, t);

                for (float lx = left; lx < right; lx += spacing)
                {
                    float px = lx + (float)rng.NextDouble() * spacing;
                    float pz = z + (float)rng.NextDouble() * spacing;
                    double roll = rng.NextDouble();
                    int tuftSeed = rng.Next();

                    if (++counted % TuftsPerStep == 0) yield return null;
                    if (px >= right || pz >= end) continue;

                    float keep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ZFrom, FadeInTo, pz))
                                 * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BackFadeFrom, ZTo, pz)));
                    if (roll > keep) continue;

                    float xn = px / width, zn = pz / depth;
                    if (data.GetSteepness(xn, zn) > ProceduralTerrain.GrassMaxSlopeDeg
                        && (anySlope == null || !anySlope(x0 + px, pz))) continue;
                    if (skip(x0 + px, pz)) continue;

                    var at = new Vector3(px, data.GetInterpolatedHeight(xn, zn), pz);
                    AddTuft(at, data.GetInterpolatedNormal(xn, zn), size, Dryness(x0 + px, pz, patchOffset),
                        new System.Random(tuftSeed), verts, normals, uvs, tris);
                }

                z += spacing;
            }

            if (verts.Count == 0)
            {
                done(null);
                yield break;
            }

            var mesh = new Mesh { name = "Vosges far grass (generated)", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            done(mesh);
        }

        static void AddTuft(Vector3 at, Vector3 normal, float size, float dryness, System.Random rng,
            List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
        {
            for (int b = 0; b < Blades; b++)
            {
                int shade = Mathf.Clamp(Mathf.RoundToInt(dryness * (Shades - 1)) + rng.Next(-1, 2), 0, Shades - 1);
                float u = (shade + 0.5f) / Shades;

                float bx = ((float)rng.NextDouble() - 0.5f) * TuftWidth * size;
                float bz = ((float)rng.NextDouble() - 0.5f) * DepthSpread * size;
                float half = Mathf.Lerp(BladeHalfMin, BladeHalfMax, (float)rng.NextDouble()) * size;
                float height = Mathf.Lerp(BladeMin, BladeMax, (float)rng.NextDouble()) * size;
                float lean = ((float)rng.NextDouble() - 0.5f) * 2f * LeanMax * size;

                int i = verts.Count;
                verts.Add(at + new Vector3(bx - half, -Sink, bz));
                verts.Add(at + new Vector3(bx + lean, height, bz));
                verts.Add(at + new Vector3(bx + half, -Sink, bz));
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uvs.Add(new Vector2(u, 0.25f)); uvs.Add(new Vector2(u, 0.75f)); uvs.Add(new Vector2(u, 0.25f));
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
        }
    }
}
