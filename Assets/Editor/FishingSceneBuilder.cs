using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Crew;
using RaftGame.Fishing;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class FishingSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Starter Fishing Rod")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(RaftSceneBuilder.ScenePath)) SalvageSceneBuilder.Install();
            GameConfig gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            if (gameConfig.Crew == null) CrewSceneBuilder.Install();
            if (gameConfig.Resources == null) SalvageSceneBuilder.Install();
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            EnsureFolder("Assets/Art/Prefabs/Tools");
            EnsureFolder("Assets/Art/Materials/Fishing");
            const string path = "Assets/Configs/Fishing_Default.asset";
            var config = AssetDatabase.LoadAssetAtPath<FishingConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<FishingConfig>(); AssetDatabase.CreateAsset(config, path); }
            gameConfig.Fishing = config;
            EditorUtility.SetDirty(gameConfig);
            var manager = UnityEngine.Object.FindObjectOfType<FishingManager>();
            if (manager == null) manager = new GameObject("FishingManager").AddComponent<FishingManager>();
            Assign(UnityEngine.Object.FindObjectOfType<GameManager>(), "fishingManager", manager);
            UpdateSailor(CreateRod());
            UpdatePanel();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save fishing setup.");
            Debug.Log("FISHING_SCENE_OK");
            if (Application.isBatchMode)
            {
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }

        private static GameObject CreateRod()
        {
            const string path = "Assets/Art/Prefabs/Tools/FishingRod.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var rod = new GameObject("Fishing Rod");
            Material bamboo = Material("BambooRod", new Color(0.63f, 0.40f, 0.16f));
            Material grip = Material("RodGrip", new Color(0.27f, 0.17f, 0.10f));
            Material iron = Material("RodRings", new Color(0.50f, 0.59f, 0.56f));
            Part("Grip", PrimitiveType.Cylinder, rod.transform, new Vector3(0, 0, 0.10f), new Vector3(0.065f, 0.16f, 0.065f), grip, Quaternion.Euler(90, 0, 0));
            Part("Lower Pole", PrimitiveType.Cylinder, rod.transform, new Vector3(0, 0, 0.45f), new Vector3(0.035f, 0.33f, 0.035f), bamboo, Quaternion.Euler(90, 0, 0));
            Part("Upper Pole", PrimitiveType.Cylinder, rod.transform, new Vector3(0, 0, 1.06f), new Vector3(0.020f, 0.31f, 0.020f), bamboo, Quaternion.Euler(90, 0, 0));
            foreach (float z in new[] { 0.24f, 0.76f, 1.32f })
                Part("Guide", PrimitiveType.Sphere, rod.transform, new Vector3(0, 0, z), Vector3.one * 0.045f, iron, Quaternion.identity);
            Part("Reel", PrimitiveType.Cylinder, rod.transform, new Vector3(0.055f, -0.025f, 0.15f), new Vector3(0.12f, 0.035f, 0.12f), iron, Quaternion.Euler(0, 0, 90));
            Transform tip = new GameObject("Rod Tip").transform;
            tip.SetParent(rod.transform, false);
            tip.localPosition = new Vector3(0, 0, 1.38f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rod, path);
            UnityEngine.Object.DestroyImmediate(rod);
            return prefab;
        }

        private static void UpdateSailor(GameObject rodPrefab)
        {
            const string path = "Assets/Art/Prefabs/Crew/Sailor.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CrewView view = root.GetComponent<CrewView>();
                var fields = new SerializedObject(view);
                if (fields.FindProperty("fishingRod").objectReferenceValue == null)
                {
                    Transform arm = (Transform)fields.FindProperty("rightArm").objectReferenceValue;
                    GameObject rod = (GameObject)PrefabUtility.InstantiatePrefab(rodPrefab, arm);
                    rod.transform.localPosition = new Vector3(0, -0.32f, 0.04f);
                    rod.transform.localRotation = Quaternion.identity;
                    rod.SetActive(false);
                    Assign(view, "fishingRod", rod);
                    Assign(view, "rodTip", rod.transform.Find("Rod Tip"));

                    var lineObject = new GameObject("Fishing Line", typeof(LineRenderer));
                    lineObject.transform.SetParent(root.transform, false);
                    var line = lineObject.GetComponent<LineRenderer>();
                    line.sharedMaterial = Material("FishingLine", new Color(0.81f, 0.88f, 0.81f), true);
                    line.useWorldSpace = true;
                    line.positionCount = 3;
                    line.widthMultiplier = 0.014f;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    lineObject.SetActive(false);
                    Assign(view, "fishingLine", line);

                    Transform bobber = new GameObject("Fishing Bobber").transform;
                    bobber.SetParent(root.transform, false);
                    Material red = Material("BobberCoral", new Color(0.93f, 0.31f, 0.21f));
                    Material cream = Material("BobberCream", new Color(0.98f, 0.93f, 0.76f));
                    Part("Float", PrimitiveType.Sphere, bobber, Vector3.zero, new Vector3(0.14f, 0.18f, 0.14f), red, Quaternion.identity);
                    Part("Float Top", PrimitiveType.Sphere, bobber, new Vector3(0, 0.075f, 0), new Vector3(0.10f, 0.10f, 0.10f), cream, Quaternion.identity);
                    bobber.gameObject.SetActive(false);
                    Assign(view, "bobber", bobber);
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
                UICommon_Build view = root.GetComponent<UICommon_Build>();
                if (view.FishText == null)
                {
                    RectTransform card = (RectTransform)view.WoodText.transform.parent;
                    card.sizeDelta = new Vector2(card.sizeDelta.x, 132);
                    view.FishText = Label("鲜鱼 0 · 鱼竿 1", card, new Vector2(100, -93), new Vector2(350, 26), 17);
                }
                if (view.FishingButton == null)
                {
                    var buildRect = view.BuildButton.GetComponent<RectTransform>();
                    var cancelRect = view.CancelButton.GetComponent<RectTransform>();
                    buildRect.sizeDelta = cancelRect.sizeDelta = new Vector2(225, 48);
                    cancelRect.anchoredPosition = new Vector2(267, -52);
                    ResizeButtonText(view.BuildButton);
                    ResizeButtonText(view.CancelButton);
                    RectTransform rect = Rect("Fishing Toggle", buildRect.parent, new Vector2(510, -52), new Vector2(225, 48));
                    rect.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.50f, 0.44f);
                    view.FishingButton = rect.gameObject.AddComponent<Button>();
                    view.FishingButtonText = Label("空闲钓鱼：开  F", rect, Vector2.zero, new Vector2(225, 48), 18);
                    view.FishingButtonText.color = new Color(1f, 0.98f, 0.91f);
                    view.FishingButtonText.alignment = TextAnchor.MiddleCenter;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ResizeButtonText(Button button)
        {
            Text label = button.GetComponentInChildren<Text>();
            label.rectTransform.sizeDelta = new Vector2(225, 48);
            label.fontSize = 18;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            Text label = Rect(value, parent, position, size).gameObject.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = new Color(0.18f, 0.29f, 0.27f);
            label.text = value;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = rotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = "Assets/Art/Materials/Fishing/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(unlit ? "Unlit/Color" : "Standard")) { name = name, color = color };
            if (!unlit) material.SetFloat("_Glossiness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var fields = new SerializedObject(target);
            fields.FindProperty(field).objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
