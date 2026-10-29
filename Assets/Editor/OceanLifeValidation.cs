using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RaftGame.Core;
using RaftGame.Ocean;
using RaftGame.Building;
using RaftGame.Resources;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class OceanLifeValidation
    {
        private const string Key = "RaftGame.ValidateOceanLife";
        private static int assertions;
        private static GameManager game;
        private static DayNightManager day;
        private static OceanLifeManager life;
        private static OceanManager ocean;
        private static UIManager ui;
        private static UICommon_Build view;
        private static BuildManager build;
        public static void Validate()
        {
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 2; AssetDatabase.SaveAssets();
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 100);
            Install(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Install()
        { if (!SessionState.GetBool(Key, false)) return; EditorApplication.update -= Wait; EditorApplication.update += Wait; }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying) return;
            ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address)) { EditorApplication.update -= Wait; Run(); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) { Debug.LogError("Ocean life validation startup timed out"); Finish(1); }
        }
        private static async void Run()
        {
            try
            {
                assertions = 0;
                game = UnityEngine.Object.FindObjectOfType<GameManager>(); day = game.DayNight; life = game.OceanLife;
                ocean = UnityEngine.Object.FindObjectOfType<OceanManager>(); build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                view = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && day != null && day.IsInitialized && life != null && life.IsInitialized, "Scene initializes both new systems");
                Require(life.Count == 16 && day.Day == 1 && day.Hour >= 8 && day.Hour < 9, "Four small schools and 08:00 starter day");
                Require(view.DayClockText != null && view.DayIcon != null && view.DayProgressFill != null, "Actual AA HUD contains clock, sun/moon illustration and daily progress");
                var config = AssetDatabase.LoadAssetAtPath<OceanLifeConfig>("Assets/Configs/OceanLife_Default.asset");
                Require(life.Initialize(config, ocean, day) && life.Count == 16, "Repeated initialization cannot multiply fish");
                var colors = new HashSet<Color>(); var speciesMeshes = new Dictionary<int, Mesh>();
                for (int index = 0; index < life.Count; index++)
                {
                    var fish = life.GetFish(index); var properties = new MaterialPropertyBlock(); fish.GetComponentInChildren<Renderer>().GetPropertyBlock(properties);
                    colors.Add(properties.GetColor("_FishColor"));
                    Require(fish.Tail != null && fish.GetComponentsInChildren<Collider>().Length == 0, "Fish has animated fork tail without blocking salvage clicks");
                    Mesh mesh = fish.GetComponentInChildren<MeshFilter>().sharedMesh;
                    int species = index / config.FishPerSchool % 3;
                    if (!speciesMeshes.ContainsKey(species)) speciesMeshes.Add(species, mesh);
                    Require(mesh == speciesMeshes[species], "Fish within each species share the Blender mesh");
                    Require(fish.transform.position.y < ocean.GetSurfaceHeight(fish.transform.position) - .2f, "Fish swims below the actual animated water surface");
                    foreach (var renderer in fish.GetComponentsInChildren<Renderer>()) Require(renderer.sharedMaterial.shader.isSupported && renderer.shadowCastingMode == ShadowCastingMode.Off, "Underwater material is supported and casts no surface shadow");
                }
                Require(colors.Count == 3, "Schools use silver, coral and blue fish palettes");
                Require(new HashSet<Mesh>(speciesMeshes.Values).Count == 3, "Three Blender fish species have distinct silhouettes");
                Vector3 moving = life.GetFish(0).transform.position; Quaternion tail = life.GetFish(0).Tail.localRotation;
                await RealWait(.25);
                Require((life.GetFish(0).transform.position - moving).sqrMagnitude > .00001f && life.GetFish(0).Tail.localRotation != tail, "Real GameManager updates swimming and tail motion");
                view.PauseButton.onClick.Invoke();
                moving = life.GetFish(0).transform.position; tail = life.GetFish(0).Tail.localRotation;
                Quaternion light = day.MainLight.transform.rotation; double elapsed = game.TimeControl.ElapsedSeconds; float seaTime = ocean.ElapsedTime;
                await RealWait(.25);
                Require(game.TimeControl.ElapsedSeconds == elapsed && ocean.ElapsedTime == seaTime && life.GetFish(0).transform.position == moving && life.GetFish(0).Tail.localRotation == tail && day.MainLight.transform.rotation == light, "Pause freezes clock, waves, fish, tails and moving light across real frames");
                view.SpeedFourButton.onClick.Invoke(); elapsed = game.TimeControl.ElapsedSeconds;
                await RealWait(.25);
                Require(game.TimeControl.ElapsedSeconds > elapsed + .4 && game.TimeControl.Speed == 4 && life.GetFish(0).transform.position != moving, "4x resumes and accelerates the simulation used by both systems");
                game.TimeControl.SetPaused(true); game.enabled = false;
                var inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>(); string stock = JsonUtility.ToJson(inventory.CaptureInventory()); string loot = JsonUtility.ToJson(game.Loot.Capture());
                Directory.CreateDirectory("Docs/Previews");
                SetHour(12); Require(day.Daylight > .99f && !day.Lantern.enabled && day.Phase == "白昼", "Noon uses daylight and switches off lantern");
                Color noonWater = ocean.LitShallowColor; float noonLight = day.MainLight.intensity;
                await FlushFrames(); Capture("OceanLife_Day", 1600, 900);
                Require(RenderedFishPixels() > 160, "Actual stencil-based water render shows fish pixels");
                TestDepthAbsorption(config);
                TestRaftOcclusion();
                SetHour(18); Require(day.Twilight > .5f && day.Daylight > 0 && day.Daylight < 1 && day.Phase == "黄昏", "Sunset gradually blends warm lighting and water palette");
                await FlushFrames(); Capture("OceanLife_Dusk", 1600, 900);
                SetHour(23); Require(day.IsNight && day.Daylight < .01f && day.Lantern.enabled && day.Lantern.intensity > 1 && day.MainLight.intensity >= .3f, "Night has cool moonlight and a warm, usable lantern");
                Require(day.MainLight.intensity < noonLight && ocean.LitShallowColor.g < noonWater.g && ocean.LitShallowColor.g > .15f, "Night water darkens without becoming black");
                await FlushFrames(); Capture("OceanLife_Night", 1600, 900); Capture("OceanLife_NightCompact", 1280, 800);
                Require(view.DayClockText.text.Contains("23:00") && view.DayPhaseText.text == "夜晚", "Clock text reflects night and day index");
                Require(RenderedFishPixels() > 100, "Fish remain visible in moonlit water");
                TestSave();
                SetHour(5.8f, 2); Require(day.Day == 2 && day.Phase == "清晨", "Next-day dawn appears after midnight");
                await FlushFrames(); Capture("OceanLife_Dawn", 1600, 900);
                SetHour(23.98f); game.TimeControl.Tick(2); day.Tick();
                Require(day.Day == 2 && day.Hour < .2f, "Midnight wraps the hour and increments the day");
                SetHour(5.9f, 2); float beforeDawn = day.Daylight; Color beforeLight = day.MainLight.color;
                SetHour(6, 2); Require(Mathf.Abs(day.Daylight - beforeDawn) < .15f && Vector4.Distance(day.MainLight.color, beforeLight) < .25f, "Dawn light interpolates continuously instead of snapping");
                Require(JsonUtility.ToJson(inventory.CaptureInventory()) == stock && JsonUtility.ToJson(game.Loot.Capture()) == loot, "Decorative fish and lighting do not grant stock or consume gameplay loot randomness");
                SetHour(10.5f); var camera = UnityEngine.Object.FindObjectOfType<CameraManager>();
                Vector3 center = life.GetFish(0).transform.position; center.y = 0;
                camera.RestorePose(center, 35, 5);
                await FlushFrames(); Capture("OceanLife_Fish", 1600, 900);
                await CaptureBlenderModels(camera, config);
                File.WriteAllText("Docs/Previews/OceanLifeValidation.txt", "PASS: " + assertions + " assertions; actual Play mode and built AA HUD; 16 low-poly fish in four schools / three Blender silhouettes and palettes; shared meshes per species; submerged positions; tail animation; no click-blocking colliders; real swimming, pause and 4x updates; fish render pixel differences by day/night; underwater stencil prevents showing through raft; 10-minute day derived from saved clock; noon, sunset, moonlight, lantern, dawn and midnight; smooth lighting; clock/sun-moon/progress UI; 1600x900 and 1280x800 captures; save restores hour/day/light/fish positions/tails immediately; version 1 and 2 save compatibility; no inventory or loot RNG mutation.\n");
                Debug.Log("OCEAN_LIFE_VALIDATION_OK assertions=" + assertions); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static async Task CaptureBlenderModels(CameraManager camera, OceanLifeConfig config)
        {
            // A separate inspection shot: stage the real runtime meshes without granting inventory or changing their prefabs.
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(false);
            var positions = new[] { new Vector3(6, -.5f, -1.3f), new Vector3(7.5f, -.5f, .2f), new Vector3(5, -.5f, 1.5f) };
            var colors = new[] { config.SilverFish, config.CoralFish, config.BlueFish };
            for (int species = 0; species < 3; species++)
            {
                var fish = life.GetFish(species * config.FishPerSchool); fish.gameObject.SetActive(true);
                fish.transform.SetPositionAndRotation(positions[species], Quaternion.Euler(0, 20, 0)); fish.transform.localScale = Vector3.one * 1.35f;
                fish.Present(ocean.ElapsedTime, species, colors[species], ocean, day.Daylight);
            }
            var bottle = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Salvage/Drift_WaterBottle.prefab"), new Vector3(8.8f, .05f, 1), Quaternion.identity);
            var crate = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Salvage/Drift_SupplyCrate.prefab"), new Vector3(8.6f, .05f, -1.1f), Quaternion.identity);
            camera.RestorePose(new Vector3(7, 0, .2f), 35, 4);
            await FlushFrames(); Capture("Blender_ModelsInWater", 1600, 900);
            UnityEngine.Object.DestroyImmediate(bottle); UnityEngine.Object.DestroyImmediate(crate);
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(true);
            life.Tick(); camera.RestorePose(Vector3.zero, 35, 4.8f);
            await FlushFrames(); Capture("Blender_RaftDetail", 1600, 900);
        }
        private static void SetHour(float hour, int date = 1)
        {
            double seconds = ((date - 1) + (hour - 8) / 24.0) * day.DayDuration;
            game.TimeControl.Restore(1, true, seconds); ocean.RestoreTime((float)seconds);
            day.Tick(); life.Tick(); build.SetTool(BuildTool.None);
        }
        private static void TestSave()
        {
            int date = day.Day; float hour = day.Hour, intensity = day.MainLight.intensity;
            var positions = new List<Vector3>(); var tails = new List<Quaternion>();
            for (int index = 0; index < life.Count; index++) { positions.Add(life.GetFish(index).transform.position); tails.Add(life.GetFish(index).Tail.localRotation); }
            Require(game.Saves.Save(), "Night state saves through existing atomic save system"); string json = File.ReadAllText(game.Saves.ManualPath);
            SetHour(12, 3); Require(game.Saves.Load(), "Version 2 save loads with new environmental systems");
            Require(day.Day == date && Mathf.Abs(day.Hour - hour) < .001f && Mathf.Abs(day.MainLight.intensity - intensity) < .0001f && day.Lantern.enabled, "Load immediately restores day, hour, moonlight and lantern");
            for (int index = 0; index < life.Count; index++) Require(life.GetFish(index).transform.position == positions[index] && life.GetFish(index).Tail.localRotation == tails[index], "Load reconstructs exact fish positions and tail phases without random reroll");
            var legacy = JsonUtility.FromJson<SaveData>(json); legacy.Version = 1;
            // Version one only contained wood and scrap; new bottles are not valid legacy input.
            legacy.Floating.Clear(); legacy.Floating.Add(new FloatingSave { Type = ResourceType.Wood, Position = new Vector3(8, 0, 4), Age = 1, Phase = .2f });
            legacy.Floating.Add(new FloatingSave { Type = ResourceType.Scrap, Position = new Vector3(-7, 0, 5), Age = 2, Phase = .4f });
            File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(legacy)); Require(game.Saves.Load() && day.Day == date && Mathf.Abs(day.Hour - hour) < .001f, "Version one elapsed clock derives the same day/night time");
            File.WriteAllText(game.Saves.ManualPath, json); Require(game.Saves.Load(), "Restore normal version-two fixture");
        }
        private static void TestRaftOcclusion()
        {
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(false);
            var first = life.GetFish(0); first.transform.SetPositionAndRotation(new Vector3(0, -.7f, 0), Quaternion.identity);
            Color32[] without = RenderWorld(); first.gameObject.SetActive(true); Color32[] hidden = RenderWorld();
            int hiddenPixels = DifferentPixels(without, hidden);
            first.transform.position = new Vector3(6, -.7f, 0); Color32[] exposed = RenderWorld();
            int exposedPixels = DifferentPixels(without, exposed);
            Debug.Log("FISH_OCCLUSION_PIXELS hidden=" + hiddenPixels + " exposed=" + exposedPixels);
            Directory.CreateDirectory("Logs"); SaveDiagnostic(without, "Logs/FishOcclusionWithout.png"); SaveDiagnostic(hidden, "Logs/FishOcclusionUnderDeck.png");
            Require(exposedPixels > 100 && hiddenPixels < exposedPixels * .25f, "Most fish pixels are occluded by the raft, with water still visible through plank gaps");
            // Cover the genuine gaps with an opaque test slab, then require complete occlusion.
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); slab.transform.position = new Vector3(0, .1f, 0); slab.transform.localScale = new Vector3(3, .2f, 3);
            first.transform.position = new Vector3(0, -.7f, 0);
            RectInt region = FishScreenBounds(first);
            first.gameObject.SetActive(false); Color32[] covered = RenderWorld();
            int renderNoise = DifferentPixelsIn(covered, RenderWorld(), region);
            first.gameObject.SetActive(true); Color32[] occluded = RenderWorld();
            int remaining = DifferentPixelsIn(covered, occluded, region);
            Debug.Log("FISH_OPAQUE_MASK remaining=" + remaining + " control=" + renderNoise + " region=" + region);
            Require(remaining <= Mathf.Max(4, renderNoise * 3), "Continuous opaque geometry masks the fish within its projected silhouette, allowing measured soft-shadow render noise");
            UnityEngine.Object.DestroyImmediate(slab);
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(true);
            life.Tick();
        }
        private static void TestDepthAbsorption(OceanLifeConfig config)
        {
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(false);
            Color32[] water = RenderWorld(); var first = life.GetFish(0); first.gameObject.SetActive(true);
            Vector3 point = new Vector3(6, 0, 0); float surface = ocean.GetSurfaceHeight(point);
            first.transform.position = new Vector3(6, surface - .3f, 0); first.Present(ocean.ElapsedTime, 0, config.SilverFish, ocean, day.Daylight);
            long shallow = Contrast(water, RenderWorld());
            first.transform.position = new Vector3(6, surface - 1.5f, 0); first.Present(ocean.ElapsedTime, 0, config.SilverFish, ocean, day.Daylight);
            long deep = Contrast(water, RenderWorld());
            Debug.Log("FISH_WATER_ABSORPTION shallow=" + shallow + " deep=" + deep);
            Require(shallow > 1000 && deep < shallow * .8, "Actual water absorption and depth fade reduce the silhouette contrast of deeper fish");
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(true);
            life.Tick();
        }
        private static long Contrast(Color32[] a, Color32[] b)
        { long total = 0; for (int i = 0; i < a.Length; i++) total += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b); return total; }
        private static void SaveDiagnostic(Color32[] colors, string path)
        { var pixels = new Texture2D(800, 450, TextureFormat.RGB24, false); pixels.SetPixels32(colors); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(pixels); }
        private static RectInt FishScreenBounds(OceanFishView fish)
        {
            Vector2 min = new Vector2(800, 450), max = Vector2.zero;
            foreach (var renderer in fish.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.bounds;
                foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y }) foreach (float z in new[] { bounds.min.z, bounds.max.z })
                { Vector3 point = Camera.main.WorldToViewportPoint(new Vector3(x, y, z)); var pixel = new Vector2(point.x * 800, point.y * 450); min = Vector2.Min(min, pixel); max = Vector2.Max(max, pixel); }
            }
            return new RectInt(Mathf.FloorToInt(min.x) - 2, Mathf.FloorToInt(min.y) - 2, Mathf.CeilToInt(max.x - min.x) + 4, Mathf.CeilToInt(max.y - min.y) + 4);
        }
        private static int DifferentPixelsIn(Color32[] a, Color32[] b, RectInt rect)
        {
            int count = 0;
            for (int y = Mathf.Max(0, rect.yMin); y < Mathf.Min(450, rect.yMax); y++) for (int x = Mathf.Max(0, rect.xMin); x < Mathf.Min(800, rect.xMax); x++)
            { int i = y * 800 + x; if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 12) count++; }
            return count;
        }
        private static int RenderedFishPixels()
        {
            Color32[] with = RenderWorld();
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(false);
            Color32[] without = RenderWorld();
            for (int index = 0; index < life.Count; index++) life.GetFish(index).gameObject.SetActive(true);
            return DifferentPixels(with, without);
        }
        private static int DifferentPixels(Color32[] a, Color32[] b)
        { int count = 0; for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 12) count++; return count; }
        private static Color32[] RenderWorld()
        {
            var camera = Camera.main; var target = new RenderTexture(800, 450, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(800, 450, TextureFormat.RGB24, false); var old = camera.targetTexture; var active = RenderTexture.active;
            var canvas = ui.GetComponent<Canvas>(); bool enabled = canvas.enabled;
            try { canvas.enabled = false; camera.targetTexture = target; camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 800, 450), 0, 0); pixels.Apply(); return pixels.GetPixels32(); }
            finally { canvas.enabled = enabled; camera.targetTexture = old; RenderTexture.active = active; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); }
        }
        private static void Capture(string name, int width, int height) => VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), view, "Docs/Previews/" + name + ".png", width, height);
        private static async Task RealWait(double seconds) { double until = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < until) await NextFrame(); }
        private static Task NextFrame() { var next = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => next.TrySetResult(true); return next.Task; }
        private static async Task FlushFrames() { int frame = Time.frameCount; while (Time.frameCount <= frame + 1) await NextFrame(); }
        private static void Require(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Wait; Time.timeScale = 1;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode && Application.isBatchMode) EditorApplication.Exit(code); }; EditorApplication.ExitPlaymode();
        }
    }
}
