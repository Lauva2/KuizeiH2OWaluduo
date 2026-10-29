using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Ocean;

namespace RaftGame.Editor
{
    public static class OceanLifeSceneBuilder
    {
        [MenuItem("Tools/Raft Game/Add Ocean Fish and Day Night")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset");
            config.DayNight = LoadOrCreate<DayNightConfig>("Assets/Configs/DayNight_Default.asset");
            config.OceanLife = LoadOrCreate<OceanLifeConfig>("Assets/Configs/OceanLife_Default.asset");
            config.OceanLife.FishPrefab = CreateFish();
            var build = Object.FindObjectOfType<BuildManager>();
            Transform raft = new SerializedObject(build).FindProperty("raftRoot").objectReferenceValue as Transform;
            var previous = raft.Find("Night Lantern"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(CreateLantern(), scene);
            lamp.name = "Night Lantern"; lamp.transform.SetParent(raft, false); lamp.transform.localPosition = new Vector3(-2.65f, .48f, -2.65f);
            var day = Object.FindObjectOfType<DayNightManager>();
            if (day == null) day = new GameObject("Day and Night").AddComponent<DayNightManager>();
            Light sun = RenderSettings.sun;
            if (sun == null) foreach (var candidate in Object.FindObjectsOfType<Light>()) if (candidate.type == LightType.Directional) { sun = candidate; break; }
            Assign(day, "mainLight", sun); Assign(day, "lantern", lamp.GetComponentInChildren<Light>(true));
            Assign(day, "lanternGlass", lamp.transform.Find("Lantern Glass").GetComponent<Renderer>());
            Assign(day, "lanternCore", lamp.transform.Find("Warm Lamp Core").GetComponent<Renderer>());
            Assign(Object.FindObjectOfType<GameManager>(), "dayNightManager", day);
            EditorUtility.SetDirty(config); EditorUtility.SetDirty(config.OceanLife);
            ManagementHudBuilder.Rebuild();
            if (BlenderModelSceneBuilder.IsAvailable) BlenderModelSceneBuilder.ApplyToOpenScene();
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("OCEAN_LIFE_SCENE_OK");
            if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        public static void InstallAndBuildContent() { Install(); RaftSceneBuilder.BuildWithContent(); }
        private static GameObject CreateFish()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/OceanFish.mat");
            if (material == null) { material = new Material(Shader.Find("RaftGame/Submerged Fish")); AssetDatabase.CreateAsset(material, "Assets/Art/Materials/OceanFish.mat"); }
            Mesh body = BodyMesh(), tail = TailMesh();
            SaveMesh(body, "Assets/Art/Meshes/Fish_Body.asset"); SaveMesh(tail, "Assets/Art/Meshes/Fish_Tail.asset");
            body = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Fish_Body.asset"); tail = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Fish_Tail.asset");
            var root = new GameObject("Ocean Fish", typeof(OceanFishView));
            var bodyObject = MeshPart("Fish Body", root.transform, body, material);
            var tailObject = MeshPart("Tail", root.transform, tail, material); tailObject.transform.localPosition = new Vector3(0, 0, -.46f);
            Assign(root.GetComponent<OceanFishView>(), "tail", tailObject.transform);
            var fields = new SerializedObject(root.GetComponent<OceanFishView>()); var parts = fields.FindProperty("parts"); parts.arraySize = 2;
            parts.GetArrayElementAtIndex(0).objectReferenceValue = bodyObject.GetComponent<Renderer>(); parts.GetArrayElementAtIndex(1).objectReferenceValue = tailObject.GetComponent<Renderer>(); fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Environment/OceanFish.prefab"); Object.DestroyImmediate(root); return prefab;
        }
        private static GameObject MeshPart(string name, Transform parent, Mesh mesh, Material material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return part;
        }
        private static Mesh BodyMesh()
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
            float[] lengths = { .63f, .43f, .1f, -.22f, -.48f }, widths = { .012f, .16f, .24f, .19f, .045f }, heights = { .02f, .1f, .14f, .11f, .05f };
            for (int ring = 0; ring < lengths.Length; ring++)
                for (int edge = 0; edge < 8; edge++)
                {
                    float angle = edge * Mathf.PI / 4;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * widths[ring], Mathf.Sin(angle) * heights[ring], lengths[ring]));
                    colors.Add(Color.white * Mathf.Lerp(.56f, 1, (Mathf.Sin(angle) + 1) * .5f));
                    Color c = colors[colors.Count - 1]; c.a = 1; colors[colors.Count - 1] = c;
                    if (ring == 0) continue;
                    int a = (ring - 1) * 8 + edge, b = (ring - 1) * 8 + (edge + 1) % 8, cIndex = ring * 8 + edge, d = ring * 8 + (edge + 1) % 8;
                    indices.AddRange(new[] { a, b, cIndex, b, d, cIndex });
                }
            // Side fins and a dorsal fin keep the silhouette recognisable from the overhead camera.
            Triangle(vertices, colors, indices, new Vector3(-.18f, 0, .08f), new Vector3(-.43f, -.035f, -.22f), new Vector3(-.16f, 0, -.16f), new Color(.70f, .83f, .86f, .82f));
            Triangle(vertices, colors, indices, new Vector3(.18f, 0, .08f), new Vector3(.43f, -.035f, -.22f), new Vector3(.16f, 0, -.16f), new Color(.70f, .83f, .86f, .82f));
            Triangle(vertices, colors, indices, new Vector3(0, .10f, .17f), new Vector3(0, .32f, -.13f), new Vector3(0, .09f, -.29f), new Color(.62f, .74f, .80f, .9f));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 eye = new Vector3(side * .13f, .072f, .40f);
                Triangle(vertices, colors, indices, eye + new Vector3(-.025f, .01f, -.025f), eye + new Vector3(.025f, .01f, -.025f), eye + new Vector3(0, .025f, .027f), new Color(.05f, .10f, .12f, 1));
            }
            return MakeMesh("Fish Body", vertices, colors, indices);
        }
        private static Mesh TailMesh()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            Triangle(v, c, t, new Vector3(0, 0, .02f), new Vector3(-.26f, 0, -.37f), new Vector3(0, 0, -.22f), new Color(.78f, .90f, .91f, .9f));
            Triangle(v, c, t, new Vector3(0, 0, .02f), new Vector3(0, 0, -.22f), new Vector3(.26f, 0, -.37f), new Color(.78f, .90f, .91f, .9f));
            return MakeMesh("Fish Tail", v, c, t);
        }
        private static void Triangle(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 d, Color tint)
        { int start = v.Count; v.AddRange(new[] { a, b, d }); c.AddRange(new[] { tint, tint, tint }); t.AddRange(new[] { start, start + 1, start + 2 }); }
        private static Mesh MakeMesh(string name, List<Vector3> v, List<Color> c, List<int> t)
        { var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh; }
        private static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); }
        }
        private static GameObject CreateLantern()
        {
            var root = new GameObject("Raft Lantern");
            var wood = Material("LanternWood", new Color(.43f, .26f, .14f));
            var metal = Material("LanternMetal", new Color(.20f, .31f, .30f));
            var glass = Material("LanternGlass", new Color(1, .78f, .42f)); glass.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(glass);
            const string glowPath = "Assets/Art/Materials/LanternCore.mat";
            var glowMaterial = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
            if (glowMaterial == null) { glowMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(1, .86f, .48f) }; AssetDatabase.CreateAsset(glowMaterial, glowPath); }
            Part("Wooden Post", root.transform, PrimitiveType.Cylinder, new Vector3(0, .57f, 0), new Vector3(.10f, .57f, .1f), wood);
            Part("Lantern Glass", root.transform, PrimitiveType.Cylinder, new Vector3(0, 1.24f, 0), new Vector3(.27f, .18f, .27f), glass);
            Part("Lantern Roof", root.transform, PrimitiveType.Cylinder, new Vector3(0, 1.45f, 0), new Vector3(.40f, .045f, .40f), metal);
            Part("Warm Lamp Core", root.transform, PrimitiveType.Sphere, new Vector3(0, 1.51f, 0), new Vector3(.14f, .10f, .14f), glowMaterial);
            Part("Lantern Base", root.transform, PrimitiveType.Cylinder, new Vector3(0, 1.04f, 0), new Vector3(.34f, .035f, .34f), metal);
            foreach (float x in new[] { -.13f, .13f }) foreach (float z in new[] { -.13f, .13f }) Part("Lantern Frame", root.transform, PrimitiveType.Cube, new Vector3(x, 1.24f, z), new Vector3(.025f, .38f, .025f), metal);
            var glow = new GameObject("Warm Lantern Light", typeof(Light)); glow.transform.SetParent(root.transform, false); glow.transform.localPosition = new Vector3(0, 1.35f, 0);
            var light = glow.GetComponent<Light>(); light.type = LightType.Point; light.color = new Color(1, .82f, .55f); light.range = 11; light.intensity = 0; light.enabled = false; light.renderMode = LightRenderMode.ForcePixel; light.shadows = LightShadows.None;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/Prefabs/Environment/RaftLantern.prefab"); Object.DestroyImmediate(root); return prefab;
        }
        private static void Part(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(part.GetComponent<Collider>());
        }
        private static Material Material(string name, Color color)
        {
            string path = "Assets/Art/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { color = color }; material.SetFloat("_Glossiness", .2f); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        { var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); } return asset; }
        private static void Assign(Object target, string property, Object value)
        { var fields = new SerializedObject(target); fields.FindProperty(property).objectReferenceValue = value; fields.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
