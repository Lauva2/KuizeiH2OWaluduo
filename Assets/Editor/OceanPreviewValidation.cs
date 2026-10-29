using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RaftGame.Core;
using RaftGame.Ocean;

namespace RaftGame.Editor
{
    /// <summary>Focused integration checks for the ocean, with real GPU renders saved outside Assets.</summary>
    public static class OceanPreviewValidation
    {
        private const string RuntimeCheckKey = "RaftGame.OceanRuntimeCheck";
        private static int pauseFrame;
        private static float pausedTime;

        public static void BuildAndValidate()
        {
            OceanSceneBuilder.Build();
            Validate();
        }

        public static void RefreshAndValidateRegions()
        {
            OceanSceneBuilder.RefreshMaterial();
            Validate();
        }

        // CLI entry point without -quit: survives the play-mode domain reload via SessionState.
        public static void CheckPlayMode()
        {
            EditorSceneManager.OpenScene(OceanSceneBuilder.ScenePath);
            SessionState.SetBool(RuntimeCheckKey, true);
            InstallRuntimeCheck();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void InstallRuntimeCheck()
        {
            if (!SessionState.GetBool(RuntimeCheckKey, false)) return;
            EditorApplication.update -= CheckRuntimeFrame;
            EditorApplication.update += CheckRuntimeFrame;
        }

        private static void CheckRuntimeFrame()
        {
            if (!EditorApplication.isPlaying || Time.frameCount < 10) return;
            try
            {
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                var ocean = UnityEngine.Object.FindObjectOfType<OceanManager>();
                Require(game != null && game.IsInitialized && ocean != null && ocean.IsInitialized,
                    "GameManager initializes both systems in Play mode");
                if (pauseFrame == 0)
                {
                    Require(ocean.ElapsedTime > 0, "GameManager drives ocean animation");
                    pauseFrame = Time.frameCount;
                    pausedTime = ocean.ElapsedTime;
                    Time.timeScale = 0f;
                    return;
                }
                if (Time.frameCount < pauseFrame + 5) return;
                Require(Mathf.Approximately(pausedTime, ocean.ElapsedTime), "Paused simulation freezes ocean time");
                Directory.CreateDirectory("Docs/Previews");
                File.WriteAllText("Docs/Previews/PlayModeValidation.txt",
                    "PASS: actual Play mode GameManager initialization, frame-driven wave updates, pause across 5 frames.\n");
                Debug.Log("OCEAN_PLAY_MODE_OK");
                FinishRuntimeCheck(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishRuntimeCheck(1);
            }
        }

        private static void FinishRuntimeCheck(int exitCode)
        {
            Time.timeScale = 1f;
            SessionState.SetBool(RuntimeCheckKey, false);
            EditorApplication.update -= CheckRuntimeFrame;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            };
            EditorApplication.ExitPlaymode();
        }

        [MenuItem("Tools/Raft Game/Validate Ocean Preview")]
        public static void Validate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(OceanSceneBuilder.ScenePath);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Default.asset");
            var cameraManager = UnityEngine.Object.FindObjectOfType<CameraManager>();
            var ocean = UnityEngine.Object.FindObjectOfType<OceanManager>();
            var camera = Camera.main;
            Require(config != null && camera != null && cameraManager != null && ocean != null, "Scene wiring");
            Require(cameraManager.Initialize(config), "Camera initialization");
            Require(ocean.Initialize(config.Ocean, cameraManager.Focus), "Ocean initialization");
            Require(camera.orthographic && (camera.depthTextureMode & DepthTextureMode.Depth) != 0, "Orthographic depth setup");

            for (int index = 0; index < 100; index++)
            {
                Vector3 point = new Vector3(index * 2.71f - 120f, 0, index * -1.37f);
                float height = OceanManager.SampleHeight(config.Ocean, point, index * 0.3f);
                Require(!float.IsNaN(height) && Mathf.Abs(height - config.Ocean.SeaLevel) <= config.Ocean.MaximumWaveHeight + 0.0001f,
                    "Finite height within wave envelope");
            }

            Directory.CreateDirectory("Docs/Previews");
            Color32[] initial = Render(camera, "Docs/Previews/Ocean.png");
            ValidateRegions(config.Ocean, ocean, cameraManager, camera, initial);
            ocean.Tick(8f);
            Color32[] animated = Render(camera, "Docs/Previews/Ocean_Animated.png");
            Require(Difference(initial, animated) > 0.8, "Animated water changes over time");
            float pausedHeight = ocean.GetSurfaceHeight(Vector3.one);
            ocean.Tick(0f);
            Require(Mathf.Approximately(pausedHeight, ocean.GetSurfaceHeight(Vector3.one)), "Zero timestep keeps water still");

            // Recenter far from origin and at the maximum camera zoom: the whole frame must remain water.
            cameraManager.Focus.position = new Vector3(450f, 0, -320f);
            camera.transform.position += cameraManager.Focus.position;
            camera.orthographicSize = config.MaximumCameraSize;
            ocean.Tick(0f);
            Render(camera, "Docs/Previews/Ocean_FarZoom.png");
            camera.transform.position -= cameraManager.Focus.position;
            cameraManager.Focus.position = Vector3.zero;
            camera.orthographicSize = config.CameraSize;
            ocean.Tick(0f);

            // Temporary opaque geometry verifies contact foam; it is never saved into the scene.
            GameObject contact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            contact.name = "Temporary Foam Validation";
            contact.transform.position = new Vector3(0, 0.15f, 0);
            contact.transform.localScale = new Vector3(5f, 1f, 4f);
            Material contactMaterial = new Material(Shader.Find("Standard"));
            contactMaterial.color = new Color(0.45f, 0.25f, 0.10f);
            contactMaterial.SetFloat("_Glossiness", 0f);
            contact.GetComponent<Renderer>().sharedMaterial = contactMaterial;
            Color32[] withFoam = Render(camera, "Docs/Previews/Ocean_ContactFoam.png");
            var properties = new MaterialPropertyBlock();
            var renderer = ocean.GetComponentInChildren<MeshRenderer>();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_ContactFoamStrength", 0f);
            renderer.SetPropertyBlock(properties);
            Color32[] withoutFoam = Render(camera, null);
            Require(Difference(withFoam, withoutFoam) > 0.01, "Orthographic contact foam reacts to opaque geometry");
            UnityEngine.Object.DestroyImmediate(contact);
            UnityEngine.Object.DestroyImmediate(contactMaterial);
            ocean.Shutdown();

            Shader shader = config.Ocean.Material.shader;
            Require(shader.isSupported && !ShaderUtil.ShaderHasError(shader), "GPU shader compilation");
            File.WriteAllText("Docs/Previews/Validation.txt",
                "PASS: scene references, camera/ocean initialization, orthographic depth, 100 height samples, animation, zero timestep, " +
                "far camera render, contact foam, shallow/deep regions, shader compilation.\n" +
                "Renderer: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType + "\n");
            // Restore authored edit-mode state after testing, without saving the validation changes.
            EditorSceneManager.OpenScene(OceanSceneBuilder.ScenePath);
            Debug.Log("OCEAN_VALIDATION_OK");
            if (Application.isBatchMode)
            {
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void ValidateRegions(OceanConfig config, OceanManager ocean, CameraManager rig,
            Camera camera, Color32[] initial)
        {
            Vector3 shallow = new Vector3(config.ShallowCenter.x, 0, config.ShallowCenter.y);
            Vector3 deep = shallow + Vector3.right * (config.ShallowRadii.x * 3f + config.DepthTransitionWidth);
            Require(ocean.GetRegion(shallow) == OceanRegion.Shallow && ocean.GetRegion(deep) == OceanRegion.Deep,
                "Distinct shallow and deep water types");
            Require(Mathf.Abs(ocean.GetWaterDepth(shallow) - config.ShallowDepth) < 0.001f &&
                Mathf.Abs(ocean.GetWaterDepth(deep) - config.DeepDepth) < 0.001f, "Shallow/deep depth endpoints");
            Require(Mathf.Approximately(ocean.GetDeepWaterBlend(shallow), ocean.GetDeepWaterBlend(shallow + Vector3.up * 100)),
                "Water regions are independent of wave height");

            float distance = config.ShallowRadii.x + config.DepthTransitionWidth + config.BoundaryVariation * 2f;
            float previous = 0f;
            bool foundTransition = false;
            for (int index = 0; index <= 1024; index++)
            {
                float blend = ocean.GetDeepWaterBlend(shallow + Vector3.right * (distance * index / 1024f));
                Require(blend >= 0f && blend <= 1f && Mathf.Abs(blend - previous) < 0.05f, "Continuous region transition");
                foundTransition |= blend > 0.05f && blend < 0.95f;
                previous = blend;
            }
            Require(foundTransition, "Intermediate depths exist between the two regions");

            // The rendered region must stay in place when the ocean mesh follows the camera focus.
            rig.Focus.position = new Vector3(65f, 0, -45f);
            ocean.Tick(0f);
            Require(Difference(initial, Render(camera, null)) < 0.3, "World-fixed colors survive mesh recentering");
            rig.Focus.position = shallow;
            camera.transform.position += shallow;
            ocean.Tick(0f);
            Color32[] shallowPixels = Render(camera, "Docs/Previews/Ocean_Shallow.png");
            rig.Focus.position = deep;
            camera.transform.position += deep - shallow;
            ocean.Tick(0f);
            Color32[] deepPixels = Render(camera, "Docs/Previews/Ocean_Deep.png");
            Vector3 shallowMean = MeanColor(shallowPixels);
            Vector3 deepMean = MeanColor(deepPixels);
            Require(shallowMean.y > deepMean.y + 0.12f &&
                deepMean.z / Mathf.Max(0.001f, deepMean.y) > shallowMean.z / Mathf.Max(0.001f, shallowMean.y) + 0.25f,
                "GPU renders shallow water brighter/greener and deep water bluer");
            camera.transform.position -= deep;
            rig.Focus.position = Vector3.zero;
            ocean.Tick(0f);
            File.WriteAllText("Docs/Previews/RegionsValidation.txt",
                "PASS: region types, shallow/deep depth endpoints, height independence, 1025 transition samples, " +
                "stable color after mesh recentering, GPU shallow/deep palette separation.\n");
        }

        private static Vector3 MeanColor(Color32[] pixels)
        {
            Vector3 total = Vector3.zero;
            foreach (Color32 pixel in pixels) total += new Vector3(pixel.r, pixel.g, pixel.b);
            return total / (pixels.Length * 255f);
        }

        private static Color32[] Render(Camera camera, string output)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                Color32[] pixels = texture.GetPixels32();
                int invalid = 0;
                for (int i = 0; i < pixels.Length; i++)
                    if ((pixels[i].r > 220 && pixels[i].b > 220 && pixels[i].g < 70) ||
                        (pixels[i].r < 3 && pixels[i].g < 3 && pixels[i].b < 3)) invalid++;
                Require(invalid < pixels.Length / 100, "No error-pink/black water pixels");
                if (output != null) File.WriteAllBytes(output, texture.EncodeToPNG());
                return pixels;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static double Difference(Color32[] first, Color32[] second)
        {
            double sum = 0;
            for (int i = 0; i < first.Length; i++)
                sum += Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b);
            return sum / (first.Length * 3);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Ocean validation failed: " + message);
        }
    }
}
