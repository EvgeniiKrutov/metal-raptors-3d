using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class TankSkin
    {
        public const string Texture = "machines/tank_ww1";

        static readonly string[] FlatMaterials = { "black_steel", "steel", "hull", "tracks" };

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Dictionary<Material, Material> Skinned = new Dictionary<Material, Material>();

        static Texture2D _texture;
        static bool _missing;
        static bool _warned;

        static Texture2D Atlas()
        {
            if (_texture != null || _missing) return _texture;

            _texture = Resources.Load<Texture2D>(Texture);
            if (_texture == null)
            {
                Debug.LogError($"TankSkin: {Texture} not found in Resources.");
                _missing = true;
            }

            return _texture;
        }

        public static void Apply(Transform view)
        {
            Texture2D atlas = Atlas();
            if (view == null || atlas == null) return;

            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            if (!Recognised(renderers))
            {
                Warn(renderers);
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (Flat(slots[i])) continue;
                    slots[i] = SkinnedMaterial(slots[i], atlas);
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = slots;
            }
        }

        static Material SkinnedMaterial(Material source, Texture2D atlas)
        {
            if (Skinned.ContainsValue(source)) return source;
            if (Skinned.TryGetValue(source, out Material cached) && cached != null) return cached;

            var copy = new Material(source) { name = source.name + " (skinned)" };

            if (copy.HasProperty(BaseMapId)) copy.SetTexture(BaseMapId, atlas);
            if (copy.HasProperty(MainTexId)) copy.SetTexture(MainTexId, atlas);
            if (copy.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, Color.white);
            if (copy.HasProperty(ColorId)) copy.SetColor(ColorId, Color.white);

            Skinned[source] = copy;
            return copy;
        }

        static bool Recognised(Renderer[] renderers)
        {
            foreach (Renderer renderer in renderers)
                foreach (Material slot in renderer.sharedMaterials)
                    if (Flat(slot)) return true;

            return false;
        }

        static bool Flat(Material material)
        {
            if (material == null) return true;

            foreach (string flat in FlatMaterials)
                if (material.name.StartsWith(flat)) return true;

            return false;
        }

        static void Warn(Renderer[] renderers)
        {
            if (_warned) return;
            _warned = true;

            var found = new HashSet<string>();
            foreach (Renderer renderer in renderers)
                foreach (Material slot in renderer.sharedMaterials)
                    if (slot != null) found.Add(slot.name);

            Debug.LogWarning($"TankSkin: the model carries none of {string.Join(", ", FlatMaterials)}"
                             + $", so {Texture} was not bound. Its materials are: "
                             + $"{string.Join(", ", found)}.");
        }
    }
}
