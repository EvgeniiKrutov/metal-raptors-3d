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

        static MaterialPropertyBlock _block;
        static bool _missing;
        static bool _warned;

        static MaterialPropertyBlock Block()
        {
            if (_block != null || _missing) return _block;

            var texture = Resources.Load<Texture2D>(Texture);
            if (texture == null)
            {
                Debug.LogError($"TankSkin: {Texture} not found in Resources.");
                _missing = true;
                return null;
            }

            _block = new MaterialPropertyBlock();
            _block.SetTexture(BaseMapId, texture);
            _block.SetTexture(MainTexId, texture);
            _block.SetColor(BaseColorId, Color.white);
            return _block;
        }

        public static void Apply(Transform view)
        {
            MaterialPropertyBlock block = Block();
            if (view == null || block == null) return;

            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            if (!Recognised(renderers))
            {
                Warn(renderers);
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (!Flat(slots[i])) renderer.SetPropertyBlock(block, i);
            }
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
