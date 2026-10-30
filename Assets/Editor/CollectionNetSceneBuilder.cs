using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.Salvage;

namespace RaftGame.Editor
{
    public static class CollectionNetSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Collection Nets")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(FacilitySceneBuilder.PanelPath)) FacilitySceneBuilder.Install();
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            Folder("Assets/Art/Materials/CollectionNet");
            const string configPath = "Assets/Configs/CollectionNet_Default.asset";
            var config = AssetDatabase.LoadAssetAtPath<CollectionNetConfig>(configPath);
            if (config == null) { config = ScriptableObject.CreateInstance<CollectionNetConfig>(); AssetDatabase.CreateAsset(config, configPath); }
            game.CollectionNets = config;
            bool first = game.Building.CollectionNetPrefab == null;
            game.Building.CollectionNetPrefab = Model(game.Salvage);
            game.Building.CollectionNetBlueprintPrefab = Ghost(game.Building.CollectionNetPrefab);
            if (first) game.Building.CollectionNetCost = new RaftGame.Resources.ResourceCost(6, 2);
            var manager = Object.FindObjectOfType<CollectionNetManager>();
            if (manager == null) manager = new GameObject("CollectionNetManager").AddComponent<CollectionNetManager>();
            var fields = new SerializedObject(Object.FindObjectOfType<GameManager>());
            fields.FindProperty("collectionNetManager").objectReferenceValue = manager; fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.Building);
            ManagementHudBuilder.Rebuild(); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save collection net scene.");
            Debug.Log("COLLECTION_NET_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }

        private static GameObject Model(SalvageConfig supplies)
        {
            var root = new GameObject("CollectionNet");
            try
            {
                Material wood = Mat("NetWood", new Color(.66f, .43f, .22f)), rope = Mat("NetRope", new Color(.82f, .74f, .51f));
                foreach (float x in new[] { -.84f, .84f })
                {
                    Box(root.transform, "Anchor Post", new Vector3(x, .18f, -.72f), new Vector3(.13f, .55f, .13f), wood);
                    Beam(root.transform, new Vector3(x, .35f, -.72f), new Vector3(x, -.06f, -2.78f), .04f, rope);
                    Beam(root.transform, new Vector3(x, -.06f, -1.08f), new Vector3(x, -.06f, -2.78f), .075f, wood);
                }
                foreach (float z in new[] { -1.08f, -2.78f }) Box(root.transform, "Basket Rim", new Vector3(0, -.06f, z), new Vector3(1.76f, .075f, .075f), wood);
                Box(root.transform, "Deck Crossbeam", new Vector3(0, .20f, -.72f), new Vector3(1.82f, .12f, .12f), wood);
                for (int index = 0; index <= 6; index++)
                {
                    float x = -.72f + index * .24f;
                    Beam(root.transform, new Vector3(x, -.32f, -1.24f), new Vector3(x, -.32f, -2.62f), .026f, rope);
                    foreach (float z in new[] { -1.08f, -2.78f })
                        Beam(root.transform, new Vector3(x, -.06f, z), new Vector3(x, -.32f, z < -2 ? -2.62f : -1.24f), .024f, rope);
                }
                for (int index = 0; index <= 6; index++)
                {
                    float z = -1.24f - index * .23f;
                    Beam(root.transform, new Vector3(-.72f, -.32f, z), new Vector3(.72f, -.32f, z), .026f, rope);
                    foreach (float x in new[] { -.84f, .84f }) Beam(root.transform, new Vector3(x, -.06f, z), new Vector3(x > 0 ? .72f : -.72f, -.32f, z), .024f, rope);
                }
                Combine(root);
                var view = root.AddComponent<CollectionNetView>();
                view.Indicator = Box(root.transform, "Capacity Flag", new Vector3(.84f, .42f, -.72f), new Vector3(.25f, .20f, .035f), Mat("NetFlag", new Color(.23f, .70f, .52f))).GetComponent<Renderer>();
                view.Wood = Cargo(root.transform, supplies.WoodPrefab, "Wood", .28f);
                view.Scrap = Cargo(root.transform, supplies.ScrapPrefab, "Scrap", .36f);
                view.Bottles = Cargo(root.transform, supplies.BottlePrefab, "Bottle", .42f);
                view.Crates = Cargo(root.transform, supplies.SupplyCratePrefab, "Crate", .36f);
                view.Present(new NetSupply[0], false);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/CollectionNet.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject[] Cargo(Transform parent, GameObject prefab, string name, float scale)
        {
            var result = new GameObject[CollectionNetManager.Capacity];
            for (int index = 0; index < result.Length; index++)
            {
                var item = Object.Instantiate(prefab, parent); item.name = name + " Slot " + index;
                item.transform.localPosition = new Vector3(index % 2 == 0 ? -.39f : .39f, -.18f, index < 2 ? -1.55f : -2.28f);
                item.transform.localRotation = Quaternion.Euler(0, index * 28, 0); item.transform.localScale = Vector3.one * scale;
                item.GetComponent<FloatingResource>().ShowMarker(false);
                foreach (Collider collider in item.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                Object.DestroyImmediate(item.GetComponent<FloatingResource>()); item.SetActive(false); result[index] = item;
            }
            return result;
        }
        private static GameObject Ghost(GameObject prefab)
        {
            var root = Object.Instantiate(prefab); root.name = "CollectionNet_Blueprint";
            try
            {
                var view = root.GetComponent<CollectionNetView>();
                foreach (var array in new[] { view.Wood, view.Scrap, view.Bottles, view.Crates }) foreach (var cargo in array) Object.DestroyImmediate(cargo);
                Object.DestroyImmediate(view);
                var ghost = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Raft/Blueprint.mat");
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>()) { renderer.sharedMaterial = ghost; renderer.shadowCastingMode = ShadowCastingMode.Off; }
                Outline(root.transform, ghost, "Footprint", new Vector3[] { new Vector3(-.94f, .04f, -.94f), new Vector3(-.94f, .04f, .94f), new Vector3(.94f, .04f, .94f), new Vector3(.94f, .04f, -.94f) }, true);
                Outline(root.transform, ghost, "Work Cell", new Vector3[] { new Vector3(-.64f, .04f, 1.36f), new Vector3(-.64f, .04f, 2.64f), new Vector3(.64f, .04f, 2.64f), new Vector3(.64f, .04f, 1.36f) }, true);
                Outline(root.transform, ghost, "Approach Arrow", new Vector3[] { new Vector3(0, .05f, 1.1f), new Vector3(0, .05f, 2.15f), new Vector3(-.22f, .05f, 1.9f), new Vector3(0, .05f, 2.15f), new Vector3(.22f, .05f, 1.9f) }, false);
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Facilities/CollectionNet_Blueprint.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Combine(GameObject root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var originals = new List<GameObject>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Material material = filter.GetComponent<Renderer>().sharedMaterial;
                if (!groups.TryGetValue(material, out var list)) groups.Add(material, list = new List<CombineInstance>());
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = filter.transform.localToWorldMatrix }); originals.Add(filter.gameObject);
            }
            foreach (var group in groups)
            {
                var mesh = new Mesh { name = "CollectionNet_" + group.Key.name }; mesh.CombineMeshes(group.Value.ToArray());
                string path = "Assets/Art/Meshes/" + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) { AssetDatabase.CreateAsset(mesh, path); existing = mesh; }
                else { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); }
                var obj = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root.transform, false);
                obj.GetComponent<MeshFilter>().sharedMesh = existing; obj.GetComponent<Renderer>().sharedMaterial = group.Key;
            }
            foreach (var obj in originals) Object.DestroyImmediate(obj);
        }
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale; part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>()); return part;
        }
        private static void Beam(Transform parent, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var beam = Box(parent, "Rope", (a + b) * .5f, new Vector3(thickness, thickness, (b - a).magnitude), material);
            beam.transform.localRotation = Quaternion.LookRotation(b - a);
        }
        private static void Outline(Transform parent, Material material, string name, Vector3[] points, bool loop)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.sharedMaterial = material; line.useWorldSpace = false; line.loop = loop; line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = .045f; line.shadowCastingMode = ShadowCastingMode.Off;
        }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/Art/Materials/CollectionNet/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")) { name = name, color = color }; material.SetFloat("_Glossiness", .15f); AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        private static void Folder(string path)
        { if (!AssetDatabase.IsValidFolder(path)) { Folder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); } }
    }
}
