using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Crew;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class CrewSceneBuilder
    {
        private const string ConfigPath = "Assets/Configs/Crew_Default.asset";
        private const string PrefabPath = "Assets/Art/Prefabs/Crew/Sailor.prefab";

        [MenuItem("Tools/Raft Game/Add Crew To Raft Preview")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(RaftSceneBuilder.ScenePath)) RaftSceneBuilder.Build();
            EnsureFolder("Assets/Art/Prefabs/Crew");
            EnsureFolder("Assets/Art/Materials/Crew");
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            CrewConfig config = AssetDatabase.LoadAssetAtPath<CrewConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CrewConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            if (config.CrewPrefab == null) config.CrewPrefab = CreateSailor();
            EditorUtility.SetDirty(config);
            GameConfig gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            gameConfig.Crew = config;
            EditorUtility.SetDirty(gameConfig);
            CrewManager manager = UnityEngine.Object.FindObjectOfType<CrewManager>();
            if (manager == null) manager = new GameObject("CrewManager").AddComponent<CrewManager>();
            var serialized = new SerializedObject(UnityEngine.Object.FindObjectOfType<GameManager>());
            serialized.FindProperty("crewManager").objectReferenceValue = manager;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UpdatePanel();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save crew setup.");
            Debug.Log("CREW_SCENE_OK");
            if (Application.isBatchMode)
            {
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void InstallAndBuildContent()
        {
            Install();
            RaftSceneBuilder.BuildWithContent();
        }

        private static GameObject CreateSailor()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            Material skin = Material("Skin", new Color(0.82f, 0.54f, 0.34f));
            Material shirt = Material("SeaTeal", new Color(0.12f, 0.43f, 0.42f));
            Material pants = Material("Trousers", new Color(0.12f, 0.20f, 0.24f));
            Material cream = Material("Canvas", new Color(0.91f, 0.83f, 0.61f));
            Material dark = Material("BootsAndEyes", new Color(0.12f, 0.09f, 0.065f));
            Material hair = Material("Hair", new Color(0.26f, 0.13f, 0.055f));
            Material wood = Material("HammerWood", new Color(0.51f, 0.29f, 0.12f));
            Material metal = Material("HammerIron", new Color(0.35f, 0.40f, 0.39f));
            var sailor = new GameObject("Sailor", typeof(CrewView));
            Transform body = Pivot("Body", sailor.transform, Vector3.zero);
            Part("Shirt", PrimitiveType.Capsule, body, new Vector3(0, 0.69f, 0), new Vector3(0.44f, 0.255f, 0.33f), shirt);
            Part("Belt", PrimitiveType.Cube, body, new Vector3(0, 0.48f, 0), new Vector3(0.36f, 0.07f, 0.29f), cream);
            Part("Head", PrimitiveType.Sphere, body, new Vector3(0, 1.08f, 0.02f), new Vector3(0.46f, 0.46f, 0.42f), skin);
            Part("Hair", PrimitiveType.Sphere, body, new Vector3(0, 1.20f, -0.035f), new Vector3(0.47f, 0.29f, 0.40f), hair);
            Part("Cap", PrimitiveType.Sphere, body, new Vector3(0, 1.30f, -0.005f), new Vector3(0.52f, 0.17f, 0.46f), cream);
            Part("Cap Brim", PrimitiveType.Cube, body, new Vector3(0, 1.28f, 0.15f), new Vector3(0.48f, 0.045f, 0.29f), cream);
            Part("Cap Band", PrimitiveType.Cube, body, new Vector3(0, 1.25f, 0.13f), new Vector3(0.39f, 0.055f, 0.16f), shirt);
            Part("Nose", PrimitiveType.Sphere, body, new Vector3(0, 1.06f, 0.225f), Vector3.one * 0.075f, skin);
            foreach (float x in new[] { -0.085f, 0.085f })
                Part("Eye", PrimitiveType.Sphere, body, new Vector3(x, 1.13f, 0.207f), new Vector3(0.033f, 0.045f, 0.025f), dark);
            Transform leftArm = Arm("Left Arm", body, -1f, shirt, skin);
            Transform rightArm = Arm("Right Arm", body, 1f, shirt, skin);
            Transform leftLeg = Leg("Left Leg", body, -1f, pants, dark);
            Transform rightLeg = Leg("Right Leg", body, 1f, pants, dark);
            Transform hammer = Pivot("Hammer", rightArm, new Vector3(0, -0.33f, 0.07f));
            Part("Handle", PrimitiveType.Cylinder, hammer, new Vector3(0, 0.02f, 0.08f), new Vector3(0.045f, 0.17f, 0.045f), wood);
            Part("Head", PrimitiveType.Cube, hammer, new Vector3(0, 0.18f, 0.08f), new Vector3(0.22f, 0.095f, 0.09f), metal);
            hammer.gameObject.SetActive(false);

            Transform progress = Pivot("Construction Progress", sailor.transform, Vector3.zero);
            Material barBackground = Material("ProgressBackground", new Color(0.08f, 0.18f, 0.18f), true);
            Material barFill = Material("ProgressFill", new Color(0.40f, 0.90f, 0.67f), true);
            Part("Background", PrimitiveType.Cube, progress, Vector3.zero, new Vector3(1.04f, 0.15f, 0.03f), barBackground);
            Transform fill = Part("Fill", PrimitiveType.Cube, progress, new Vector3(-0.48f, 0, -0.025f), new Vector3(0, 0.09f, 0.025f), barFill).transform;
            progress.gameObject.SetActive(false);

            CrewView view = sailor.GetComponent<CrewView>();
            Assign(view, "body", body);
            Assign(view, "leftArm", leftArm);
            Assign(view, "rightArm", rightArm);
            Assign(view, "leftLeg", leftLeg);
            Assign(view, "rightLeg", rightLeg);
            Assign(view, "hammer", hammer.gameObject);
            Assign(view, "progressRoot", progress);
            Assign(view, "progressFill", fill);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sailor, PrefabPath);
            UnityEngine.Object.DestroyImmediate(sailor);
            return prefab;
        }

        private static Transform Arm(string name, Transform body, float side, Material shirt, Material skin)
        {
            Transform arm = Pivot(name, body, new Vector3(side * 0.26f, 0.83f, 0));
            Part("Sleeve", PrimitiveType.Capsule, arm, new Vector3(0, -0.11f, 0), new Vector3(0.16f, 0.14f, 0.17f), shirt);
            Part("Hand", PrimitiveType.Sphere, arm, new Vector3(0, -0.31f, 0), Vector3.one * 0.14f, skin);
            return arm;
        }

        private static Transform Leg(string name, Transform body, float side, Material pants, Material boot)
        {
            Transform leg = Pivot(name, body, new Vector3(side * 0.105f, 0.43f, 0));
            Part("Trouser Leg", PrimitiveType.Capsule, leg, new Vector3(0, -0.17f, 0), new Vector3(0.17f, 0.18f, 0.19f), pants);
            Part("Boot", PrimitiveType.Cube, leg, new Vector3(0, -0.37f, 0.035f), new Vector3(0.19f, 0.12f, 0.27f), boot);
            return leg;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            transform.localPosition = position;
            return transform;
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

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = "Assets/Art/Materials/Crew/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(unlit ? "Unlit/Color" : "Standard")) { name = name, color = color };
            if (!unlit) material.SetFloat("_Glossiness", 0.05f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void UpdatePanel()
        {
            const string path = "Assets/Art/Prefabs/UI/UICommon_Build.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                UICommon_Build view = root.GetComponent<UICommon_Build>();
                if (view.CrewText == null)
                {
                    foreach (Text label in root.GetComponentsInChildren<Text>(true))
                        if (label.text == "蓝图等待船员施工") { view.CrewText = label; break; }
                }
                if (view.CrewText == null) throw new InvalidOperationException("Crew status label not found in build panel.");
                view.CrewText.text = "船员：待命";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
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
