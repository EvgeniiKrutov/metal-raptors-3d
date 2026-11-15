using UnityEngine;

namespace MetalRaptors
{
    public class PlaneSkin
    {
        public string id;

        public string label;

        public string texture;

        public int unlockLevel;
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
            new PlaneSkin { id = "red", label = "red", texture = "skins/sopwith_camel/red" },
            new PlaneSkin
            {
                id = "mahogany", label = "mahogany", texture = "skins/sopwith_camel/mahogany",
            },
            new PlaneSkin
            {
                id = "white", label = "white", texture = "skins/sopwith_camel/white", unlockLevel = 7,
            },
        };

        public static readonly PlaneSkin[] FokkerDr1 =
        {
            new PlaneSkin { id = "manfred", label = "manfred", texture = "skins/fokker_dr_1/manfred" },
            new PlaneSkin
            {
                id = "black_white", label = "black-white", texture = "skins/fokker_dr_1/black_white",
            },
            new PlaneSkin
            {
                id = "red_green", label = "red-green", texture = "skins/fokker_dr_1/red_green",
            },
            new PlaneSkin { id = "lothar", label = "lothar", texture = "skins/fokker_dr_1/lothar" },
            new PlaneSkin
            {
                id = "raven", label = "raven", texture = "skins/fokker_dr_1/raven", unlockLevel = 4,
            },
        };

        public static readonly PlaneSkin[] AlbatrosD3 =
        {
            new PlaneSkin { id = "plywood", label = "plywood", texture = "skins/albatros_d3/plywood" },
            new PlaneSkin { id = "violet", label = "violet", texture = "skins/albatros_d3/violet" },
            new PlaneSkin { id = "lubbert", label = "lubbert", texture = "skins/albatros_d3/lubbert" },
            new PlaneSkin
            {
                id = "black_white", label = "black-white", texture = "skins/albatros_d3/black_white",
            },
            new PlaneSkin
            {
                id = "raven", label = "raven", texture = "skins/albatros_d3/raven", unlockLevel = 4,
            },
        };

        public static readonly PlaneSkin[] FokkerEindecker =
        {
            new PlaneSkin { id = "sand", label = "sand", texture = "skins/fokker_eindecker/sand" },
            new PlaneSkin { id = "green", label = "green", texture = "skins/fokker_eindecker/green" },
            new PlaneSkin { id = "white", label = "white", texture = "skins/fokker_eindecker/white" },
            new PlaneSkin
            {
                id = "raven", label = "raven", texture = "skins/fokker_eindecker/raven", unlockLevel = 4,
            },
        };

        public static readonly PlaneSkin[] AircoDh4 =
        {
            new PlaneSkin { id = "original", label = "original", texture = "skins/airco_dh4/original" },
            new PlaneSkin { id = "green", label = "green", texture = "skins/airco_dh4/green" },
            new PlaneSkin
            {
                id = "white_blue", label = "white-blue", texture = "skins/airco_dh4/white_blue",
            },
            new PlaneSkin { id = "sun", label = "sun", texture = "skins/airco_dh4/sun" },
        };

        public static readonly PlaneSkin[] GothaG4 =
        {
            new PlaneSkin { id = "k2", label = "k2", texture = "skins/gotha_g4/k2", unlockLevel = 9 },
        };

        public static PlaneSkin[] Of(PlaneModelConfig plane) =>
            plane != null && plane.skins != null ? plane.skins : Empty;

        public static bool Selectable(PlaneModelConfig plane) => Of(plane).Length > 1;

        public static bool IsLocked(PlaneSkin skin) =>
            skin != null && skin.unlockLevel > 0 && !CampaignProgress.IsCompleted(skin.unlockLevel);

        public const string CompanionId = "dark_blue";

        public static PlaneSkin Companion(PlaneModelConfig plane, string id = null) =>
            ById(plane, id) ?? ById(plane, CompanionId) ?? Default(plane);

        public static PlaneSkin Enemy(PlaneModelConfig plane, string id) =>
            ById(plane, id) ?? Default(plane);

        public const string RavenId = "raven";

        public static PlaneSkin RandomEnemy(PlaneModelConfig plane, string id)
        {
            if (!string.IsNullOrEmpty(id)) return Enemy(plane, id);

            PlaneSkin[] skins = Of(plane);
            int count = 0;
            foreach (PlaneSkin skin in skins)
                if (skin.id != RavenId) count++;

            int pick = Random.Range(0, count);
            foreach (PlaneSkin skin in skins)
            {
                if (skin.id == RavenId) continue;
                if (pick-- == 0) return skin;
            }

            return Default(plane);
        }

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
