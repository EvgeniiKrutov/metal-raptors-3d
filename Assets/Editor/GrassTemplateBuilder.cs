using UnityEditor;
using UnityEngine;

namespace MetalRaptors.EditorTools
{
    static class GrassTemplateBuilder
    {
        const string TerrainFolder = "Assets/Terrain";
        const string ResourcesFolder = TerrainFolder + "/Resources";
        const string TexturePath = ResourcesFolder + "/GrassBlades.asset";
        const string TemplatePath =
            ResourcesFolder + "/" + ProceduralTerrain.GrassTemplateResource + ".asset";

        const int TemplateDetailRes = 32;

        [MenuItem("Metal Raptors/Rebuild Grass Terrain Template")]
        public static void Rebuild() => Build();

        [InitializeOnLoadMethod]
        static void EnsureTemplateExists()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<TerrainData>(TemplatePath) == null) Build();
            };
        }

        static void Build()
        {
            if (!AssetDatabase.IsValidFolder(TerrainFolder))
                AssetDatabase.CreateFolder("Assets", "Terrain");
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder(TerrainFolder, "Resources");

            AssetDatabase.DeleteAsset(TemplatePath);
            AssetDatabase.DeleteAsset(TexturePath);

            var blades = ProceduralTerrain.GrassBladesTexture(
                new System.Random(ProceduralTerrain.GrassTextureSeed));
            AssetDatabase.CreateAsset(blades, TexturePath);

            var template = new TerrainData();
            template.detailPrototypes = new[] { ProceduralTerrain.CreateGrassPrototype(blades) };
            ProceduralTerrain.SetupGrassDetail(template, TemplateDetailRes);
            AssetDatabase.CreateAsset(template, TemplatePath);

            AssetDatabase.SaveAssets();
            Debug.Log($"GrassTemplateBuilder: wrote {TemplatePath} and {TexturePath}.");
        }
    }
}
