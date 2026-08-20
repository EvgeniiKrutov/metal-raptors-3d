using UnityEngine;

namespace MetalRaptors
{
    public class PlaneSkin
    {
        public string id;

        public string label;

        public string texture;
    }

    public static class PlaneSkins
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        static readonly PlaneSkin[] Empty = new PlaneSkin[0];

        public static readonly PlaneSkin[] SopwithCamel =
        {
            new PlaneSkin { id = "green", label = "green", texture = "skins/sopwith_camel/green" },
            new PlaneSkin { id = "blue",  label = "blue",  texture = "skins/sopwith_camel/blue" },
        };

        public static readonly PlaneSkin[] AlbatrosD3 =
        {
            new PlaneSkin { id = "plywood", label = "plywood", texture = "skins/albatros_d3/plywood" },
        };

        public static PlaneSkin[] Of(PlaneModelConfig plane) =>
            plane != null && plane.skins != null ? plane.skins : Empty;

        public static bool Selectable(PlaneModelConfig plane) => Of(plane).Length > 1;

        public static PlaneSkin Default(PlaneModelConfig plane)
        {
            PlaneSkin[] skins = Of(plane);
            return skins.Length > 0 ? skins[0] : null;
        }

        public static PlaneSkin ById(PlaneModelConfig plane, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (PlaneSkin skin in Of(plane))
                if (skin.id == id) return skin;

            return null;
        }

        public static int IndexOf(PlaneModelConfig plane, PlaneSkin skin)
        {
            PlaneSkin[] skins = Of(plane);
            for (int i = 0; i < skins.Length; i++)
                if (skins[i] == skin) return i;

            return 0;
        }

        public static string[] Labels(PlaneModelConfig plane)
        {
            PlaneSkin[] skins = Of(plane);
            var labels = new string[skins.Length];
            for (int i = 0; i < skins.Length; i++) labels[i] = skins[i].label;
            return labels;
        }

        public static void Apply(Transform model, PlaneSkin skin)
        {
            if (model == null || skin == null) return;

            var texture = Resources.Load<Texture2D>(skin.texture);
            if (texture == null)
            {
                Debug.LogError($"PlaneSkins: {skin.texture} not found in Resources.");
                return;
            }

            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, texture);
                block.SetTexture(MainTexId, texture);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
