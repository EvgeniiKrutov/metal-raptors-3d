using UnityEditor;

namespace MetalRaptors.EditorTools
{
    class VosgesModelImport : AssetPostprocessor
    {
        static readonly string[] ReadablePrefixes =
        {
            "Assets/Resources/objects/villages/austrian_village_",
            "Assets/Resources/objects/alpine_houses/",
            "Assets/Resources/objects/cow",
            "Assets/Resources/objects/trees/spruce_trees/",
        };

        void OnPreprocessModel()
        {
            if (!Readable(assetPath)) return;
            if (assetImporter is ModelImporter importer && !importer.isReadable) importer.isReadable = true;
        }

        static bool Readable(string path)
        {
            foreach (string prefix in ReadablePrefixes)
                if (path.StartsWith(prefix)) return true;
            return false;
        }
    }
}
