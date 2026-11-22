using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class WreckSoot
    {
        const int MinSize = 128;
        const int MaxSize = 512;
        const float Soot = 1f;
        const float Dim = 0.78f;

        static readonly Dictionary<Texture, RenderTexture> Baked = new Dictionary<Texture, RenderTexture>();

        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int SootId = Shader.PropertyToID("_Soot");
        static readonly int DimId = Shader.PropertyToID("_Dim");

        static Material _material;
        static bool _missing;

        public static Texture For(Texture source)
        {
            if (source == null) source = Texture2D.whiteTexture;

            Material material = Material;
            if (material == null) return source;

            if (Baked.TryGetValue(source, out RenderTexture rt) && rt != null)
            {
                if (!rt.IsCreated()) Bake(source, rt, material);
                return rt;
            }

            int width = Mathf.Clamp(source.width, MinSize, MaxSize);
            int height = Mathf.Clamp(source.height, MinSize, MaxSize);
            rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = source.name + " (soot)",
                useMipMap = true,
                autoGenerateMips = true,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            Bake(source, rt, material);
            Baked[source] = rt;
            return rt;
        }

        static void Bake(Texture source, RenderTexture rt, Material material)
        {
            if (!rt.IsCreated()) rt.Create();
            material.SetFloat(SeedId, (source.name.GetHashCode() & 0xffff) * 0.013f);
            material.SetFloat(SootId, Soot);
            material.SetFloat(DimId, Dim);
            Graphics.Blit(source, rt, material, 0);
        }

        static Material Material
        {
            get
            {
                if (_material != null || _missing) return _material;

                Shader shader = Shader.Find("Hidden/WreckSoot");
                if (shader == null)
                {
                    _missing = true;
                    Debug.LogWarning("WreckSoot: Hidden/WreckSoot shader not found; wrecks keep their skin.");
                    return null;
                }

                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return _material;
            }
        }
    }
}
