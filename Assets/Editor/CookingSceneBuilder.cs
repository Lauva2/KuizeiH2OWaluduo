using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Production;
using RaftGame.Needs;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class CookingSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Grill and Meals")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(RaftSceneBuilder.ScenePath)) FishingSceneBuilder.Install();
            GameConfig game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            if (game.Fishing == null) FishingSceneBuilder.Install();
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            Folder("Assets/Art/Materials/Cooking"); Folder("Assets/Art/Prefabs/Facilities");
            game.Production = Config<ProductionConfig>("Assets/Configs/Production_Default.asset");
            game.Needs = Config<NeedsConfig>("Assets/Configs/Needs_Default.asset");
            EditorUtility.SetDirty(game);
            var production = UnityEngine.Object.FindObjectOfType<ProductionManager>();
            if (production == null) production = new GameObject("ProductionManager").AddComponent<ProductionManager>();
            var needs = UnityEngine.Object.FindObjectOfType<NeedsManager>();
            if (needs == null) needs = new GameObject("NeedsManager").AddComponent<NeedsManager>();
            var owner = UnityEngine.Object.FindObjectOfType<GameManager>();
            Assign(owner, "productionManager", production); Assign(owner, "needsManager", needs);
            game.Building.GrillPrefab = CreateGrill();
            game.Building.GrillBlueprintPrefab = CreateGhost(game.Building.GrillPrefab);
            EditorUtility.SetDirty(game.Building);
            UpdateSailor(); UpdatePanel();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save cooking setup.");
            Debug.Log("COOKING_SCENE_OK");
            if (Application.isBatchMode)
            { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }

        private static T Config<T>(string path) where T : ScriptableObject
        {
            T config = AssetDatabase.LoadAssetAtPath<T>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(config, path); }
            return config;
        }

        private static GameObject CreateGrill()
        {
            const string path = "Assets/Art/Prefabs/Facilities/Grill.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Grill", typeof(GrillView));
            Material iron = Mat("DarkIron", new Color(0.19f, 0.28f, 0.28f));
            Material rail = Mat("Steel", new Color(0.48f, 0.57f, 0.55f));
            Material wood = Mat("WarmWood", new Color(0.69f, 0.40f, 0.18f));
            Material coal = Mat("Embers", new Color(0.38f, 0.15f, 0.07f));
            foreach (float x in new[] { -0.47f, 0.47f }) foreach (float z in new[] { -0.38f, 0.38f })
                Part("Leg", PrimitiveType.Cylinder, root.transform, new Vector3(x, 0.35f, z), new Vector3(0.09f, 0.35f, 0.09f), rail);
            Part("Brazier", PrimitiveType.Cube, root.transform, new Vector3(0, 0.65f, 0), new Vector3(1.15f, 0.24f, 0.96f), iron);
            Part("Charcoal", PrimitiveType.Cube, root.transform, new Vector3(0, 0.79f, 0), new Vector3(0.96f, 0.07f, 0.76f), coal);
            for (int i = -4; i <= 4; i++)
                Part("Grate Bar", PrimitiveType.Cube, root.transform, new Vector3(i * 0.115f, 0.85f, 0), new Vector3(0.032f, 0.035f, 0.86f), rail);
            foreach (float z in new[] { -0.48f, 0.48f })
                Part("Rim", PrimitiveType.Cube, root.transform, new Vector3(0, 0.86f, z), new Vector3(1.2f, 0.075f, 0.06f), iron);
            Part("Side Shelf", PrimitiveType.Cube, root.transform, new Vector3(-0.77f, 0.82f, 0), new Vector3(0.3f, 0.09f, 0.95f), wood);
            var food = new GameObject("Fish on Grate"); food.transform.SetParent(root.transform, false);
            food.transform.localPosition = new Vector3(0, 0.94f, 0);
            GameObject fish = Fish(food.transform, Mat("FreshFish", new Color(0.46f, 0.74f, 0.73f)));
            var fire = new GameObject("Cooking Glow", typeof(Light)).GetComponent<Light>();
            fire.transform.SetParent(root.transform, false); fire.transform.localPosition = new Vector3(0, 0.95f, 0);
            fire.color = new Color(1f, 0.53f, 0.19f); fire.range = 2.2f; fire.intensity = 1.4f;
            fire.shadows = LightShadows.None; fire.enabled = false;
            Assign(root.GetComponent<GrillView>(), "food", food); Assign(root.GetComponent<GrillView>(), "fireLight", fire);
            Assign(root.GetComponent<GrillView>(), "fish", fish.GetComponent<Renderer>());
            food.SetActive(false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        private static GameObject CreateGhost(GameObject prefab)
        {
            const string path = "Assets/Art/Prefabs/Facilities/Grill_Blueprint.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = UnityEngine.Object.Instantiate(prefab); root.name = "Grill_Blueprint";
            UnityEngine.Object.DestroyImmediate(root.GetComponent<GrillView>());
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light.gameObject);
            Material ghost = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Raft/Blueprint.mat");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            { renderer.sharedMaterial = ghost; renderer.shadowCastingMode = ShadowCastingMode.Off; }
            Line(root.transform, "Footprint", ghost, new[] { new Vector3(-0.91f, 0.03f, -0.91f), new Vector3(-0.91f, 0.03f, 0.91f),
                new Vector3(0.91f, 0.03f, 0.91f), new Vector3(0.91f, 0.03f, -0.91f), new Vector3(-0.91f, 0.03f, -0.91f) });
            Line(root.transform, "Work Cell", ghost, new[] { new Vector3(-0.65f, 0.04f, 1.35f), new Vector3(-0.65f, 0.04f, 2.65f),
                new Vector3(0.65f, 0.04f, 2.65f), new Vector3(0.65f, 0.04f, 1.35f), new Vector3(-0.65f, 0.04f, 1.35f) });
            Line(root.transform, "Approach Arrow", ghost, new[] { new Vector3(0, 0.05f, 1.1f), new Vector3(0, 0.05f, 2.15f),
                new Vector3(-0.22f, 0.05f, 1.9f), new Vector3(0, 0.05f, 2.15f), new Vector3(0.22f, 0.05f, 1.9f) });
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); return saved;
        }

        private static void UpdateSailor()
        {
            const string path = "Assets/Art/Prefabs/Crew/Sailor.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CrewView>(); var fields = new SerializedObject(view);
                Transform hand = (Transform)fields.FindProperty("rightArm").objectReferenceValue;
                if (fields.FindProperty("cookingTool").objectReferenceValue == null)
                {
                    var tool = new GameObject("Cooking Tongs"); tool.transform.SetParent(hand, false);
                    tool.transform.localPosition = new Vector3(0, -0.32f, 0.02f);
                    foreach (float x in new[] { -0.025f, 0.025f })
                        Part("Tong", PrimitiveType.Cube, tool.transform, new Vector3(x, 0, 0.15f), new Vector3(0.025f, 0.025f, 0.35f), Mat("Steel", Color.gray));
                    Assign(view, "cookingTool", tool); tool.SetActive(false);
                }
                if (fields.FindProperty("mealFood").objectReferenceValue == null)
                {
                    var food = new GameObject("Cooked Fish Meal"); food.transform.SetParent(hand, false);
                    food.transform.localPosition = new Vector3(0, -0.32f, 0.04f); food.transform.localScale = Vector3.one * 0.6f;
                    Fish(food.transform, Mat("CookedFish", new Color(0.97f, 0.61f, 0.23f)));
                    Assign(view, "mealFood", food); food.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpdatePanel()
        {
            const string path = "Assets/Art/Prefabs/UI/UICommon_Build.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<UICommon_Build>();
                var inventory = (RectTransform)view.WoodText.transform.parent; inventory.sizeDelta = new Vector2(470, 170);
                if (view.CookedFishText == null) view.CookedFishText = Label("熟鱼 0 · 目标 4", inventory, new Vector2(100, -132), new Vector2(235, 26), 17);
                if (view.TargetMinusButton == null) view.TargetMinusButton = Button("−", inventory, new Vector2(340, -132), new Vector2(30, 26), 20);
                if (view.TargetPlusButton == null) view.TargetPlusButton = Button("+", inventory, new Vector2(390, -132), new Vector2(30, 26), 20);
                var counts = (RectTransform)view.CompletedText.transform.parent; counts.sizeDelta = new Vector2(260, 170);
                if (view.GrillText == null) view.GrillText = Label("已建烤架 0", counts, new Vector2(22, -91), new Vector2(220, 23), 17);
                if (view.NeedsText == null) view.NeedsText = Label("饱腹 70% · 效率 100%", counts, new Vector2(22, -126), new Vector2(224, 26), 16);
                Transform toolbar = view.BuildButton.transform.parent;
                if (view.GrillButton == null) view.GrillButton = Button("建造烤架  G", toolbar, new Vector2(205, -52), new Vector2(165, 48), 17);
                Button[] buttons = { view.BuildButton, view.GrillButton, view.CancelButton, view.FishingButton };
                for (int i = 0; i < buttons.Length; i++)
                {
                    var rect = buttons[i].GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(24 + i * 181, -52); rect.sizeDelta = new Vector2(165, 48);
                    Text label = buttons[i].GetComponentInChildren<Text>(); label.rectTransform.sizeDelta = new Vector2(165, 48); label.fontSize = 17;
                }
                view.ModeText.fontSize = 18;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject Fish(Transform parent, Material material)
        {
            GameObject body = Part("Fish", PrimitiveType.Sphere, parent, Vector3.zero, new Vector3(0.25f, 0.13f, 0.60f), material);
            Part("Tail", PrimitiveType.Cube, parent, new Vector3(0, 0, -0.3f), new Vector3(0.29f, 0.055f, 0.18f), material);
            Part("Eye", PrimitiveType.Sphere, parent, new Vector3(-0.075f, 0.055f, 0.15f), Vector3.one * 0.045f, Mat("DarkIron", Color.black));
            return body;
        }
        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material; UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>()); return part;
        }
        private static void Line(Transform parent, string name, Material material, Vector3[] points)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.useWorldSpace = false; line.sharedMaterial = material; line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = 0.045f; line.shadowCastingMode = ShadowCastingMode.Off;
        }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/Art/Materials/Cooking/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = name, color = color }; mat.SetFloat("_Glossiness", 0.15f);
            AssetDatabase.CreateAsset(mat, path); return mat;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(value, parent, position, size).gameObject.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = fontSize;
            label.color = new Color(0.18f, 0.29f, 0.27f); label.text = value; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; return label;
        }
        private static Button Button(string value, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Rect(value, parent, position, size); rect.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.34f, 0.32f);
            var button = rect.gameObject.AddComponent<Button>(); Text text = Label(value, rect, Vector2.zero, size, fontSize);
            text.color = new Color(1f, 0.98f, 0.91f); text.alignment = TextAnchor.MiddleCenter; return button;
        }
        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var fields = new SerializedObject(target); fields.FindProperty(field).objectReferenceValue = value; fields.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
