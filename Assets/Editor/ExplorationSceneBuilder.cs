using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Core;
using RaftGame.Resources;
using RaftGame.Salvage;

namespace RaftGame.Editor
{
    public static class ExplorationSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Exploration and Blueprint Discovery")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            game.Loot = AssetDatabase.LoadAssetAtPath<LootConfig>("Assets/Configs/Loot_Default.asset");
            if (game.Loot == null) { game.Loot = ScriptableObject.CreateInstance<LootConfig>(); AssetDatabase.CreateAsset(game.Loot, "Assets/Configs/Loot_Default.asset"); }
            game.Building.PurifierCost = new ResourceCost(6, 3);
            var salvage = game.Salvage;
            salvage.BottlePrefab = CreateSupply(FloatingKind.Bottle); salvage.SupplyCratePrefab = CreateSupply(FloatingKind.SupplyCrate);
            salvage.StartingItems = 3; salvage.MaximumItems = 7; salvage.SpawnInterval = 16; salvage.SpawnIntervalVariation = .35f;
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.Building); EditorUtility.SetDirty(salvage); EditorUtility.SetDirty(game.Ocean);
            ManagementHudBuilder.Rebuild();
            if (BlenderModelSceneBuilder.IsAvailable) BlenderModelSceneBuilder.ApplyToOpenScene();
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("EXPLORATION_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }
        private static GameObject CreateSupply(FloatingKind kind)
        {
            string path = "Assets/Art/Prefabs/Salvage/" + (kind == FloatingKind.Bottle ? "Drift_WaterBottle" : "Drift_SupplyCrate") + ".prefab";
            var root = new GameObject(kind.ToString(), typeof(FloatingResource), typeof(BoxCollider));
            Material wood = Mat("DriftWood", new Color(.65f, .37f, .16f));
            Material rope = Mat("Rope", new Color(.82f, .67f, .4f));
            Material cream = Mat("SupplyCream", new Color(.93f, .91f, .77f));
            Material blue = Mat("BottleWater", new Color(.18f, .59f, .72f));
            if (kind == FloatingKind.Bottle)
            {
                Part("Bottle", PrimitiveType.Cylinder, root.transform, new Vector3(0, .2f, 0), new Vector3(.28f, .27f, .28f), blue);
                Part("Label", PrimitiveType.Cylinder, root.transform, new Vector3(0, .2f, 0), new Vector3(.29f, .07f, .29f), cream);
                Part("Neck", PrimitiveType.Cylinder, root.transform, new Vector3(0, .49f, 0), new Vector3(.13f, .08f, .13f), blue);
                Part("Cap", PrimitiveType.Cylinder, root.transform, new Vector3(0, .57f, 0), new Vector3(.16f, .025f, .16f), rope);
                root.GetComponent<BoxCollider>().size = new Vector3(.55f, .9f, .55f);
            }
            else
            {
                Part("Box", PrimitiveType.Cube, root.transform, new Vector3(0, .16f, 0), new Vector3(.88f, .54f, .78f), wood);
                for (int i = -1; i <= 1; i++) Part("Lid Plank", PrimitiveType.Cube, root.transform, new Vector3(i * .29f, .45f, 0), new Vector3(.27f, .06f, .79f), rope);
                foreach (float x in new[] { -.32f, .32f }) Part("Lashing", PrimitiveType.Cube, root.transform, new Vector3(x, .18f, 0), new Vector3(.055f, .64f, .83f), cream);
                Part("Supply Label", PrimitiveType.Cube, root.transform, new Vector3(0, .19f, -.40f), new Vector3(.28f, .24f, .015f), cream);
                Part("Ocean Seal", PrimitiveType.Sphere, root.transform, new Vector3(0, .19f, -.42f), new Vector3(.10f, .13f, .02f), blue);
                root.GetComponent<BoxCollider>().size = new Vector3(1, .85f, .9f);
            }
            root.GetComponent<BoxCollider>().center = new Vector3(0, .2f, 0);
            var marker = new GameObject("Salvage Mark", typeof(LineRenderer)); marker.transform.SetParent(root.transform, false);
            var line = marker.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.widthMultiplier = .035f;
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Salvage/MarkedGold.mat"); line.positionCount = 32; line.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < 32; i++) { float angle = i * Mathf.PI * 2 / 32; line.SetPosition(i, new Vector3(Mathf.Cos(angle) * .72f, .64f, Mathf.Sin(angle) * .66f)); }
            marker.SetActive(false); var fields = new SerializedObject(root.GetComponent<FloatingResource>()); fields.FindProperty("marker").objectReferenceValue = marker; fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return prefab;
        }
        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(part.GetComponent<Collider>()); return part;
        }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/Art/Materials/Salvage/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path); if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = name, color = color }; mat.SetFloat("_Glossiness", .18f); AssetDatabase.CreateAsset(mat, path); return mat;
        }
    }
}
