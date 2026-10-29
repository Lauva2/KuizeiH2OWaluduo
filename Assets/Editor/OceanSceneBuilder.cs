using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Core;
using RaftGame.Ocean;

namespace RaftGame.Editor
{
    /// <summary>Creates authored assets; no runtime scene-generation or automatic scene replacement.</summary>
    public static class OceanSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/OceanPreview.unity";
        private const string OceanConfigPath = "Assets/Configs/Ocean_Default.asset";
        private const string GameConfigPath = "Assets/Configs/Game_Default.asset";
        private const string MaterialPath = "Assets/Art/Materials/Ocean_Turquoise.mat";
        private const string MeshPath = "Assets/Art/Meshes/Ocean_Grid.asset";
        private const string PrefabPath = "Assets/Art/Prefabs/Environment/Ocean.prefab";

        [MenuItem("Tools/Raft Game/Create Ocean Preview")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log("Ocean preview already exists. Open " + ScenePath + " to preserve your changes.");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Configs");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Art/Materials");
            EnsureFolder("Assets/Art/Meshes");
            EnsureFolder("Assets/Art/Prefabs/Environment");

            Shader shader = Shader.Find("RaftGame/Stylized Ocean");
            if (shader == null) throw new InvalidOperationException("Stylized Ocean shader did not import.");

            OceanConfig oceanConfig = LoadOrCreate<OceanConfig>(OceanConfigPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Ocean_Turquoise" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            oceanConfig.Material = material;
            ApplyMaterial(oceanConfig);

            GameConfig gameConfig = LoadOrCreate<GameConfig>(GameConfigPath);
            gameConfig.Ocean = oceanConfig;
            EditorUtility.SetDirty(oceanConfig);
            EditorUtility.SetDirty(gameConfig);

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = CreateGrid(oceanConfig);
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.73f, 0.85f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.53f, 0.70f, 0.71f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.40f, 0.39f);
            RenderSettings.fog = false;

            GameObject environment = new GameObject("Environment");
            GameObject ocean = new GameObject("Ocean");
            GameObject surface = new GameObject("Water Surface", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(ocean.transform, false);
            surface.transform.localPosition = Vector3.up * oceanConfig.SeaLevel;
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            OceanManager oceanManager = ocean.AddComponent<OceanManager>();
            Assign(oceanManager, "surfaceRenderer", renderer);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ocean, PrefabPath);
            UnityEngine.Object.DestroyImmediate(ocean);
            ocean = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            ocean.transform.SetParent(environment.transform, false);
            oceanManager = ocean.GetComponent<OceanManager>();

            GameObject sunObject = new GameObject("Sun - Warm Daylight", typeof(Light));
            sunObject.transform.SetParent(environment.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(55f, 210f, 0f);
            Light sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            RenderSettings.sun = sun;

            GameObject cameraRig = new GameObject("Camera Rig");
            Transform focus = new GameObject("Camera Focus").transform;
            focus.SetParent(cameraRig.transform, false);
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraRig.transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = oceanConfig.DeepColor;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 220f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            CameraManager cameraManager = cameraRig.AddComponent<CameraManager>();
            Assign(cameraManager, "sceneCamera", camera);
            Assign(cameraManager, "focus", focus);
            // Author a usable edit-mode view as well as runtime initialization.
            camera.orthographic = true;
            camera.orthographicSize = gameConfig.CameraSize;
            camera.depthTextureMode = DepthTextureMode.Depth;
            Quaternion rotation = Quaternion.Euler(gameConfig.CameraTilt, gameConfig.CameraYaw, 0f);
            camera.transform.SetPositionAndRotation(rotation * Vector3.back * 85f, rotation);

            GameObject systems = new GameObject("GameManager");
            GameManager gameManager = systems.AddComponent<GameManager>();
            Assign(gameManager, "configuration", gameConfig);
            Assign(gameManager, "cameraManager", cameraManager);
            Assign(gameManager, "oceanManager", oceanManager);

            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Unable to save ocean scene.");
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(Vector3.zero, rotation, gameConfig.CameraSize * 1.6f, true);
            Selection.activeGameObject = ocean;
            Debug.Log("OCEAN_BUILD_OK: " + ScenePath);
        }

        [MenuItem("Tools/Raft Game/Apply Ocean Config To Preview Material")]
        public static void RefreshMaterial()
        {
            OceanConfig config = AssetDatabase.LoadAssetAtPath<OceanConfig>(OceanConfigPath);
            if (config == null) throw new InvalidOperationException("Create the ocean preview first.");
            ApplyMaterial(config);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }

        private static void ApplyMaterial(OceanConfig config)
        {
            Material m = config.Material;
            m.SetColor("_DeepColor", config.DeepColor);
            m.SetColor("_ShallowColor", config.ShallowColor);
            m.SetColor("_FoamColor", config.FoamColor);
            m.SetColor("_ReflectionColor", config.ReflectionColor);
            m.SetVector("_ShallowArea", new Vector4(config.ShallowCenter.x, config.ShallowCenter.y,
                config.ShallowRadii.x, config.ShallowRadii.y));
            m.SetVector("_DepthTransition", new Vector4(config.DepthTransitionWidth, config.BoundaryVariation, 0f, 0f));
            m.SetVector("_WaveA", config.WaveA);
            m.SetVector("_WaveB", config.WaveB);
            m.SetVector("_WaveC", config.WaveC);
            m.SetFloat("_WaveSpeed", config.WaveSpeed);
            m.SetFloat("_RippleStrength", config.RippleStrength);
            m.SetFloat("_RippleScale", config.RippleScale);
            m.SetFloat("_CrestFoam", config.CrestFoam);
            m.SetFloat("_ContactFoamWidth", config.ContactFoamWidth);
            m.SetFloat("_ContactFoamStrength", config.ContactFoamStrength);
            m.SetFloat("_SunGlint", config.SunGlint);
            m.SetFloat("_ReflectionStrength", config.ReflectionStrength);
            m.SetFloat("_OceanTime", -1f);
            EditorUtility.SetDirty(m);
        }

        private static Mesh CreateGrid(OceanConfig config)
        {
            int resolution = config.GridResolution;
            int row = resolution + 1;
            var vertices = new Vector3[row * row];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[resolution * resolution * 6];
            for (int z = 0; z < row; z++)
            for (int x = 0; x < row; x++)
            {
                int index = z * row + x;
                vertices[index] = new Vector3(((float)x / resolution - 0.5f) * config.SurfaceSize, 0,
                    ((float)z / resolution - 0.5f) * config.SurfaceSize);
                normals[index] = Vector3.up;
            }
            int cursor = 0;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int a = z * row + x;
                triangles[cursor++] = a;
                triangles[cursor++] = a + row;
                triangles[cursor++] = a + 1;
                triangles[cursor++] = a + 1;
                triangles[cursor++] = a + row;
                triangles[cursor++] = a + row + 1;
            }
            var mesh = new Mesh { name = "Ocean_Grid", vertices = vertices, normals = normals, triangles = triangles };
            // Include all allowed vertical wave displacements in CPU frustum culling.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(config.SurfaceSize, 8f, config.SurfaceSize));
            return mesh;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
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
