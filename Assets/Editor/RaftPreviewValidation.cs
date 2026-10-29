using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Ocean;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class RaftPreviewValidation
    {
        private const string Key = "RaftGame.ValidateRaft";
        private static bool running;

        public static void Validate()
        {
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            // Exercise actual built bundles, not the AssetDatabase shortcut.
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 2;
            AssetDatabase.SaveAssets();
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 100f);
            Install();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Install()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= WaitForStartup;
            EditorApplication.update += WaitForStartup;
        }

        private static void WaitForStartup()
        {
            if (!EditorApplication.isPlaying || running) return;
            UIManager ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address))
            {
                running = true;
                EditorApplication.update -= WaitForStartup;
                RunChecks();
            }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0f))
            {
                Debug.LogError("Raft test timed out waiting for AA-loaded UI.");
                Finish(1);
            }
        }

        private static async void RunChecks()
        {
            try
            {
                var build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
                var ocean = UnityEngine.Object.FindObjectOfType<OceanManager>();
                // Exercise manual build APIs deterministically; automatic crew work has separate coverage.
                UnityEngine.Object.FindObjectOfType<GameManager>().enabled = false;
                Require(UnityEngine.Object.FindObjectOfType<GameManager>().IsInitialized && build.IsInitialized, "Explicit system initialization");
                Require(build.CompletedCount == 9 && build.BlueprintCount == 0, "Nine authored starter tiles, no duplicates");
                Require(!build.CanPlace(Vector2Int.zero, out _) && !build.CanPlace(new Vector2Int(2, 2), out _), "Overlap and diagonal-only rejection");
                Require(!build.CanPlace(new Vector2Int(int.MinValue, int.MaxValue), out _), "Out-of-bounds rejection");
                Vector2Int negative = new Vector2Int(-4, 3);
                Require(build.WorldToCell(build.CellToWorld(negative)) == negative, "Negative coordinate round trip");

                Require(build.TryPlaceBlueprint(new Vector2Int(2, 0), out _) && build.TryPlaceBlueprint(new Vector2Int(3, 0), out _), "Continuous blueprint chains");
                Require(!build.TryPlaceBlueprint(new Vector2Int(2, 0), out _), "Duplicate blueprint rejection");
                Require(!build.TryCancelBlueprint(new Vector2Int(2, 0), out _), "Cannot disconnect dependent blueprints");
                Require(build.TryCancelBlueprint(new Vector2Int(3, 0), out _) && build.TryCancelBlueprint(new Vector2Int(2, 0), out _), "Cancel outer-to-inner");
                Require(!build.TryCancelBlueprint(Vector2Int.zero, out _), "Finished tiles are protected");
                build.TryPlaceBlueprint(new Vector2Int(2, 0), out _);
                build.TryPlaceBlueprint(new Vector2Int(3, 0), out _);
                Require(!build.TryBeginConstruction(new Vector2Int(3, 0)), "Construction needs a finished neighbor");
                Require(build.TryBeginConstruction(new Vector2Int(2, 0)) && build.TryCompleteConstruction(new Vector2Int(2, 0)), "Crew construction hand-off");
                Require(!build.TryCompleteConstruction(new Vector2Int(2, 0)) && build.CompletedCount == 10, "Completion is idempotent");
                Require(build.TryCancelBlueprint(new Vector2Int(3, 0), out _), "Cancel remaining planned tile");

                Time.timeScale = 0f;
                float pausedOcean = ocean.ElapsedTime;
                build.TryPlaceBlueprint(new Vector2Int(2, 1), out _);
                build.TryPlaceBlueprint(new Vector2Int(3, 1), out _);
                build.TryPlaceBlueprint(new Vector2Int(2, -1), out _);
                await NextEditorFrame();
                await NextEditorFrame();
                Require(build.BlueprintCount == 3 && Mathf.Approximately(pausedOcean, ocean.ElapsedTime), "Planning while simulation is paused");
                Time.timeScale = 1f;

                var view = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(view.Layer == UILayer.Common && view.transform.parent.name == "Common", "UIView selects its layer");
                var openA = ui.OpenAsync(UICommon_Build.Address);
                var openB = ui.OpenAsync(UICommon_Build.Address);
                Require(await openA == await openB && ui.InstanceCount == 1, "Repeated opens share a single panel");
                ui.Close(UICommon_Build.Address);
                Require(ui.GetView(UICommon_Build.Address) == view && !view.gameObject.activeSelf, "cache=true hides the instance");
                Require(await ui.OpenAsync(UICommon_Build.Address) == view, "Cached instance reused");
                for (int i = 0; i < 3; i++) { ui.Close(UICommon_Build.Address); await ui.OpenAsync(UICommon_Build.Address); }
                build.SetTool(BuildTool.None);
                view.BuildButton.onClick.Invoke();
                Require(build.Tool == BuildTool.DeckBlueprint, "Controller listeners do not multiply after reopening");
                Require(view.BlueprintText.text.Contains("3") && view.CompletedText.text.Contains("10"), "MVC data refresh");

                view.cache = false;
                ui.Close(UICommon_Build.Address);
                Require(ui.GetView(UICommon_Build.Address) == null && ui.InstanceCount == 0, "cache=false destroys and removes the panel");
                var replacement = await ui.OpenAsync(UICommon_Build.Address);
                Require(replacement != view && replacement.cache, "Destroyed panel is loaded afresh from AA");
                ui.DestroyPanel(UICommon_Build.Address);
                Task<UIView> staleLoad = ui.OpenAsync(UICommon_Build.Address);
                ui.DestroyPanel(UICommon_Build.Address);
                await staleLoad;
                Require(ui.InstanceCount == 0, "Destroy during AA loading never resurrects the panel");
                Task<UIView> closedLoad = ui.OpenAsync(UICommon_Build.Address);
                ui.Close(UICommon_Build.Address);
                await closedLoad;
                Require(!ui.IsOpen(UICommon_Build.Address), "Close during AA loading stays closed");
                view = (UICommon_Build)await ui.OpenAsync(UICommon_Build.Address);
                Require(view != null && view.gameObject.activeSelf, "Closed pending panel can reopen");
                build.SetTool(BuildTool.None);

                Directory.CreateDirectory("Docs/Previews");
                Capture("Docs/Previews/Raft_Planning.png", Camera.main, ui.GetComponent<Canvas>(), view);
                File.WriteAllText("Docs/Previews/RaftValidation.txt",
                    "PASS: real Play mode + built Addressables bundles; starter grid; occupied/diagonal/bounds rejection; negative coordinates; " +
                    "blueprint chains and connectivity-safe cancellation; construction lifecycle; paused planning; MVC refresh; " +
                    "four UI layers; single-instance opens; cache hide/reuse; destroy/reload; pending load close/destroy; listener cleanup; UI raycasts.\n");
                Debug.Log("RAFT_VALIDATION_OK");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static Task NextEditorFrame()
        {
            var completion = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () => completion.TrySetResult(true);
            return completion.Task;
        }

        internal static void Capture(string path, Camera camera, Canvas canvas, UICommon_Build view)
        {
            var target = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = target;
            // A dynamic CJK atlas may rebuild as counters/statuses change. Populate all current labels
            // before this offscreen camera render so static labels do not reference an old atlas layout.
            foreach (UnityEngine.UI.Text label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>())
                if (label.font != null) label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
            foreach (UnityEngine.UI.Text label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>()) label.SetVerticesDirty();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            // Batch mode has no overlay Game view render; check raycasts after the camera has built UI geometry.
            RectTransform buttonRect = view.BuildButton.GetComponent<RectTransform>();
            Vector2 pointer = RectTransformUtility.WorldToScreenPoint(camera, buttonRect.TransformPoint(buttonRect.rect.center));
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, hits);
            Require(hits.Count > 0, "UI buttons intercept pointer raycasts");
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(texture);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Raft validation failed: " + message);
        }

        private static void Finish(int exitCode)
        {
            Time.timeScale = 1f;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= WaitForStartup;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 0;
                AssetDatabase.SaveAssets();
                Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            };
            EditorApplication.ExitPlaymode();
        }
    }
}
