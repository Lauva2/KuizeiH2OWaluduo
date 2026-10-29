using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class SalvageSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Salvage And Inventory")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(RaftSceneBuilder.ScenePath)) CrewSceneBuilder.Install();
            EnsureFolder("Assets/Art/Prefabs/Salvage");
            EnsureFolder("Assets/Art/Materials/Salvage");
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            ResourceConfig inventory = LoadOrCreate<ResourceConfig>("Assets/Configs/Resources_Default.asset");
            SalvageConfig salvage = LoadOrCreate<SalvageConfig>("Assets/Configs/Salvage_Default.asset");
            if (salvage.WoodPrefab == null) salvage.WoodPrefab = CreateItem(ResourceType.Wood);
            if (salvage.ScrapPrefab == null) salvage.ScrapPrefab = CreateItem(ResourceType.Scrap);
            EditorUtility.SetDirty(salvage);
            GameConfig gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            BuildConfig build = gameConfig.Building;
            if (gameConfig.Resources == null) build.DeckCost = new ResourceCost(4, 1);
            gameConfig.Resources = inventory;
            gameConfig.Salvage = salvage;
            EditorUtility.SetDirty(build);
            EditorUtility.SetDirty(gameConfig);
            ResourceManager resourceManager = UnityEngine.Object.FindObjectOfType<ResourceManager>();
            if (resourceManager == null) resourceManager = new GameObject("ResourceManager").AddComponent<ResourceManager>();
            SalvageManager salvageManager = UnityEngine.Object.FindObjectOfType<SalvageManager>();
            if (salvageManager == null) salvageManager = new GameObject("SalvageManager").AddComponent<SalvageManager>();
            GameManager game = UnityEngine.Object.FindObjectOfType<GameManager>();
            Assign(game, "resourceManager", resourceManager);
            Assign(game, "salvageManager", salvageManager);
            UpdatePanel();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save salvage setup.");
            Debug.Log("SALVAGE_SCENE_OK");
            if (Application.isBatchMode)
            {
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }

        private static GameObject CreateItem(ResourceType type)
        {
            string name = type == ResourceType.Wood ? "Drift_Wood" : "Drift_Scrap";
            string path = "Assets/Art/Prefabs/Salvage/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(name, typeof(FloatingResource), typeof(BoxCollider));
            Material wood = Material("DriftWood", new Color(0.65f, 0.37f, 0.16f));
            Material rope = Material("Rope", new Color(0.82f, 0.67f, 0.40f));
            if (type == ResourceType.Wood)
            {
                foreach (float z in new[] { -0.16f, 0.16f })
                {
                    GameObject log = Part("Log", PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.10f, z), new Vector3(0.28f, 0.65f, 0.28f), wood);
                    log.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                }
                foreach (float x in new[] { -0.32f, 0.32f })
                    Part("Lashing", PrimitiveType.Cube, root.transform, new Vector3(x, 0.10f, 0), new Vector3(0.065f, 0.29f, 0.63f), rope);
                root.GetComponent<BoxCollider>().size = new Vector3(1.36f, 0.5f, 0.7f);
            }
            else
            {
                Material metal = Material("ScrapMetal", new Color(0.42f, 0.52f, 0.51f));
                Material rust = Material("RustBand", new Color(0.74f, 0.34f, 0.12f));
                Part("Metal Box", PrimitiveType.Cube, root.transform, new Vector3(0, 0.08f, 0), new Vector3(0.64f, 0.34f, 0.60f), metal);
                foreach (float x in new[] { -0.21f, 0.21f })
                    Part("Band", PrimitiveType.Cube, root.transform, new Vector3(x, 0.09f, 0), new Vector3(0.055f, 0.36f, 0.63f), rust);
                Part("Handle", PrimitiveType.Cube, root.transform, new Vector3(0, 0.28f, 0), new Vector3(0.23f, 0.07f, 0.08f), rust);
                root.GetComponent<BoxCollider>().size = new Vector3(0.75f, 0.6f, 0.72f);
            }
            root.GetComponent<BoxCollider>().center = new Vector3(0, 0.1f, 0);
            GameObject marker = new GameObject("Salvage Mark", typeof(LineRenderer));
            marker.transform.SetParent(root.transform, false);
            var line = marker.GetComponent<LineRenderer>();
            line.sharedMaterial = Material("MarkedGold", new Color(1f, 0.82f, 0.26f), true);
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.035f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.positionCount = 32;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.79f, 0.38f, Mathf.Sin(angle) * 0.68f));
            }
            marker.SetActive(false);
            Assign(root.GetComponent<FloatingResource>(), "marker", marker);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void UpdatePanel()
        {
            const string path = "Assets/Art/Prefabs/UI/UICommon_Build.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                UICommon_Build view = root.GetComponent<UICommon_Build>();
                if (view.WoodText == null)
                {
                    RectTransform card = Rect("Inventory Card", root.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(0, -28), new Vector2(470, 95));
                    card.gameObject.AddComponent<Image>().color = new Color(0.98f, 0.95f, 0.87f, 0.97f);
                    var shadow = card.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0.03f, 0.15f, 0.14f, 0.22f);
                    shadow.effectDistance = new Vector2(0, -4);
                    Label("物资", card, new Vector2(22, -15), new Vector2(80, 28), 21);
                    view.WoodText = Label("木材 8 · 可用 8 · 预留 0", card, new Vector2(100, -15), new Vector2(350, 26), 17);
                    view.ScrapText = Label("废料 2 · 可用 2 · 预留 0", card, new Vector2(100, -54), new Vector2(350, 26), 17);
                }
                if (view.CostText == null)
                    view.CostText = Label("每格：木材 4 + 废料 1", view.ModeText.transform.parent,
                        new Vector2(190, -18), new Vector2(300, 23), 14);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            RectTransform rect = Rect(value, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = new Color(0.18f, 0.29f, 0.27f);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = "Assets/Art/Materials/Salvage/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(unlit ? "Unlit/Color" : "Standard")) { name = name, color = color };
            if (!unlit) material.SetFloat("_Glossiness", 0.12f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); }
            return value;
        }

        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
