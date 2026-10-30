using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.Needs;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class ComfortSceneBuilder
    {
        public const string PanelPath = "Assets/Art/Prefabs/UI/UIPop_Comfort.prefab";
        private static readonly Color Ink = new Color(.16f, .28f, .25f), Green = new Color(.18f, .49f, .40f);
        [MenuItem("Tools/Raft Game/Add Decorations and Comfort")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            const string path = "Assets/Configs/Comfort_Default.asset";
            var config = AssetDatabase.LoadAssetAtPath<ComfortConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<ComfortConfig>(); AssetDatabase.CreateAsset(config, path); }
            game.Comfort = config; Folder("Assets/Art/Materials/Decorations");
            var plant = Model(FacilityKind.Plant); game.Building.PlantPrefab = plant; game.Building.PlantBlueprintPrefab = Ghost(plant);
            var rug = Model(FacilityKind.Rug); game.Building.RugPrefab = rug; game.Building.RugBlueprintPrefab = Ghost(rug);
            var lamp = Model(FacilityKind.DecorLamp); game.Building.DecorLampPrefab = lamp; game.Building.DecorLampBlueprintPrefab = Ghost(lamp);
            var manager = Object.FindObjectOfType<ComfortManager>(); if (manager == null) manager = new GameObject("ComfortManager").AddComponent<ComfortManager>();
            var fields = new SerializedObject(Object.FindObjectOfType<GameManager>()); fields.FindProperty("comfortManager").objectReferenceValue = manager; fields.ApplyModifiedPropertiesWithoutUndo();
            ManagementHudBuilder.Rebuild(); CreatePanel();
            var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("UI");
            if (group == null) throw new System.InvalidOperationException("Existing UI Addressables group required.");
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PanelPath), group).address = UIPop_Comfort.Address;
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.Building); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save comfort scene."); Debug.Log("COMFORT_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }
        private static GameObject Model(FacilityKind kind)
        {
            var root = new GameObject(kind.ToString());
            try
            {
                var view = root.AddComponent<DecorationView>();
                var wood = Mat("WarmWood", new Color(.58f, .36f, .19f)); var cream = Mat("WovenCream", new Color(.92f, .83f, .62f));
                var teal = Mat("WovenSea", new Color(.31f, .56f, .54f)); var clay = Mat("Terracotta", new Color(.74f, .38f, .23f));
                if (kind == FacilityKind.Plant)
                {
                    var corner = new GameObject("Corner Plant").transform; corner.SetParent(root.transform, false); corner.localPosition = new Vector3(-.62f, 0, -.62f);
                    Part(corner, "Pot", PrimitiveType.Cylinder, new Vector3(0, .18f, 0), new Vector3(.43f, .15f, .43f), clay);
                    Part(corner, "Pot Lip", PrimitiveType.Cylinder, new Vector3(0, .32f, 0), new Vector3(.49f, .04f, .49f), clay);
                    Part(corner, "Soil", PrimitiveType.Cylinder, new Vector3(0, .365f, 0), new Vector3(.40f, .012f, .40f), Mat("Soil", new Color(.23f, .19f, .12f)));
                    Part(corner, "Stem", PrimitiveType.Cylinder, new Vector3(0, .60f, 0), new Vector3(.04f, .25f, .04f), Mat("LeafDark", new Color(.20f, .43f, .29f)));
                    for (int i = 0; i < 7; i++)
                    {
                        var leaf = new GameObject("Leaf", typeof(MeshFilter), typeof(MeshRenderer)); leaf.transform.SetParent(corner, false);
                        leaf.GetComponent<MeshFilter>().sharedMesh = Leaf(); leaf.GetComponent<Renderer>().sharedMaterial = Mat(i % 2 == 0 ? "LeafGreen" : "LeafLight", i % 2 == 0 ? new Color(.29f, .57f, .34f) : new Color(.52f, .68f, .32f));
                        leaf.transform.localPosition = new Vector3(0, .49f + i * .045f, 0); leaf.transform.localRotation = Quaternion.Euler(-15 + i % 3 * 10, i * 137, 0);
                    }
                }
                else if (kind == FacilityKind.Rug)
                {
                    Part(root.transform, "Woven Rug", PrimitiveType.Cube, new Vector3(0, .075f, 0), new Vector3(1.52f, .035f, 1.52f), clay);
                    foreach (float edge in new[] { -.65f, .65f })
                    {
                        Part(root.transform, "Border", PrimitiveType.Cube, new Vector3(edge, .098f, 0), new Vector3(.055f, .012f, 1.35f), cream);
                        Part(root.transform, "Border", PrimitiveType.Cube, new Vector3(0, .098f, edge), new Vector3(1.35f, .012f, .055f), cream);
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        var diamond = Part(root.transform, "Sea Diamond", PrimitiveType.Cube, new Vector3(0, .10f, -.42f + i * .21f), new Vector3(.18f, .015f, .18f), teal);
                        diamond.transform.localRotation = Quaternion.Euler(0, 45, 0);
                    }
                    for (int i = 0; i < 9; i++) foreach (float z in new[] { -.80f, .80f })
                        Part(root.transform, "Fringe", PrimitiveType.Cube, new Vector3(-.62f + i * .155f, .075f, z), new Vector3(.035f, .025f, .13f), cream);
                }
                else
                {
                    var corner = new GameObject("Corner Lamp").transform; corner.SetParent(root.transform, false); corner.localPosition = new Vector3(-.64f, 0, -.64f);
                    Part(corner, "Foot", PrimitiveType.Cylinder, new Vector3(0, .07f, 0), new Vector3(.43f, .06f, .43f), wood);
                    Part(corner, "Post", PrimitiveType.Cylinder, new Vector3(0, .57f, 0), new Vector3(.055f, .50f, .055f), wood);
                    Part(corner, "Shade", PrimitiveType.Cylinder, new Vector3(0, 1.13f, 0), new Vector3(.48f, .20f, .48f), cream);
                    foreach (float y in new[] { .92f, 1.34f }) Part(corner, "Shade Rim", PrimitiveType.Cylinder, new Vector3(0, y, 0), new Vector3(.50f, .025f, .50f), wood);
                    var glow = Mat("LampGlow", new Color(1, .82f, .4f)); glow.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(glow);
                    view.Glow = Part(corner, "Lamp Core", PrimitiveType.Sphere, new Vector3(0, .91f, 0), new Vector3(.18f, .19f, .18f), glow).GetComponent<Renderer>();
                    var light = new GameObject("Warm Light", typeof(Light)); light.transform.SetParent(corner, false); light.transform.localPosition = new Vector3(0, .97f, 0);
                    view.Lamp = light.GetComponent<Light>(); view.Lamp.type = LightType.Point; view.Lamp.color = new Color(1, .73f, .35f); view.Lamp.range = 4.5f; view.Lamp.intensity = 0; view.Lamp.enabled = false; view.Lamp.shadows = LightShadows.None;
                }
                foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/" + kind + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static Mesh Leaf()
        {
            const string path = "Assets/Art/Meshes/Decoration_Leaf.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null && existing.vertexCount == 10) return existing;
            var mesh = new Mesh { name = "Decoration Leaf" };
            var front = new[] { Vector3.zero, new Vector3(-.13f, .13f, .21f), new Vector3(0, .18f, .41f), new Vector3(.13f, .13f, .21f), new Vector3(0, .16f, .20f) };
            var vertices = new Vector3[10]; front.CopyTo(vertices, 0); front.CopyTo(vertices, 5); mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4, 9, 6, 5, 9, 7, 6, 9, 8, 7, 9, 5, 8 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); return existing;
        }
        private static GameObject Ghost(GameObject prefab)
        {
            var root = Object.Instantiate(prefab); root.name = prefab.name + "_Blueprint";
            try
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light.gameObject);
                Object.DestroyImmediate(root.GetComponent<DecorationView>());
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Raft/Blueprint.mat");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>()) { renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                var line = new GameObject("Footprint", typeof(LineRenderer)); line.transform.SetParent(root.transform, false); var outline = line.GetComponent<LineRenderer>();
                outline.useWorldSpace = false; outline.loop = true; outline.widthMultiplier = .035f; outline.positionCount = 4; outline.sharedMaterial = material;
                outline.SetPositions(new[] { new Vector3(-.92f, .13f, -.92f), new Vector3(-.92f, .13f, .92f), new Vector3(.92f, .13f, .92f), new Vector3(.92f, .13f, -.92f) });
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/" + root.name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape); part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale; part.GetComponent<Renderer>().sharedMaterial = material; return part;
        }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/Art/Materials/Decorations/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path); if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard")) { name = name, color = color }; mat.SetFloat("_Glossiness", .05f); AssetDatabase.CreateAsset(mat, path); return mat;
        }
        private static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        private static void CreatePanel()
        {
            var root = new GameObject(UIPop_Comfort.Address, typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                root.AddComponent<Image>().color = new Color(.03f, .14f, .15f, .55f);
                var view = root.AddComponent<UIPop_Comfort>(); view.cache = true; var fields = new SerializedObject(view); fields.FindProperty("layer").enumValueIndex = (int)UILayer.Pop; fields.ApplyModifiedPropertiesWithoutUndo();
                var window = Rect("Comfort Window", root.transform, Vector2.zero, new Vector2(800, 470)); window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f); view.Window = window;
                window.gameObject.AddComponent<RoundedPanel>().color = new Color(.98f, .96f, .89f);
                Label("营地舒适度", window, new Vector2(24, -18), new Vector2(650, 38), 27);
                var close = Rect("Close", window, new Vector2(736, -24), new Vector2(40, 34)); close.gameObject.AddComponent<RoundedPanel>().color = Green; view.CloseButton = close.gameObject.AddComponent<Button>();
                var cross = Label("×", close, Vector2.zero, new Vector2(40, 34), 23); cross.alignment = TextAnchor.MiddleCenter; cross.color = new Color(1, .98f, .92f);
                view.ScoreText = Label("", window, new Vector2(24, -65), new Vector2(752, 38), 23);
                view.HintText = Label("", window, new Vector2(24, -111), new Vector2(752, 42), 14);
                view.Rows = new ComfortRowView[3];
                for (int index = 0; index < 3; index++)
                {
                    var card = Rect("Contribution " + index, window, new Vector2(24, -162 - index * 84), new Vector2(752, 72)); card.gameObject.AddComponent<RoundedPanel>().color = new Color(.91f, .92f, .83f);
                    var icon = Rect("Icon", card, new Vector2(14, -9), new Vector2(56, 54)).gameObject.AddComponent<BlueprintIcon>(); icon.Tool = FacilityRules.ToolFor(ComfortManager.Kinds[index]); icon.raycastTarget = false;
                    var row = new ComfortRowView(); view.Rows[index] = row;
                    row.Name = Label("", card, new Vector2(90, -8), new Vector2(260, 28), 20);
                    row.Contribution = Label("", card, new Vector2(358, -9), new Vector2(376, 26), 15);
                    row.Next = Label("", card, new Vector2(90, -42), new Vector2(644, 23), 14);
                }
                Label("只统计已建成的装饰 · 舒适度最高 100 · 搬移保留原位贡献", window, new Vector2(24, -426), new Vector2(752, 24), 13);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int font)
        {
            var text = Rect(value, parent, position, size).gameObject.AddComponent<Text>(); text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = font; text.color = Ink; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
        }
    }
}
