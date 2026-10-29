using UnityEditor;

namespace RaftGame.Editor
{
    public sealed class BlenderModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Art/Models/Blender/", System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.globalScale = 1; importer.isReadable = false;
        }
    }
}
