using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Production;

namespace RaftGame.Editor
{
    public static class ProgressionSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Water, Time and Saves")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            game.Water = AssetDatabase.LoadAssetAtPath<WaterConfig>("Assets/Configs/Water_Default.asset");
            if (game.Water == null)
            { game.Water = ScriptableObject.CreateInstance<WaterConfig>(); AssetDatabase.CreateAsset(game.Water, "Assets/Configs/Water_Default.asset"); }
            var owner = Object.FindObjectOfType<GameManager>();
            var water = Object.FindObjectOfType<WaterManager>();
            if (water == null) water = new GameObject("WaterManager").AddComponent<WaterManager>();
            Assign(owner, "waterManager", water);
            game.Building.PurifierPrefab = CreatePurifier();
            game.Building.PurifierBlueprintPrefab = CreateGhost(game.Building.PurifierPrefab);
            game.Building.PurifierCost = new RaftGame.Resources.ResourceCost(6, 3);
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.Building); EditorUtility.SetDirty(game.Needs);
            AddCup(); ManagementHudBuilder.Rebuild(); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save progression scene.");
            Debug.Log("PROGRESSION_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }

        private static GameObject CreatePurifier()
        {
            const string path = "Assets/Art/Prefabs/Facilities/Purifier.prefab";
            var root = new GameObject("Purifier", typeof(PurifierView));
            try
            {
                Material wood = Mat("PurifierWood", new Color(0.63f, 0.38f, 0.20f));
                Material steel = Mat("PurifierSteel", new Color(0.45f, 0.58f, 0.56f));
                Material dark = Mat("PurifierIron", new Color(0.18f, 0.29f, 0.28f));
                Material cream = Mat("PurifierCanvas", new Color(0.94f, 0.87f, 0.67f));
                Material blue = Mat("CleanWater", new Color(0.22f, 0.65f, 0.78f));
                Part("Platform", PrimitiveType.Cube, root.transform, new Vector3(0, .17f, 0), new Vector3(1.6f, .14f, 1.35f), wood);
                foreach (float x in new[] { -.64f, .64f }) foreach (float z in new[] { -.48f, .48f })
                    Part("Leg", PrimitiveType.Cube, root.transform, new Vector3(x, .12f, z), new Vector3(.12f, .24f, .12f), dark);
                Part("Boiler", PrimitiveType.Cylinder, root.transform, new Vector3(-.35f, .53f, 0), new Vector3(.63f, .27f, .63f), dark);
                Part("Copper Lid", PrimitiveType.Cylinder, root.transform, new Vector3(-.35f, .82f, 0), new Vector3(.69f, .045f, .69f), steel);
                Part("Lid Grip", PrimitiveType.Cube, root.transform, new Vector3(-.35f, .92f, 0), new Vector3(.22f, .09f, .09f), wood);
                Part("Warm Fire", PrimitiveType.Sphere, root.transform, new Vector3(-.35f, .29f, .25f), new Vector3(.32f, .12f, .12f), Mat("PurifierEmber", new Color(.91f, .45f, .17f)));
                Part("Collection Bowl", PrimitiveType.Cylinder, root.transform, new Vector3(.46f, .39f, .05f), new Vector3(.49f, .15f, .49f), cream);
                var waterRoot = new GameObject("Clean Water"); waterRoot.transform.SetParent(root.transform, false); waterRoot.transform.localPosition = new Vector3(.46f, .50f, .05f);
                Part("Surface", PrimitiveType.Cylinder, waterRoot.transform, Vector3.zero, new Vector3(.42f, .025f, .42f), blue);
                root.GetComponent<PurifierView>().CleanWater = waterRoot.transform;
                foreach (float z in new[] { -.48f, .48f })
                    Part("Roof Post", PrimitiveType.Cube, root.transform, new Vector3(-.62f, 1.05f, z), new Vector3(.065f, 1.1f, .065f), wood);
                var roof = Part("Condenser Canopy", PrimitiveType.Cube, root.transform, new Vector3(.05f, 1.28f, 0), new Vector3(1.43f, .055f, 1.12f), cream);
                roof.transform.localRotation = Quaternion.Euler(0, 0, -20);
                for (int i = -1; i <= 1; i++)
                {
                    var strip = Part("Canopy Seam", PrimitiveType.Cube, roof.transform, new Vector3(0, .55f, i * .25f), new Vector3(.98f, .1f, .015f), steel);
                    strip.transform.localRotation = Quaternion.identity;
                }
                var pipe = Part("Drip Spout", PrimitiveType.Cube, root.transform, new Vector3(.73f, 1.01f, .05f), new Vector3(.10f, .25f, .10f), steel);
                var steam = new GameObject("Steam"); steam.transform.SetParent(root.transform, false);
                for (int i = 0; i < 3; i++)
                    Part("Steam Puff", PrimitiveType.Sphere, steam.transform, new Vector3(-.35f + i * .03f, 1.0f + i * .10f, .1f), Vector3.one * (.07f + i * .015f), Mat("SteamPearl", new Color(.84f, .91f, .86f)));
                root.GetComponent<PurifierView>().Steam = steam; steam.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateGhost(GameObject prefab)
        {
            var root = Object.Instantiate(prefab); root.name = "Purifier_Blueprint";
            Object.DestroyImmediate(root.GetComponent<PurifierView>());
            var ghost = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Raft/Blueprint.mat");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) { renderer.sharedMaterial = ghost; renderer.shadowCastingMode = ShadowCastingMode.Off; }
            Line(root.transform, "Footprint", ghost, new[] { new Vector3(-.9f, .03f, -.9f), new Vector3(-.9f, .03f, .9f), new Vector3(.9f, .03f, .9f), new Vector3(.9f, .03f, -.9f), new Vector3(-.9f, .03f, -.9f) });
            Line(root.transform, "Work Cell", ghost, new[] { new Vector3(-.65f, .04f, 1.35f), new Vector3(-.65f, .04f, 2.65f), new Vector3(.65f, .04f, 2.65f), new Vector3(.65f, .04f, 1.35f), new Vector3(-.65f, .04f, 1.35f) });
            Line(root.transform, "Approach Arrow", ghost, new[] { new Vector3(0, .05f, 1.1f), new Vector3(0, .05f, 2.15f), new Vector3(-.22f, .05f, 1.9f), new Vector3(0, .05f, 2.15f), new Vector3(.22f, .05f, 1.9f) });
            var saved = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/Purifier_Blueprint.prefab"); Object.DestroyImmediate(root); return saved;
        }
        private static void AddCup()
        {
            const string path = "Assets/Art/Prefabs/Crew/Sailor.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CrewView>();
                if (view.DrinkCup != null) return;
                var hand = (Transform)new SerializedObject(view).FindProperty("rightArm").objectReferenceValue;
                var cup = new GameObject("Drinking Cup"); cup.transform.SetParent(hand, false); cup.transform.localPosition = new Vector3(0, -.30f, .08f);
                var ceramic = Mat("CupCeramic", new Color(.87f, .90f, .81f));
                Part("Mug", PrimitiveType.Cylinder, cup.transform, Vector3.zero, new Vector3(.15f, .10f, .15f), ceramic);
                Part("Water", PrimitiveType.Cylinder, cup.transform, new Vector3(0, .10f, 0), new Vector3(.12f, .008f, .12f), Mat("CleanWater", Color.cyan));
                Part("Handle", PrimitiveType.Cube, cup.transform, new Vector3(.1f, 0, 0), new Vector3(.06f, .08f, .04f), ceramic);
                view.DrinkCup = cup; cup.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = mat; Object.DestroyImmediate(part.GetComponent<Collider>()); return part;
        }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/Art/Materials/Cooking/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = name, color = color }; mat.SetFloat("_Glossiness", .18f); AssetDatabase.CreateAsset(mat, path); return mat;
        }
        private static void Line(Transform parent, string name, Material mat, Vector3[] points)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(parent, false); line.useWorldSpace = false;
            line.sharedMaterial = mat; line.positionCount = points.Length; line.SetPositions(points); line.widthMultiplier = .045f; line.shadowCastingMode = ShadowCastingMode.Off;
        }
        private static void Assign(Object target, string field, Object value)
        { var fields = new SerializedObject(target); fields.FindProperty(field).objectReferenceValue = value; fields.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
