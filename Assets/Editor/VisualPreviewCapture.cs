using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Fishing;
using RaftGame.Needs;
using RaftGame.Ocean;
using RaftGame.Resources;
using RaftGame.UI;

namespace RaftGame.Editor
{
    /// <summary>Repeatable visual review captures from an actual Play-mode scene and its AA-loaded HUD.</summary>
    public static class VisualPreviewCapture
    {
        private const string Key = "RaftGame.VisualCapture";

        public static void PrepareAndValidate()
        {
            ManagementHudBuilder.Rebuild();
            OceanPreviewValidation.RefreshAndValidateRegions();
            RaftSceneBuilder.BuildWithContent();
        }

        public static void ValidateAndCapture()
        {
            OceanPreviewValidation.RefreshAndValidateRegions();
            CapturePlayMode();
        }

        [MenuItem("Tools/Raft Game/Capture Visual Review")]
        public static void CapturePlayMode()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 90);
            Install(); EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Install()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Wait; EditorApplication.update += Wait;
        }

        private static async void Wait()
        {
            if (!EditorApplication.isPlaying) return;
            var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui == null || !ui.IsOpen(UICommon_Build.Address))
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) Finish(1);
                return;
            }
            EditorApplication.update -= Wait;
            try
            {
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                var build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                var crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
                var inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                var fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
                var ocean = UnityEngine.Object.FindObjectOfType<OceanManager>();
                var needs = UnityEngine.Object.FindObjectOfType<NeedsManager>();
                game.enabled = false;
                fishing.SetAutomaticFishing(false);
                var coordinate = new Vector2Int(-1, 0);
                Require(build.TryPlaceGrillBlueprint(coordinate, 0, out _) && build.TryBeginGrill(coordinate) && build.TryCompleteGrill(coordinate), "Review grill construction");
                Require(inventory.Add(ResourceType.Fish, 2), "Review fish stock");
                fishing.SetAutomaticFishing(true);
                for (int step = 0; step < 600; step++)
                {
                    crew.Tick(1f / 30f);
                    if (crew.Activity == CrewActivity.Cooking && crew.WorkProgress >= 0.4f) break;
                }
                Require(crew.Activity == CrewActivity.Cooking, "Crew reaches cooking state");
                ocean.Tick(4.5f);
                // Flush Destroy() of the construction ghost before capturing the completed grill.
                int captureFrame = Time.frameCount;
                while (Time.frameCount <= captureFrame + 1)
                {
                    var nextFrame = new TaskCompletionSource<bool>();
                    EditorApplication.delayCall += () => nextFrame.TrySetResult(true);
                    await nextFrame.Task;
                }
                var view = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                var canvas = ui.GetComponent<Canvas>();
                Directory.CreateDirectory("Docs/Previews");
                Capture(Camera.main, canvas, view, "Docs/Previews/OceanHud_Cooking.png", 1600, 900);
                Require(view.SatietyFill != null && Mathf.Abs(view.SatietyFill.anchorMax.x - needs.Satiety / 100f) < 0.01f, "Satiety bar follows model");
                ui.Close(UICommon_Build.Address); ui.OpenAsync(UICommon_Build.Address).GetAwaiter().GetResult();
                int target = UnityEngine.Object.FindObjectOfType<RaftGame.Production.ProductionManager>().Target;
                view.TargetPlusButton.onClick.Invoke();
                Require(UnityEngine.Object.FindObjectOfType<RaftGame.Production.ProductionManager>().Target == target + 1, "Cached target control binds once");
                view.TargetMinusButton.onClick.Invoke();
                build.TryPlaceBlueprint(new Vector2Int(2, 1), out _); build.TryPlaceBlueprint(new Vector2Int(3, 1), out _);
                build.SetTool(BuildTool.DeckBlueprint);
                Capture(Camera.main, canvas, view, "Docs/Previews/OceanHud_Planning.png", 1600, 900);
                needs.SetSatiety(18);
                Capture(Camera.main, canvas, view, "Docs/Previews/OceanHud_Compact.png", 1280, 800);
                Require(view.SatietyFill.GetComponent<Image>().color.r > 0.7f, "Low satiety has warm warning color");
                File.WriteAllText("Docs/Previews/VisualValidation.txt", "PASS: real Play mode, AA-loaded HUD, cooking/planning/low-satiety captures, 1600x900 and 1280x800, no truncated labels, satiety binding, cached target controls.\n");
                Debug.Log("VISUAL_CAPTURE_OK"); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        internal static void Capture(Camera camera, Canvas canvas, UICommon_Build view, string path, int width, int height, Action beforeRender = null)
        {
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
                camera.targetTexture = target;
                foreach (Text text in canvas.GetComponentsInChildren<Text>()) text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                foreach (Text text in canvas.GetComponentsInChildren<Text>()) text.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                beforeRender?.Invoke();
                Canvas.ForceUpdateCanvases();
                foreach (Text text in canvas.GetComponentsInChildren<Text>())
                    Require(text.preferredHeight <= text.rectTransform.rect.height + 2, "Label fits vertically: " + text.name);
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                foreach (var icon in canvas.GetComponentsInChildren<ResourceIcon>())
                {
                    var corners = new Vector3[4]; icon.rectTransform.GetWorldCorners(corners);
                    Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                    Vector2 last = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                    int colored = 0;
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(first.x)); x < Mathf.Min(width, last.x); x++)
                        for (int y = Mathf.Max(0, Mathf.CeilToInt(first.y)); y < Mathf.Min(height, last.y); y++)
                        {
                            Color pixel = texture.GetPixel(x, y);
                            float high = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)), low = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                            if (high - low > .13f && high < .96f && high > .25f) colored++;
                        }
                    Require(colored >= 12, "Rendered resource illustration remains visible: " + icon.Resource + " in " + path);
                }
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = previous;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Wait;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                if (Application.isBatchMode) EditorApplication.Exit(code);
            };
            EditorApplication.ExitPlaymode();
        }
    }
}
