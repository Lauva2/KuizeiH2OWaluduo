using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Building;
using RaftGame.Ocean;

namespace RaftGame.Editor
{
    /// <summary>Bakes evaluated Blender geometry into shared native meshes, retaining gameplay prefab identities.</summary>
    public static class BlenderModelSceneBuilder
    {
        [Serializable] private sealed class Kit { public string generator, units; public Model[] assets; }
        [Serializable] private sealed class Model { public string id; public int triangles; public Node[] nodes; }
        [Serializable] private sealed class Node
        {
            public string name; public Vector3 position; public Vector3[] vertices, normals;
            public Color[] colors; public Slot[] submeshes;
        }
        [Serializable] private sealed class Slot { public string material; public int[] triangles; }
        private const string Manifest = "SourceArt/Blender/raft_models.json";
        private const string Environment = "Assets/Art/Prefabs/Environment/";
        private static readonly string[] DeckSlots = { "HoneyWood", "LightWood", "DarkWood", "Rope", "IronNails" };
        public static bool IsAvailable => File.Exists(Manifest);

        [MenuItem("Tools/Raft Game/Apply Blender Models")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            ApplyToOpenScene(); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("BLENDER_MODELS_SCENE_OK");
            if (Application.isBatchMode) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }

        public static void ApplyToOpenScene()
        {
            if (!IsAvailable) throw new FileNotFoundException("Generate the Blender model kit first", Manifest);
            var kit = JsonUtility.FromJson<Kit>(File.ReadAllText(Manifest));
            if (kit == null || kit.units != "metres" || kit.assets == null || kit.assets.Length != 7)
                throw new InvalidDataException("Expected seven Blender models in metres");
            var deck = kit.assets.Single(item => item.id == "DeckTile").nodes.Single();
            var oldDeck = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Deck_Tile.asset");
            Debug.Log("BLENDER_BEFORE deckTriangles=" + oldDeck.triangles.Length / 3
                + " lanternRenderers=" + AssetDatabase.LoadAssetAtPath<GameObject>(Environment + "RaftLantern.prefab").GetComponentsInChildren<MeshRenderer>().Length);
            // Existing deck and ghost renderers already use this fixed five-material order.
            Bake(deck, "Assets/Art/Meshes/Deck_Tile.asset", false, DeckSlots);
            var config = AssetDatabase.LoadAssetAtPath<OceanLifeConfig>("Assets/Configs/OceanLife_Default.asset");
            config.FishPrefab = Fish(kit.assets.Single(item => item.id == "FishSilver"), "OceanFish");
            config.CoralFishPrefab = Fish(kit.assets.Single(item => item.id == "FishCoral"), "OceanFish_Coral");
            config.BlueFishPrefab = Fish(kit.assets.Single(item => item.id == "FishBlue"), "OceanFish_Blue");
            EditorUtility.SetDirty(config);
            var lantern = ReplaceVisuals(kit.assets.Single(item => item.id == "Lantern"), Environment + "RaftLantern.prefab");
            ReplaceVisuals(kit.assets.Single(item => item.id == "WaterBottle"), "Assets/Art/Prefabs/Salvage/Drift_WaterBottle.prefab");
            ReplaceVisuals(kit.assets.Single(item => item.id == "SupplyCrate"), "Assets/Art/Prefabs/Salvage/Drift_SupplyCrate.prefab");
            ValidateModels(kit);
            var build = UnityEngine.Object.FindObjectOfType<BuildManager>();
            if (build == null) return;
            var raft = (Transform)new SerializedObject(build).FindProperty("raftRoot").objectReferenceValue;
            var previous = raft.Find("Night Lantern");
            Vector3 position = previous != null ? previous.localPosition : new Vector3(-2.65f, .48f, -2.65f);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(lantern, build.gameObject.scene);
            instance.name = "Night Lantern"; instance.transform.SetParent(raft, false); instance.transform.localPosition = position;
            var day = UnityEngine.Object.FindObjectOfType<DayNightManager>();
            if (day != null)
            {
                Assign(day, "lantern", instance.GetComponentInChildren<Light>(true));
                Assign(day, "lanternGlass", instance.transform.Find("Lantern Glass").GetComponent<Renderer>());
                Assign(day, "lanternCore", instance.transform.Find("Warm Lamp Core").GetComponent<Renderer>());
            }
            Debug.Log("BLENDER_MESHES_OK generator=" + kit.generator + " triangles=" + kit.assets.Sum(item => item.triangles));
        }

        private static GameObject Fish(Model model, string filename)
        {
            var root = new GameObject(filename, typeof(OceanFishView));
            try
            {
                var parts = new List<Renderer>();
                foreach (var node in model.nodes)
                {
                    bool tail = node.name.StartsWith("Tail", StringComparison.Ordinal);
                    var part = Part(root.transform, model, node, true); part.name = tail ? "Tail" : "Fish Body";
                    parts.Add(part.GetComponent<Renderer>());
                    if (tail) Assign(root.GetComponent<OceanFishView>(), "tail", part.transform);
                }
                var serialized = new SerializedObject(root.GetComponent<OceanFishView>());
                var property = serialized.FindProperty("parts"); property.arraySize = parts.Count;
                for (int index = 0; index < parts.Count; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = parts[index];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, Environment + filename + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject ReplaceVisuals(Model model, string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Keep the original light, pickup collider, selection ring and component bindings.
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    UnityEngine.Object.DestroyImmediate(filter.gameObject);
                foreach (var node in model.nodes) Part(root.transform, model, node, false);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject Part(Transform parent, Model model, Node node, bool fish)
        {
            string path = "Assets/Art/Meshes/Blender_" + model.id + "_" + node.name.Replace(' ', '_').Replace('.', '_') + ".asset";
            var mesh = Bake(node, path, fish);
            var part = new GameObject(node.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false); part.transform.localPosition = node.position;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = fish ? new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/OceanFish.mat") }
                : node.submeshes.Select(slot => Material(slot.material)).ToArray();
            if (fish) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
            return part;
        }

        private static Mesh Bake(Node node, string path, bool merge, string[] order = null)
        {
            if (node.vertices == null || node.normals.Length != node.vertices.Length || node.colors.Length != node.vertices.Length)
                throw new InvalidDataException("Incomplete Blender vertex attributes: " + node.name);
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path), indexFormat = node.vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = node.vertices; mesh.normals = node.normals; mesh.colors = node.colors;
            if (merge) { mesh.subMeshCount = 1; mesh.SetTriangles(node.submeshes.SelectMany(slot => slot.triangles).ToArray(), 0); }
            else
            {
                var slots = order == null ? node.submeshes : order.Select(name => node.submeshes.Single(slot => slot.material == name)).ToArray();
                mesh.subMeshCount = slots.Length;
                for (int index = 0; index < slots.Length; index++) mesh.SetTriangles(slots[index].triangles, index);
            }
            mesh.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); return existing;
        }

        private static Material Material(string name)
        {
            string path = DeckSlots.Contains(name) ? "Assets/Art/Materials/Raft/" + name + ".mat"
                : name.StartsWith("Lantern", StringComparison.Ordinal) ? "Assets/Art/Materials/" + name + ".mat"
                : "Assets/Art/Materials/Salvage/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            if (name != "BottleInk") throw new InvalidDataException("Missing Blender material: " + path);
            material = new Material(Shader.Find("Standard")) { name = name, color = new Color(.07f, .30f, .35f) };
            material.SetFloat("_Glossiness", .18f); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void ValidateModels(Kit kit)
        {
            var report = new System.Text.StringBuilder("Blender model integration: PASS\n" + kit.generator + "; metres; native shared meshes baked from evaluated Blender geometry.\n");
            var deck = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Deck_Tile.asset");
            if (deck.subMeshCount != 5 || deck.bounds.size.x > 2.01f || deck.bounds.size.z > 2.01f || deck.bounds.max.y > .11f)
                throw new InvalidDataException("Deck no longer fits its existing two-metre construction grid");
            foreach (var model in kit.assets)
            {
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Blender/" + model.id + ".fbx");
                if (imported == null || imported.GetComponentsInChildren<MeshFilter>().Length == 0)
                    throw new InvalidDataException("FBX did not import: " + model.id);
                report.AppendLine(model.id + ": " + model.triangles + " triangles; " + model.nodes.Length + " shared mesh nodes; FBX imported.");
            }
            foreach (string filename in new[] { "OceanFish", "OceanFish_Coral", "OceanFish_Blue" })
            {
                var fish = AssetDatabase.LoadAssetAtPath<GameObject>(Environment + filename + ".prefab");
                var tail = fish.GetComponent<OceanFishView>().Tail;
                if (tail == null || tail.localPosition.z > -.3f || tail.GetComponent<MeshFilter>().sharedMesh.bounds.max.z > .1f
                    || fish.GetComponentsInChildren<Renderer>().Length != 2 || fish.GetComponentsInChildren<Collider>().Length != 0)
                    throw new InvalidDataException("Fish tail pivot, shared renderers or click-through shape is invalid: " + filename);
            }
            foreach (string filename in new[] { "Drift_WaterBottle", "Drift_SupplyCrate" })
            {
                var supply = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Salvage/" + filename + ".prefab");
                var marker = new SerializedObject(supply.GetComponent<RaftGame.Salvage.FloatingResource>()).FindProperty("marker").objectReferenceValue;
                if (marker == null || supply.GetComponent<BoxCollider>() == null || supply.GetComponentsInChildren<MeshRenderer>().Length != 1)
                    throw new InvalidDataException("Supply lost its pickup collider or marker: " + filename);
            }
            var lamp = AssetDatabase.LoadAssetAtPath<GameObject>(Environment + "RaftLantern.prefab");
            if (lamp.GetComponentsInChildren<MeshRenderer>().Length != 3 || lamp.GetComponentInChildren<Light>(true) == null)
                throw new InvalidDataException("Lantern lost its light or separate glowing meshes");
            report.AppendLine("PASS: two-metre deck bounds / five existing material slots; three separate tail pivots; two renderers per fish; three per lantern; one per supply; original pickup colliders and markers preserved.");
            Directory.CreateDirectory("Docs/Previews"); File.WriteAllText("Docs/Previews/BlenderModelValidation.txt", report.ToString());
        }
        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
