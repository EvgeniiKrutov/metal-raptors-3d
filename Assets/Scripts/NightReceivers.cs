using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class NightReceivers
    {
        public const float CloudResponse = 0.7f;
        public const float SmokeResponse = 0.5f;
        public const float DistantResponse = 0.3f;

        static readonly int ResponseId = Shader.PropertyToID("_NightResponse");
        static readonly int RimId = Shader.PropertyToID("_NightRim");

        static readonly Dictionary<(Material, float, float), Material> Adopted =
            new Dictionary<(Material, float, float), Material>();
        static readonly List<Renderer> Renderers = new List<Renderer>(32);

        static Shader _urpLit;
        static Shader _lit;
        static Shader _terrainLit;

        static Shader UrpLit
        {
            get
            {
                if (_urpLit == null) _urpLit = Shader.Find("Universal Render Pipeline/Lit");
                return _urpLit;
            }
        }

        public static Shader Lit
        {
            get
            {
                if (_lit == null) _lit = Pick("Custom/NightLit", UrpLit);
                return _lit;
            }
        }

        public static Shader TerrainLit
        {
            get
            {
                if (_terrainLit == null)
                    _terrainLit = Pick("Custom/NightTerrainLit", Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                return _terrainLit;
            }
        }

        static Shader Pick(string name, Shader fallback)
        {
            var shader = Shader.Find(name);
            if (shader != null && shader.isSupported) return shader;
            Debug.LogWarning($"NightReceivers: {name} is missing or unsupported; night lighting falls back to URP.");
            return fallback;
        }

        public static void SetResponse(Material material, float response)
        {
            if (material != null && material.HasProperty(ResponseId)) material.SetFloat(ResponseId, response);
        }

        public static void SetResponse(GameObject root, float response)
        {
            if (root == null) return;
            root.GetComponentsInChildren(true, Renderers);
            foreach (Renderer renderer in Renderers)
                SetResponse(renderer.sharedMaterial, response);
            Renderers.Clear();
        }

        public static void Adopt(GameObject root, float response = 1f, float rim = 0f)
        {
            if (root == null || Lit == UrpLit) return;

            root.GetComponentsInChildren(true, Renderers);
            foreach (Renderer renderer in Renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material night = Convert(slots[i], response, rim);
                    if (night == slots[i]) continue;
                    slots[i] = night;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = slots;
            }
            Renderers.Clear();
        }

        static Material Convert(Material source, float response, float rim)
        {
            if (source == null || source.shader != UrpLit) return source;

            var key = (source, response, rim);
            if (Adopted.TryGetValue(key, out Material cached) && cached != null) return cached;

            string[] keywords = source.shaderKeywords;
            int queue = source.renderQueue;
            string renderType = source.GetTag("RenderType", false, "");

            var copy = new Material(source) { name = source.name + " (night)" };
            copy.shader = Lit;
            copy.shaderKeywords = keywords;
            copy.renderQueue = queue;
            if (!string.IsNullOrEmpty(renderType)) copy.SetOverrideTag("RenderType", renderType);
            copy.SetFloat(ResponseId, response);
            copy.SetFloat(RimId, rim);

            Adopted[key] = copy;
            return copy;
        }
    }
}
