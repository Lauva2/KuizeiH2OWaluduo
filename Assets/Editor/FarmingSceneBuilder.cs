using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.Crew;
using RaftGame.Farming;
namespace RaftGame.Editor
{
    public static class FarmingSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Potato Farming")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            Folder("Assets/Art/Materials/Farming"); Folder("Assets/Art/Prefabs/Farming");
            const string path = "Assets/Configs/Farming_Default.asset"; var config = AssetDatabase.LoadAssetAtPath<FarmingConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<FarmingConfig>(); AssetDatabase.CreateAsset(config, path); }
            game.Farming = config; bool first = game.Building.PlanterPrefab == null;
            game.Building.PlanterPrefab = Model(); game.Building.PlanterBlueprintPrefab = Ghost(game.Building.PlanterPrefab);
            if (first) { var stock = game.Resources.StartingInventory; stock.Potato = Mathf.Max(2, stock.Potato); game.Resources.StartingInventory = stock; EditorUtility.SetDirty(game.Resources); }
            var potato = Potato(false); var baked = Potato(true); UpdateCrew(baked); UpdateGrill(baked);
            var manager = Object.FindObjectOfType<FarmingManager>(); if (manager == null) manager = new GameObject("FarmingManager").AddComponent<FarmingManager>();
            var fields = new SerializedObject(Object.FindObjectOfType<GameManager>()); fields.FindProperty("farmingManager").objectReferenceValue = manager; fields.ApplyModifiedPropertiesWithoutUndo();
            ManagementHudBuilder.Rebuild(); EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.Building); EditorUtility.SetDirty(game.Production); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save farming scene."); Debug.Log("FARMING_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }
        private static GameObject Model()
        {
            var root = new GameObject("Planter");
            try
            {
                var wood = Mat("PlanterWood", new Color(.67f, .45f, .25f)); var rim = Mat("PlanterRim", new Color(.48f, .31f, .17f));
                var soil = Mat("Soil", new Color(.42f, .30f, .18f)); var green = Mat("PotatoLeaves", new Color(.39f, .64f, .30f));
                foreach (float edge in new[] { -.76f, .76f })
                {
                    Part(root.transform, "Wood Wall", PrimitiveType.Cube, new Vector3(edge, .27f, 0), new Vector3(.13f, .43f, 1.65f), wood);
                    Part(root.transform, "Wood Wall", PrimitiveType.Cube, new Vector3(0, .27f, edge), new Vector3(1.65f, .43f, .13f), wood);
                    Part(root.transform, "Rim", PrimitiveType.Cube, new Vector3(edge, .50f, 0), new Vector3(.17f, .07f, 1.70f), rim);
                    Part(root.transform, "Rim", PrimitiveType.Cube, new Vector3(0, .50f, edge), new Vector3(1.70f, .07f, .17f), rim);
                }
                var view = root.AddComponent<PlanterView>();
                view.Soil = Part(root.transform, "Soil Bed", PrimitiveType.Cube, new Vector3(0, .39f, 0), new Vector3(1.42f, .08f, 1.42f), soil).GetComponent<Renderer>();
                view.Plants = new GameObject("Potato Plants").transform; view.Plants.SetParent(root.transform, false); view.Plants.localPosition = new Vector3(0, .43f, 0); view.Sprouts = new Transform[4];
                var leaf = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Decoration_Leaf.asset");
                for (int index = 0; index < 4; index++)
                {
                    var plant = new GameObject("Sprout " + index).transform; plant.SetParent(view.Plants, false); plant.localPosition = new Vector3(index % 2 == 0 ? -.38f : .38f, 0, index < 2 ? -.38f : .38f); view.Sprouts[index] = plant;
                    Part(plant, "Stem", PrimitiveType.Cylinder, new Vector3(0, .17f, 0), new Vector3(.035f, .17f, .035f), green);
                    for (int n = 0; n < 5; n++)
                    {
                        var obj = new GameObject("Leaf", typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(plant, false);
                        obj.transform.localPosition = new Vector3(0, .05f + n * .04f, 0); obj.transform.localRotation = Quaternion.Euler(0, n * 137 + index * 35, 0); obj.transform.localScale = Vector3.one * .65f;
                        obj.GetComponent<MeshFilter>().sharedMesh = leaf; obj.GetComponent<Renderer>().sharedMaterial = green;
                    }
                }
                view.Potatoes = new GameObject("Mature Potatoes"); view.Potatoes.transform.SetParent(root.transform, false);
                for (int i = 0; i < 6; i++) Part(view.Potatoes.transform, "Potato", PrimitiveType.Sphere, new Vector3(-.49f + i % 3 * .49f, .47f, i < 3 ? -.20f : .30f), new Vector3(.24f, .14f, .20f), Mat("PotatoSkin", new Color(.73f, .56f, .31f)));
                view.Present(CropStage.Empty, 0); RemoveColliders(root); return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/Planter.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Ghost(GameObject prefab)
        {
            var root = Object.Instantiate(prefab); root.name = "Planter_Blueprint";
            try
            {
                var view = root.GetComponent<PlanterView>(); Object.DestroyImmediate(view.Plants.gameObject); Object.DestroyImmediate(view.Potatoes); Object.DestroyImmediate(view);
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Raft/Blueprint.mat");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>()) { renderer.sharedMaterial = mat; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                Line(root.transform, "Footprint", mat, new[] { new Vector3(-.93f, .06f, -.93f), new Vector3(-.93f, .06f, .93f), new Vector3(.93f, .06f, .93f), new Vector3(.93f, .06f, -.93f) }, true);
                Line(root.transform, "Work Cell", mat, new[] { new Vector3(-.64f, .06f, 1.36f), new Vector3(-.64f, .06f, 2.64f), new Vector3(.64f, .06f, 2.64f), new Vector3(.64f, .06f, 1.36f) }, true);
                Line(root.transform, "Approach", mat, new[] { new Vector3(0, .07f, 1.05f), new Vector3(0, .07f, 2.1f), new Vector3(-.2f, .07f, 1.9f), new Vector3(0, .07f, 2.1f), new Vector3(.2f, .07f, 1.9f) }, false);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/Planter_Blueprint.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Potato(bool baked)
        {
            string name = baked ? "BakedPotato" : "Potato"; var root = new GameObject(name);
            try
            {
                Part(root.transform, "Skin", PrimitiveType.Sphere, Vector3.zero, new Vector3(.30f, .19f, .23f), Mat(baked ? "BakedSkin" : "PotatoSkin", baked ? new Color(.85f, .56f, .25f) : new Color(.73f, .56f, .31f)));
                if (baked) { Part(root.transform, "Cut Flesh", PrimitiveType.Sphere, new Vector3(0, .075f, .01f), new Vector3(.23f, .07f, .17f), Mat("PotatoFlesh", new Color(.97f, .85f, .53f))); Part(root.transform, "Butter", PrimitiveType.Cube, new Vector3(0, .12f, .01f), new Vector3(.055f, .025f, .045f), Mat("Butter", new Color(1, .91f, .55f))); }
                RemoveColliders(root); return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Farming/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void UpdateCrew(GameObject baked)
        {
            const string path = "Assets/Art/Prefabs/Crew/Sailor.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CrewView>(); var fields = new SerializedObject(view); var arm = (Transform)fields.FindProperty("rightArm").objectReferenceValue;
                if (view.MealPotato == null) { view.MealPotato = Object.Instantiate(baked, arm); view.MealPotato.name = "Baked Potato Meal"; view.MealPotato.transform.localPosition = new Vector3(0, -.32f, .04f); view.MealPotato.transform.localScale = Vector3.one * .7f; view.MealPotato.SetActive(false); }
                if (view.WateringCan == null)
                {
                    var can = new GameObject("Watering Can"); can.transform.SetParent(arm, false); can.transform.localPosition = new Vector3(.03f, -.32f, .08f);
                    var metal = Mat("WateringMetal", new Color(.42f, .65f, .63f)); Part(can.transform, "Can", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.22f, .12f, .22f), metal);
                    var spout = Part(can.transform, "Spout", PrimitiveType.Cylinder, new Vector3(.15f, .045f, .06f), new Vector3(.055f, .13f, .055f), metal); spout.transform.localRotation = Quaternion.Euler(0, 0, -55);
                    Part(can.transform, "Handle", PrimitiveType.Cube, new Vector3(-.14f, .02f, 0), new Vector3(.04f, .18f, .04f), metal);
                    RemoveColliders(can); view.WateringCan = can; can.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void UpdateGrill(GameObject baked)
        {
            const string path = "Assets/Art/Prefabs/Facilities/Grill.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<GrillView>();
                if (view.PotatoFood == null)
                {
                    view.PotatoFood = new GameObject("Potatoes on Grate"); view.PotatoFood.transform.SetParent(root.transform, false); view.PotatoFood.transform.localPosition = new Vector3(0, .98f, 0);
                    for (int index = 0; index < 3; index++) { var meal = Object.Instantiate(baked, view.PotatoFood.transform); meal.transform.localPosition = new Vector3(-.32f + index * .32f, 0, index == 1 ? .10f : -.10f); if (index == 0) view.Potato = meal.GetComponentInChildren<Renderer>(); }
                    view.PotatoFood.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material mat)
        { var part = GameObject.CreatePrimitive(shape); part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale; part.GetComponent<Renderer>().sharedMaterial = mat; return part; }
        private static void RemoveColliders(GameObject root) { foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider); }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/Art/Materials/Farming/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path); if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = name, color = color }; mat.SetFloat("_Glossiness", .05f); AssetDatabase.CreateAsset(mat, path); return mat;
        }
        private static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; Folder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); }
        private static void Line(Transform parent, string name, Material mat, Vector3[] points, bool loop)
        { var obj = new GameObject(name, typeof(LineRenderer)); obj.transform.SetParent(parent, false); var line = obj.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = loop; line.widthMultiplier = .035f; line.positionCount = points.Length; line.SetPositions(points); line.sharedMaterial = mat; }
    }
}
