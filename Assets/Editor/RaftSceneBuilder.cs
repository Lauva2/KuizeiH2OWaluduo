using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class RaftSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/RaftPreview.unity";
        private const string BuildConfigPath = "Assets/Configs/Build_Default.asset";
        private const string GameConfigPath = "Assets/Configs/Game_Raft.asset";
        private const string PanelPath = "Assets/Art/Prefabs/UI/UICommon_Build.prefab";
        private static readonly Color Ink = new Color(0.15f, 0.26f, 0.25f);
        private static readonly Color Paper = new Color(0.98f, 0.95f, 0.87f, 0.97f);

        [MenuItem("Tools/Raft Game/Create Raft Preview")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) { Debug.Log("Raft preview already exists: " + ScenePath); return; }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/Art/Prefabs/Raft");
            EnsureFolder("Assets/Art/Prefabs/UI");
            EnsureFolder("Assets/Art/Materials/Raft");
            EnsureFolder("Assets/Art/Meshes");
            EnsureFolder("Assets/Configs");
            var scene = EditorSceneManager.OpenScene(OceanSceneBuilder.ScenePath);
            BuildConfig build = LoadOrCreate<BuildConfig>(BuildConfigPath);
            Mesh tileMesh = CreateTileMesh();
            Material[] woods = WoodMaterials();
            build.DeckPrefab = CreateDeckPrefab(tileMesh, woods);
            build.BlueprintPrefab = CreateBlueprintPrefab(tileMesh);
            EditorUtility.SetDirty(build);

            GameConfig config = LoadOrCreate<GameConfig>(GameConfigPath);
            EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Default.asset"), config);
            config.name = "Game_Raft";
            config.Building = build;
            config.CameraSize = 7.8f;
            config.MinimumCameraSize = 4f;
            config.MaximumCameraSize = 26f;
            EditorUtility.SetDirty(config);

            Transform raft = new GameObject("Raft").transform;
            raft.position = Vector3.up * config.Ocean.SeaLevel;
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
            {
                GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(build.DeckPrefab, scene);
                tile.name = $"Deck [{x}, {z}]";
                tile.transform.SetParent(raft, false);
                tile.transform.localPosition = new Vector3(x * build.TileSize, build.DeckHeight, z * build.TileSize);
                tile.GetComponent<DeckTile>().Coordinate = new Vector2Int(x, z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(tile.GetComponent<DeckTile>());
            }
            var buildManager = new GameObject("BuildManager").AddComponent<BuildManager>();
            Assign(buildManager, "raftRoot", raft);
            GameManager game = UnityEngine.Object.FindObjectOfType<GameManager>();
            Assign(game, "configuration", config);
            Assign(game, "buildManager", buildManager);
            UIManager ui = CreateUIRoot();
            Assign(game, "uiManager", ui);
            Camera camera = Camera.main;
            camera.orthographicSize = config.CameraSize;
            camera.backgroundColor = config.Ocean.DeepColor;
            CreatePanel();
            SetupAddressables();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Unable to save raft scene.");
            Selection.activeGameObject = raft.gameObject;
            Debug.Log("RAFT_BUILD_OK");
            if (Application.isBatchMode)
            {
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void BuildWithContent()
        {
            Build();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            Debug.Log("RAFT_AA_CONTENT_OK");
        }

        private static Material[] WoodMaterials()
        {
            return new[] {
                MaterialAsset("HoneyWood", new Color(0.67f, 0.39f, 0.17f)),
                MaterialAsset("LightWood", new Color(0.79f, 0.51f, 0.25f)),
                MaterialAsset("DarkWood", new Color(0.46f, 0.25f, 0.10f)),
                MaterialAsset("Rope", new Color(0.83f, 0.69f, 0.43f)),
                MaterialAsset("IronNails", new Color(0.18f, 0.22f, 0.21f)) };
        }

        private static Material MaterialAsset(string name, Color color)
        {
            string path = "Assets/Art/Materials/Raft/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0.08f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh CreateTileMesh()
        {
            const string path = "Assets/Art/Meshes/Deck_Tile.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            Material[] materials = WoodMaterials();
            GameObject model = new GameObject("Tile Mesh Source");
            var transientMeshes = new List<Mesh>();
            Mesh plank = BeveledBox(new Vector3(0.308f, 0.15f, 1.94f), 0.025f);
            Mesh frame = BeveledBox(new Vector3(1.96f, 0.13f, 0.075f), 0.018f);
            Mesh torus = Torus(0.235f, 0.026f);
            transientMeshes.AddRange(new[] { plank, frame, torus });
            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * 0.326f;
                Part(model.transform, plank, materials[i % 2], new Vector3(x, (i % 3 - 1) * 0.003f, 0), Quaternion.identity);
                foreach (float z in new[] { -0.73f, 0.73f })
                {
                    GameObject nail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    nail.transform.SetParent(model.transform, false);
                    nail.transform.localPosition = new Vector3(x, 0.081f, z);
                    nail.transform.localScale = new Vector3(0.035f, 0.005f, 0.035f);
                    nail.GetComponent<Renderer>().sharedMaterial = materials[4];
                }
            }
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.62f : 0.62f;
                GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.transform.SetParent(model.transform, false);
                log.transform.localPosition = new Vector3(x, -0.27f, 0);
                log.transform.localRotation = Quaternion.Euler(90, 0, 0);
                log.transform.localScale = new Vector3(0.46f, 0.985f, 0.46f);
                log.GetComponent<Renderer>().sharedMaterial = materials[2];
                Part(model.transform, torus, materials[3], new Vector3(x, -0.27f, -0.65f), Quaternion.identity);
                Part(model.transform, torus, materials[3], new Vector3(x, -0.27f, 0.65f), Quaternion.identity);
            }
            Part(model.transform, frame, materials[2], new Vector3(0, -0.055f, -0.95f), Quaternion.identity);
            Part(model.transform, frame, materials[2], new Vector3(0, -0.055f, 0.95f), Quaternion.identity);

            var groups = new List<CombineInstance>();
            foreach (Material material in materials)
            {
                var pieces = new List<CombineInstance>();
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
                    if (filter.GetComponent<Renderer>().sharedMaterial == material)
                        pieces.Add(new CombineInstance { mesh = filter.sharedMesh, transform = filter.transform.localToWorldMatrix });
                var group = new Mesh();
                group.CombineMeshes(pieces.ToArray(), true, true);
                transientMeshes.Add(group);
                groups.Add(new CombineInstance { mesh = group, transform = Matrix4x4.identity });
            }
            var mesh = new Mesh { name = "Deck_Tile" };
            mesh.CombineMeshes(groups.ToArray(), false, false);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            UnityEngine.Object.DestroyImmediate(model);
            foreach (Mesh temporary in transientMeshes) UnityEngine.Object.DestroyImmediate(temporary);
            return mesh;
        }

        private static void Part(Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
        {
            GameObject part = new GameObject("Part", typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static GameObject CreateDeckPrefab(Mesh mesh, Material[] materials)
        {
            var tile = new GameObject("Deck_Tile", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(DeckTile));
            tile.GetComponent<MeshFilter>().sharedMesh = mesh;
            tile.GetComponent<MeshRenderer>().sharedMaterials = materials;
            tile.GetComponent<BoxCollider>().size = new Vector3(1.96f, 0.15f, 1.94f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tile, "Assets/Art/Prefabs/Raft/Deck_Tile.prefab");
            UnityEngine.Object.DestroyImmediate(tile);
            return prefab;
        }

        private static GameObject CreateBlueprintPrefab(Mesh mesh)
        {
            string path = "Assets/Art/Materials/Raft/Blueprint.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("RaftGame/Build Ghost")) { name = "Blueprint" };
                AssetDatabase.CreateAsset(material, path);
            }
            var ghost = new GameObject("Deck_Blueprint", typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer));
            ghost.GetComponent<MeshFilter>().sharedMesh = mesh;
            ghost.GetComponent<MeshRenderer>().sharedMaterials = new[] { material, material, material, material, material };
            ghost.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            LineRenderer line = ghost.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.045f;
            line.positionCount = 4;
            line.SetPositions(new[] { new Vector3(-0.97f, 0.12f, -0.97f), new Vector3(-0.97f, 0.12f, 0.97f),
                new Vector3(0.97f, 0.12f, 0.97f), new Vector3(0.97f, 0.12f, -0.97f) });
            line.shadowCastingMode = ShadowCastingMode.Off;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ghost, "Assets/Art/Prefabs/Raft/Deck_Blueprint.prefab");
            UnityEngine.Object.DestroyImmediate(ghost);
            return prefab;
        }

        private static UIManager CreateUIRoot()
        {
            GameObject root = new GameObject("UIManager", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIManager));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            var layers = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                layers[i] = Rect(((UILayer)i).ToString(), root.transform);
                Stretch(layers[i]);
            }
            RectTransform inactive = Rect("Inactive Panels", root.transform);
            inactive.gameObject.SetActive(false);
            var serialized = new SerializedObject(root.GetComponent<UIManager>());
            SerializedProperty property = serialized.FindProperty("layers");
            property.arraySize = 4;
            for (int i = 0; i < 4; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
            serialized.FindProperty("inactiveRoot").objectReferenceValue = inactive;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return root.GetComponent<UIManager>();
        }

        private static void CreatePanel()
        {
            RectTransform root = Rect(UICommon_Build.Address, null);
            Stretch(root);
            UICommon_Build view = root.gameObject.AddComponent<UICommon_Build>();
            view.cache = true;
            var serialized = new SerializedObject(view);
            serialized.FindProperty("layer").enumValueIndex = (int)UILayer.Common;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RectTransform title = Card("Camp Card", root, new Vector2(0, 1), new Vector2(28, -28), new Vector2(385, 95));
            Label("木筏营地", title, new Vector2(22, -15), new Vector2(340, 32), 27, Ink);
            Label("沿着海风，把小小的家慢慢扩建", title, new Vector2(22, -57), new Vector2(345, 23), 16, new Color(0.37f, 0.45f, 0.41f));
            RectTransform counts = Card("Deck Counts", root, new Vector2(1, 1), new Vector2(-28, -28), new Vector2(260, 95));
            view.CompletedText = Label("已建甲板  9", counts, new Vector2(22, -16), new Vector2(220, 27), 22, Ink);
            view.BlueprintText = Label("待施工  0", counts, new Vector2(22, -55), new Vector2(220, 23), 17, new Color(0.37f, 0.45f, 0.41f));
            RectTransform toolbar = Card("Construction Toolbar", root, new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(760, 154));
            view.ModeText = Label("自由规划", toolbar, new Vector2(24, -15), new Vector2(160, 26), 20, Ink);
            Label("蓝图等待船员施工", toolbar, new Vector2(500, -18), new Vector2(236, 23), 15, new Color(0.42f, 0.49f, 0.44f), TextAnchor.MiddleRight);
            view.BuildButton = Button("扩建甲板   B", toolbar, new Vector2(24, -52), new Vector2(345, 48));
            view.CancelButton = Button("撤销蓝图   X", toolbar, new Vector2(389, -52), new Vector2(345, 48));
            view.StatusText = Label("沿木筏边缘规划新的甲板", toolbar, new Vector2(24, -113), new Vector2(710, 25), 16, new Color(0.33f, 0.43f, 0.39f));
            RectTransform controls = Rect("Camera Instructions", root);
            controls.anchorMin = controls.anchorMax = new Vector2(0, 0);
            controls.pivot = Vector2.zero;
            controls.anchoredPosition = new Vector2(28, 18);
            controls.sizeDelta = new Vector2(365, 25);
            Text hint = controls.gameObject.AddComponent<Text>();
            SetText(hint, "WASD 移动  ·  滚轮缩放  ·  Q / E 旋转", 14, new Color(0.94f, 0.97f, 0.91f));
            hint.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0.15f, 0.2f, 0.65f);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PanelPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private static RectTransform Card(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.gameObject.AddComponent<Image>().color = Paper;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.03f, 0.15f, 0.14f, 0.22f);
            shadow.effectDistance = new Vector2(0, -4);
            return rect;
        }

        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(value, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = rect.gameObject.AddComponent<Text>();
            SetText(text, value, fontSize, color);
            text.alignment = alignment;
            return text;
        }

        private static void SetText(Text text, string value, int fontSize, Color color)
        {
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static Button Button(string label, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(label, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.34f, 0.32f);
            Button button = rect.gameObject.AddComponent<Button>();
            Text text = Label(label, rect, Vector2.zero, size, 20, new Color(1, 0.98f, 0.91f), TextAnchor.MiddleCenter);
            return button;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetupAddressables()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create("Assets/AddressableAssetsData", "AddressableAssetSettings", true, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
            }
            AddressableAssetGroup group = settings.FindGroup("UI") ?? settings.CreateGroup("UI", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PanelPath), group);
            entry.address = UICommon_Build.Address;
            settings.ActivePlayModeDataBuilderIndex = 0;
            EditorUtility.SetDirty(settings);
        }

        // Flat bevels make the planks readable without texture dependencies.
        private static Mesh BeveledBox(Vector3 size, float bevel)
        {
            Vector3 half = size * 0.5f;
            Vector3 inner = half - Vector3.one * bevel;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Action<Vector3[]> face = points =>
            {
                Vector3 normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
                Vector3 center = Vector3.zero;
                foreach (Vector3 point in points) center += point;
                if (Vector3.Dot(normal, center) < 0) Array.Reverse(points);
                int start = vertices.Count;
                vertices.AddRange(points);
                for (int i = 1; i < points.Length - 1; i++) triangles.AddRange(new[] { start, start + i, start + i + 1 });
            };
            for (int axis = 0; axis < 3; axis++)
            foreach (float sign in new[] { -1f, 1f })
            {
                int b = (axis + 1) % 3, c = (axis + 2) % 3;
                Vector3[] points = new Vector3[4];
                int[] signsB = { -1, 1, 1, -1 }, signsC = { -1, -1, 1, 1 };
                for (int i = 0; i < 4; i++) { points[i][axis] = half[axis] * sign; points[i][b] = inner[b] * signsB[i]; points[i][c] = inner[c] * signsC[i]; }
                face(points);
            }
            for (int axis = 0; axis < 3; axis++)
            foreach (float signB in new[] { -1f, 1f })
            foreach (float signC in new[] { -1f, 1f })
            {
                int b = (axis + 1) % 3, c = (axis + 2) % 3;
                var points = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    points[i][axis] = (i < 2 ? -1 : 1) * inner[axis];
                    points[i][b] = signB * (i == 0 || i == 3 ? half[b] : inner[b]);
                    points[i][c] = signC * (i == 0 || i == 3 ? inner[c] : half[c]);
                }
                face(points);
            }
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                face(new[] { Vector3.Scale(new Vector3(x, y, z), new Vector3(half.x, inner.y, inner.z)),
                    Vector3.Scale(new Vector3(x, y, z), new Vector3(inner.x, half.y, inner.z)),
                    Vector3.Scale(new Vector3(x, y, z), new Vector3(inner.x, inner.y, half.z)) });
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh Torus(float radius, float tube)
        {
            const int major = 16, minor = 5;
            var vertices = new Vector3[major * minor];
            var triangles = new List<int>();
            for (int a = 0; a < major; a++) for (int b = 0; b < minor; b++)
            {
                float u = a * Mathf.PI * 2f / major, v = b * Mathf.PI * 2f / minor;
                float r = radius + Mathf.Cos(v) * tube;
                vertices[a * minor + b] = new Vector3(Mathf.Cos(u) * r, Mathf.Sin(u) * r, Mathf.Sin(v) * tube);
                int p = a * minor + b, q = ((a + 1) % major) * minor + b;
                int next = (b + 1) % minor;
                triangles.AddRange(new[] { p, q, a * minor + next, q, ((a + 1) % major) * minor + next, a * minor + next });
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); return mesh;
        }

        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); }
            return value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
