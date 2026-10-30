using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.Needs;

namespace RaftGame.Editor
{
    public static class RestSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Beds and Rest")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            const string path = "Assets/Configs/Rest_Default.asset";
            var config = AssetDatabase.LoadAssetAtPath<RestConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<RestConfig>(); AssetDatabase.CreateAsset(config, path); }
            game.Rest = config; game.Building.BedPrefab = Model(); game.Building.BedBlueprintPrefab = Ghost(game.Building.BedPrefab);
            var manager = Object.FindObjectOfType<RestManager>(); if (manager == null) manager = new GameObject("RestManager").AddComponent<RestManager>();
            var fields = new SerializedObject(Object.FindObjectOfType<GameManager>()); fields.FindProperty("restManager").objectReferenceValue = manager; fields.ApplyModifiedPropertiesWithoutUndo();
            ManagementHudBuilder.Rebuild(); CrewRecruitmentSceneBuilder.CreatePanel();
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.Building); EditorUtility.SetDirty(game.Needs); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save rest scene.");
            Debug.Log("REST_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }
        private static GameObject Model()
        {
            var root = new GameObject("Bed");
            try
            {
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Crew/HammerWood.mat");
                var cream = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Crew/Canvas.mat");
                var teal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Crew/SeaTeal.mat");
                var stripe = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Crew/Skin.mat");
                foreach (float x in new[] { -.53f, .53f }) foreach (float z in new[] { -.72f, .72f })
                    Box(root.transform, "Bed Leg", new Vector3(x, .20f, z), new Vector3(.13f, .40f, .13f), wood);
                Box(root.transform, "Wood Frame", new Vector3(0, .28f, 0), new Vector3(1.22f, .15f, 1.77f), wood);
                Box(root.transform, "Headboard", new Vector3(0, .54f, -.82f), new Vector3(1.22f, .43f, .10f), wood);
                Box(root.transform, "Mattress", new Vector3(0, .40f, 0), new Vector3(1.1f, .15f, 1.62f), cream);
                Box(root.transform, "Pillow", new Vector3(0, .51f, -.57f), new Vector3(.72f, .14f, .34f), cream);
                Box(root.transform, "Sea Blanket", new Vector3(0, .50f, .24f), new Vector3(1.12f, .09f, 1.10f), teal);
                foreach (float z in new[] { -.20f, .67f }) Box(root.transform, "Blanket Border", new Vector3(0, .55f, z), new Vector3(1.10f, .018f, .055f), cream);
                for (int index = 0; index < 5; index++) Box(root.transform, "Blanket Stitch", new Vector3(-.44f + index * .22f, .56f, .46f), new Vector3(.06f, .012f, .10f), stripe);
                var view = root.AddComponent<BedView>();
                view.SleepAnchor = new GameObject("Sleep Feet Anchor").transform; view.SleepAnchor.SetParent(root.transform, false); view.SleepAnchor.localPosition = new Vector3(0, .70f, .67f);
                var marker = new GameObject("Rest Zzz", typeof(TextMesh)); marker.transform.SetParent(root.transform, false); marker.transform.localPosition = new Vector3(0, 1.20f, -.60f);
                var text = marker.GetComponent<TextMesh>(); text.text = "Z z"; text.fontSize = 40; text.characterSize = .14f; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(1, .93f, .68f);
                text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); marker.GetComponent<Renderer>().sharedMaterial = text.font.material;
                view.RestMarker = marker; marker.SetActive(false);
                foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/Bed.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Ghost(GameObject prefab)
        {
            var root = Object.Instantiate(prefab); root.name = "Bed_Blueprint";
            try
            {
                var view = root.GetComponent<BedView>(); Object.DestroyImmediate(view.RestMarker); Object.DestroyImmediate(view.SleepAnchor.gameObject); Object.DestroyImmediate(view);
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Raft/Blueprint.mat");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>()) { renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                Line(root.transform, "Footprint", material, new[] { new Vector3(-.91f, .05f, -.91f), new Vector3(-.91f, .05f, .91f), new Vector3(.91f, .05f, .91f), new Vector3(.91f, .05f, -.91f) }, true);
                Line(root.transform, "Work Cell", material, new[] { new Vector3(-.64f, .05f, 1.36f), new Vector3(-.64f, .05f, 2.64f), new Vector3(.64f, .05f, 2.64f), new Vector3(.64f, .05f, 1.36f) }, true);
                Line(root.transform, "Bed Approach", material, new[] { new Vector3(0, .06f, 1), new Vector3(0, .06f, 2.1f), new Vector3(-.2f, .06f, 1.9f), new Vector3(0, .06f, 2.1f), new Vector3(.2f, .06f, 1.9f) }, false);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/Bed_Blueprint.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale; part.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static void Line(Transform parent, string name, Material material, Vector3[] points, bool loop)
        {
            var obj = new GameObject(name, typeof(LineRenderer)); obj.transform.SetParent(parent, false); var line = obj.GetComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = loop; line.widthMultiplier = .035f; line.positionCount = points.Length; line.SetPositions(points); line.sharedMaterial = material;
        }
    }
}
