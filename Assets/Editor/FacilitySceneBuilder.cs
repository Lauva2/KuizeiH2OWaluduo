using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class FacilitySceneBuilder
    {
        public const string PanelPath = "Assets/Art/Prefabs/UI/UICommon_Facility.prefab";
        private static readonly Color Ink = new Color(.16f, .28f, .25f);
        private static readonly Color Green = new Color(.18f, .49f, .40f);
        [MenuItem("Tools/Raft Game/Add Facility Management")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var manager = Object.FindObjectOfType<FacilityManager>();
            if (manager == null) manager = new GameObject("FacilityManager").AddComponent<FacilityManager>();
            var fields = new SerializedObject(Object.FindObjectOfType<GameManager>());
            fields.FindProperty("facilityManager").objectReferenceValue = manager; fields.ApplyModifiedPropertiesWithoutUndo();
            CreatePanel();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("UI");
            if (group == null) throw new System.InvalidOperationException("The existing UI Addressables group is required.");
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PanelPath), group);
            entry.address = UICommon_Facility.Address; EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save facility management scene.");
            Debug.Log("FACILITY_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }
        private static void CreatePanel()
        {
            var root = new GameObject(UICommon_Facility.Address, typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform; rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one; rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
                var view = root.AddComponent<UICommon_Facility>(); view.cache = true;
                var fields = new SerializedObject(view); fields.FindProperty("layer").enumValueIndex = (int)UILayer.Common; fields.ApplyModifiedPropertiesWithoutUndo();
                var card = Rect("Facility Card", root.transform, new Vector2(-24, 0), new Vector2(328, 400));
                view.Card = card;
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(1, .5f);
                card.gameObject.AddComponent<RoundedPanel>().color = new Color(.98f, .96f, .89f, .98f);
                var shadow = card.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.03f, .15f, .14f, .2f); shadow.effectDistance = new Vector2(0, -4);
                view.Icon = Rect("Facility Icon", card, new Vector2(20, -20), new Vector2(56, 48)).gameObject.AddComponent<BlueprintIcon>(); view.Icon.raycastTarget = false;
                view.TitleText = Label("简易烤架", card, new Vector2(88, -19), new Vector2(190, 32), 23);
                Label("营地设施", card, new Vector2(88, -54), new Vector2(180, 21), 13);
                view.CloseButton = Button("×", card, new Vector2(282, -20), new Vector2(28, 28), Green, 22);
                view.LocationText = Label("位置 0, 0 · 操作位 0, 1", card, new Vector2(20, -82), new Vector2(288, 24), 13);
                view.WorkText = Label("等待船员 / 补足储备时生产", card, new Vector2(20, -116), new Vector2(288, 42), 16);
                view.TargetText = Label("储备目标 4", card, new Vector2(20, -169), new Vector2(190, 30), 17);
                view.TargetMinusButton = Button("−", card, new Vector2(220, -169), new Vector2(36, 30), Green, 20);
                view.TargetPlusButton = Button("+", card, new Vector2(272, -169), new Vector2(36, 30), Green, 20);
                view.RefundText = Label("返还：木材 4 · 废料 2", card, new Vector2(20, -213), new Vector2(288, 48), 15);
                view.MessageText = Label("免费搬移，拆除全额返还建材", card, new Vector2(20, -268), new Vector2(288, 48), 15);
                view.ActionRoot = Rect("Actions", card, new Vector2(20, -334), new Vector2(288, 40)).gameObject;
                view.MoveButton = Button("免费搬移", view.ActionRoot.transform, Vector2.zero, new Vector2(136, 40), Green, 17);
                view.MoveText = view.MoveButton.GetComponentInChildren<Text>();
                view.DemolishButton = Button("拆除返料", view.ActionRoot.transform, new Vector2(152, 0), new Vector2(136, 40), new Color(.62f, .36f, .23f), 17);
                view.ConfirmRoot = Rect("Confirm Demolition", card, new Vector2(20, -334), new Vector2(288, 40)).gameObject;
                view.ConfirmButton = Button("确认拆除", view.ConfirmRoot.transform, Vector2.zero, new Vector2(136, 40), new Color(.62f, .36f, .23f), 17);
                view.BackButton = Button("保留设施", view.ConfirmRoot.transform, new Vector2(152, 0), new Vector2(136, 40), Green, 17);
                view.ConfirmRoot.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Text Label(string value, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(value, parent, position, size).gameObject.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.text = value;
            text.color = Ink; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
        }
        private static Button Button(string value, Transform parent, Vector2 position, Vector2 size, Color color, int fontSize)
        {
            var rect = Rect(value, parent, position, size); rect.gameObject.AddComponent<RoundedPanel>().color = color;
            var button = rect.gameObject.AddComponent<Button>();
            var text = Label(value, rect, Vector2.zero, size, fontSize); text.color = new Color(1, .98f, .92f); text.alignment = TextAnchor.MiddleCenter; return button;
        }
    }
}
