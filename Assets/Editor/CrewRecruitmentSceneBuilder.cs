using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Crew;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class CrewRecruitmentSceneBuilder
    {
        public const string PanelPath = "Assets/Art/Prefabs/UI/UIPop_Crew.prefab";
        private static readonly Color Ink = new Color(.16f, .28f, .25f);
        private static readonly Color Green = new Color(.18f, .49f, .40f);
        [MenuItem("Tools/Raft Game/Add Crew Recruitment")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            const string path = "Assets/Configs/Rescue_Default.asset";
            var rescue = AssetDatabase.LoadAssetAtPath<RescueConfig>(path);
            if (rescue == null) { rescue = ScriptableObject.CreateInstance<RescueConfig>(); AssetDatabase.CreateAsset(rescue, path); }
            rescue.RaftPrefab = CreateRaft(config); config.Rescue = rescue;
            var manager = Object.FindObjectOfType<RescueManager>();
            if (manager == null) manager = new GameObject("RescueManager").AddComponent<RescueManager>();
            var fields = new SerializedObject(Object.FindObjectOfType<GameManager>());
            fields.FindProperty("rescueManager").objectReferenceValue = manager; fields.ApplyModifiedPropertiesWithoutUndo();
            CreatePanel(); ManagementHudBuilder.Rebuild();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("UI");
            if (group == null) throw new System.InvalidOperationException("The existing UI Addressables group is required.");
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PanelPath), group).address = UIPop_Crew.Address;
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(config); EditorUtility.SetDirty(rescue);
            EditorUtility.SetDirty(config.Crew); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save recruitment scene.");
            Debug.Log("CREW_RECRUITMENT_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }
        private static GameObject CreateRaft(GameConfig config)
        {
            var root = new GameObject("RescueRaft");
            try
            {
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Crew/Boots.mat");
                if (wood == null) wood = config.Building.DeckPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
                var pale = config.Building.DeckPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
                for (int index = 0; index < 4; index++) Box(root.transform, "Float Plank", new Vector3(-.48f + index * .32f, .06f, 0), new Vector3(.29f, .18f, 1.55f), pale);
                foreach (float z in new[] { -.48f, .48f }) Box(root.transform, "Lashing", new Vector3(0, .17f, z), new Vector3(1.26f, .055f, .065f), wood);
                Box(root.transform, "Signal Pole", new Vector3(.49f, .65f, .47f), new Vector3(.045f, 1.1f, .045f), wood);
                var orange = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Crew/Skin.mat") ?? pale;
                Box(root.transform, "Signal Flag", new Vector3(.67f, 1.06f, .47f), new Vector3(.35f, .23f, .035f), orange);
                var person = Object.Instantiate(config.Crew.CrewPrefab, root.transform);
                person.name = "Waiting Sailor"; person.transform.localPosition = new Vector3(-.12f, .19f, -.10f);
                person.transform.localScale = Vector3.one * .72f;
                var view = root.AddComponent<RescueRaftView>(); view.Person = person.GetComponent<CrewView>();
                var marker = new GameObject("Rescue Marker", typeof(LineRenderer)); marker.transform.SetParent(root.transform, false);
                var line = marker.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 32;
                line.widthMultiplier = .045f; line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Raft/Blueprint.mat");
                for (int index = 0; index < 32; index++) { float a = index * Mathf.PI / 16; line.SetPosition(index, new Vector3(Mathf.Cos(a) * .94f, .20f, Mathf.Sin(a) * 1.02f)); }
                view.Marker = marker; marker.SetActive(false);
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Crew/RescueRaft.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false);
            box.transform.localPosition = position; box.transform.localScale = scale; box.GetComponent<Renderer>().sharedMaterial = material;
        }
        public static void CreatePanel()
        {
            var root = new GameObject(UIPop_Crew.Address, typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                root.AddComponent<Image>().color = new Color(.03f, .14f, .15f, .55f);
                var view = root.AddComponent<UIPop_Crew>(); view.cache = true;
                var fields = new SerializedObject(view); fields.FindProperty("layer").enumValueIndex = (int)UILayer.Pop; fields.ApplyModifiedPropertiesWithoutUndo();
                var window = Rect("Crew Window", root.transform, Vector2.zero, new Vector2(848, 468));
                window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f); view.Window = window;
                window.gameObject.AddComponent<RoundedPanel>().color = new Color(.98f, .96f, .89f);
                view.Heading = Label("船员分工 · 1 / 2", window, new Vector2(24, -18), new Vector2(700, 40), 27);
                view.CloseButton = Button("×", window, new Vector2(784, -24), new Vector2(40, 34), 23);
                view.Notice = Label("", window, new Vector2(24, -64), new Vector2(800, 48), 14);
                view.Rows = new CrewRowView[2];
                for (int index = 0; index < 2; index++)
                {
                    var card = Rect("Crew " + (index + 1), window, new Vector2(24, -124 - index * 156), new Vector2(800, 138));
                    card.gameObject.AddComponent<RoundedPanel>().color = new Color(.91f, .91f, .82f);
                    var row = new CrewRowView(); view.Rows[index] = row;
                    var stripe = Rect("Identity", card, new Vector2(0, -12), new Vector2(5, 114));
                    stripe.gameObject.AddComponent<Image>().color = index == 0 ? Green : new Color(.83f, .40f, .20f);
                    row.Name = Label("", card, new Vector2(18, -10), new Vector2(378, 30), 21);
                    row.Needs = Label("", card, new Vector2(18, -46), new Vector2(392, 28), 14);
                    row.Energy = Label("", card, new Vector2(18, -74), new Vector2(392, 22), 14);
                    row.Activity = Label("", card, new Vector2(18, -101), new Vector2(392, 28), 16);
                    Label("工作偏好", card, new Vector2(420, -10), new Vector2(360, 26), 14);
                    row.Automatic = Button("自动", card, new Vector2(420, -44), new Vector2(100, 38), 16);
                    row.Gathering = Button("采集优先", card, new Vector2(530, -44), new Vector2(112, 38), 16);
                    row.Building = Button("建造生产优先", card, new Vector2(652, -44), new Vector2(130, 38), 15);
                    row.Location = Label("", card, new Vector2(420, -96), new Vector2(362, 28), 13);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int font)
        {
            var text = Rect(value, parent, position, size).gameObject.AddComponent<Text>(); text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = font; text.color = Ink; text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft; return text;
        }
        private static Button Button(string value, Transform parent, Vector2 position, Vector2 size, int font)
        {
            var rect = Rect(value, parent, position, size); var image = rect.gameObject.AddComponent<RoundedPanel>(); image.color = Green;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label(value, rect, Vector2.zero, size, font); label.alignment = TextAnchor.MiddleCenter; label.color = new Color(1, .98f, .92f); return button;
        }
    }
}
