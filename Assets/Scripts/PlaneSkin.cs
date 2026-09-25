using UnityEngine;

namespace MetalRaptors
{
    public class PlaneSkin
    {
        public string id;

        public string label;

        public string texture;

        public bool locked;
    }

    public static class PlaneSkins
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        static readonly Color ShroudColor = new Color(0.14f, 0.13f, 0.16f);

        static Texture2D _shroud;

        static readonly PlaneSkin[] Empty = new PlaneSkin[0];

        public static readonly PlaneSkin[] SopwithCamel =
        {
            new PlaneSkin { id = "green", label = "green", texture = "skins/sopwith_camel/green" },
            new PlaneSkin
            {
                id = "dark_blue", label = "dark blue", texture = "skins/sopwith_camel/dark_blue",
            },
            new PlaneSkin { id = "white", label = "white", texture = "skins/sopwith_camel/white" },
            new PlaneSkin { id = "red", label = "red", texture = "skins/sopwith_camel/red" },
        };

        public static readonly PlaneSkin[] FokkerDr1 =
        {
            new PlaneSkin { id = "red", label = "red", texture = "skins/fokker_dr_1/red" },
            new PlaneSkin
            {
                id = "raven", label = "raven", texture = "skins/fokker_dr_1/raven", locked = true,
            },
        };

        public static readonly PlaneSkin[] AlbatrosD3 =
        {
            new PlaneSkin { id = "plywood", label = "plywood", texture = "skins/albatros_d3/plywood" },
        };

        public static PlaneSkin[] Of(PlaneModelConfig plane) =>
            plane != null && plane.skins != null ? plane.skins : Empty;

        public static bool Selectable(PlaneModelConfig plane) => Of(plane).Length > 1;

        public static bool IsLocked(PlaneSkin skin) => skin != null && skin.locked;

        public const string CompanionId = "dark_blue";

        public static PlaneSkin Companion(PlaneModelConfig plane, string id = null) =>
            ById(plane, id) ?? ById(plane, CompanionId) ?? Default(plane);

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

        public static void Apply(Transform model, PlaneSkin skin, bool shrouded = false)
        {
            if (model == null || skin == null) return;

            Texture2D texture = shrouded ? Shroud : Resources.Load<Texture2D>(skin.texture);
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

        static Texture2D Shroud
        {
            get
            {
                if (_shroud != null) return _shroud;

                _shroud = new Texture2D(1, 1)
                {
                    name = "Locked Skin Shroud",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _shroud.SetPixel(0, 0, ShroudColor);
                _shroud.Apply();
                return _shroud;
            }
        }
    }
}
