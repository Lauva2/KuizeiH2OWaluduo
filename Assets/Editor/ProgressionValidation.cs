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
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;
using RaftGame.Ocean;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class ProgressionValidation
    {
        private const string Key = "RaftGame.ValidateProgression";
        private static int assertions;
        private static BuildManager build;
        private static CrewManager crew;
        private static NeedsManager needs;
        private static WaterManager water;
        private static ResourceManager inventory;
        public static void Validate()
        {
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 2;
            AssetDatabase.SaveAssets(); SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 100);
            Install(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Install()
        { if (!SessionState.GetBool(Key, false)) return; EditorApplication.update -= Wait; EditorApplication.update += Wait; }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying) return;
            var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address)) { EditorApplication.update -= Wait; Run(ui); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) Finish(1);
        }
        private static async void Run(UIManager ui)
        {
            try
            {
                assertions = 0;
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                build = UnityEngine.Object.FindObjectOfType<BuildManager>(); crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
                needs = UnityEngine.Object.FindObjectOfType<NeedsManager>(); water = game.Water;
                inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                var fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
                var food = UnityEngine.Object.FindObjectOfType<ProductionManager>();
                var ocean = UnityEngine.Object.FindObjectOfType<OceanManager>();
                var salvage = UnityEngine.Object.FindObjectOfType<SalvageManager>();
                var camera = UnityEngine.Object.FindObjectOfType<CameraManager>();
                var panel = (UICommon_Build)ui.GetView(UICommon_Build.Address); var canvas = ui.GetComponent<Canvas>();
                Require(game.IsInitialized && water.IsInitialized && game.Saves.IsInitialized, "New systems initialize in the scene");
                Require(panel.ResourceTiles.Length == ResourceManager.TypeCount && panel.WaterText != null && panel.SaveButton != null, "Actual AA HUD contains every resource and save controls");
                foreach (var icon in panel.GetComponentsInChildren<ResourceIcon>())
                    Require(icon.GetComponent<CanvasRenderer>() != null, "Resource illustration has an actual rendering component");
                panel.SpeedTwoButton.onClick.Invoke(); Require(game.TimeControl.Speed == 2 && Time.timeScale == 2, "2x button controls engine time");
                panel.SpeedFourButton.onClick.Invoke(); Require(game.TimeControl.Speed == 4 && Time.timeScale == 4, "4x button controls engine time");
                double elapsed = game.TimeControl.ElapsedSeconds;
                await RealWait(.2);
                Require(game.TimeControl.ElapsedSeconds > elapsed + .2, "Actual LateUpdate accumulates accelerated simulation time");
                panel.PauseButton.onClick.Invoke();
                float satiety = needs.Satiety, hydration = needs.Hydration, oceanTime = ocean.ElapsedTime;
                Vector3 position = crew.Worker.position; elapsed = game.TimeControl.ElapsedSeconds;
                await RealWait(.2);
                Require(Time.timeScale == 0 && needs.Satiety == satiety && needs.Hydration == hydration && ocean.ElapsedTime == oceanTime && crew.Worker.position == position && game.TimeControl.ElapsedSeconds == elapsed, "Pause freezes needs, ocean, crew and clock across real frames");
                panel.BuildButton.onClick.Invoke(); Require(build.CatalogOpen, "Paused B button opens catalog"); panel.DeckRecipeButton.onClick.Invoke();
                Require(build.Tool == BuildTool.DeckBlueprint && build.TryPlaceBlueprint(new Vector2Int(2, 1), out _), "Planning remains available during pause");
                Require(build.TryCancelBlueprint(new Vector2Int(2, 1), out _), "Paused blueprint can be cancelled");
                camera.RestorePose(new Vector3(1, 0, 1), 42, 10); Require(camera.Yaw == 42, "Camera remains independent of paused time");
                panel.SpeedOneButton.onClick.Invoke(); Require(!game.TimeControl.IsPaused && Time.timeScale == 1, "Speed button resumes simulation");
                game.enabled = false; fishing.SetAutomaticFishing(false); crew.CancelAssignments();
                inventory.Add(ResourceType.Wood, 40); inventory.Add(ResourceType.Scrap, 10); inventory.Add(ResourceType.Fish, 2);
                build.UnlockPurifier(); panel.WaterButton.onClick.Invoke(); Require(build.Tool == BuildTool.PurifierBlueprint, "Unlocked purifier card selects placement");
                var coordinate = new Vector2Int(1, 0);
                int woodBefore = inventory.GetTotal(ResourceType.Wood), scrapBefore = inventory.GetTotal(ResourceType.Scrap);
                Require(build.TryPlaceFacilityBlueprint(coordinate, 3, FacilityKind.Purifier, out _), "Purifier uses completed deck and adjacent accessible work cell");
                StepUntil(() => build.PurifierCount == 1, 20);
                Require(inventory.GetTotal(ResourceType.Wood) == woodBefore - build.PurifierCost.Wood && inventory.GetTotal(ResourceType.Scrap) == scrapBefore - build.PurifierCost.Scrap && build.GrillCount == 0 && build.Cells[coordinate].IsBlocked, "Crew constructs correct facility and settles its cost exactly once");
                water.SetTarget(4); StepUntil(() => crew.Activity == CrewActivity.Purifying && crew.WorkProgress >= .4f, 15);
                Require(crew.CurrentCell == build.Grills[coordinate].WorkCell && inventory.GetReserved(ResourceType.Wood) == 1 && inventory.GetTotal(ResourceType.Water) == 0, "Purifying reserves fuel at correct work cell");
                float progress = crew.WorkProgress; crew.Tick(0); Require(crew.WorkProgress == progress, "Purifying pauses without progress");
                await FlushFrames(); Directory.CreateDirectory("Docs/Previews");
                VisualPreviewCapture.Capture(Camera.main, canvas, panel, "Docs/Previews/Progression_Purifying.png", 1600, 900);
                StepUntil(() => inventory.GetTotal(ResourceType.Water) == 4 && crew.Activity == CrewActivity.Idle, 30);
                woodBefore = inventory.GetTotal(ResourceType.Wood); Step(5);
                Require(inventory.GetTotal(ResourceType.Water) == 4 && inventory.GetTotal(ResourceType.Wood) == woodBefore, "Water production stops at stock target");
                needs.SetHydration(20); StepUntil(() => crew.Activity == CrewActivity.Drinking && crew.WorkProgress >= .2f, 5);
                Require(inventory.GetReserved(ResourceType.Water) == 1 && crew.Worker.GetComponent<CrewView>().DrinkCup.activeInHierarchy, "Thirsty crew reserves one water and displays mug");
                VisualPreviewCapture.Capture(Camera.main, canvas, panel, "Docs/Previews/Progression_Drinking.png", 1600, 900);
                StepUntil(() => needs.Hydration > 55 && crew.Activity != CrewActivity.Drinking, 5);
                Require(inventory.GetTotal(ResourceType.Water) == 3 && inventory.GetReserved(ResourceType.Water) == 0, "Drink settles once and restores hydration");
                StepUntil(() => inventory.GetTotal(ResourceType.Water) >= 4 && crew.Activity == CrewActivity.Idle, 15);
                needs.SetHydration(0); needs.SetSatiety(0); Require(Mathf.Abs(needs.Efficiency - .65f) < .001f, "Combined needs retain gentle 65 percent floor");
                needs.SetSatiety(80); needs.SetHydration(80);
                TestTransactions(coordinate);
                for (int index = 0; index < 3; index++) { ui.Close(UICommon_Build.Address); await ui.OpenAsync(UICommon_Build.Address); }
                int target = water.Target; panel.WaterTargetPlusButton.onClick.Invoke(); Require(water.Target == target + 1, "Cached water control binds once");
                panel.WaterTargetMinusButton.onClick.Invoke();
                var grill = new Vector2Int(-1, 0);
                Require(build.TryPlaceGrillBlueprint(grill, 1, out _), "Grill coexists with purifier");
                StepUntil(() => build.GrillCount == 1, 20); StepUntil(() => crew.Activity == CrewActivity.Cooking && crew.WorkProgress > .25f, 20);
                Require(build.PurifierCount == 1 && inventory.GetReserved(ResourceType.Fish) == 1, "Food scheduling ignores purifier and uses grill");
                game.TimeControl.SetPaused(true); food.SetTarget(2); water.SetTarget(6);
                Require(build.TryPlaceBlueprint(new Vector2Int(2, 1), out _), "Save fixture deck blueprint");
                Require(build.TryBeginConstruction(new Vector2Int(2, 1)) && build.SetConstructionProgress(new Vector2Int(2, 1), .45f), "Save fixture has partial construction");
                camera.RestorePose(new Vector3(1, 0, 0), 35, 10);
                var snapshot = game.Saves.Capture();
                Require(game.Saves.Save(), "Save accepts current partial tasks and reservations");
                panel.SaveButton.onClick.Invoke(); Require(File.Exists(game.Saves.ManualPath + ".bak"), "Atomic overwrite retains previous backup");
                string saved = File.ReadAllText(game.Saves.ManualPath);
                crew.CancelAssignments(); inventory.Add(ResourceType.Wood, 20); needs.SetHydration(5); water.SetTarget(0); camera.RestorePose(Vector3.zero, 80, 20);
                panel.LoadButton.onClick.Invoke();
                Require(game.Saves.Status == "读档成功" && inventory.ReservationCount == 0 && inventory.GetTotal(ResourceType.Wood) == snapshot.Inventory.Wood, "Load restores totals and releases transient claims without duplication");
                Require(build.GrillCount == 1 && build.PurifierCount == 1 && build.Grills[coordinate].Rotation == 3 && build.Cells[new Vector2Int(2, 1)].State == DeckState.Planned && Mathf.Abs(build.Cells[new Vector2Int(2, 1)].ConstructionProgress - .45f) < .001f, "Load restores facility kinds, rotation and partial construction");
                Require(needs.Hydration == snapshot.Hydration && needs.Satiety == snapshot.Satiety && water.Target == 6 && food.Target == 2 && game.TimeControl.IsPaused && camera.Yaw == 35 && Mathf.Abs(camera.Size - 10) < .001f, "Load restores needs, targets, clock and camera");
                Require(salvage.CaptureItems().Count == snapshot.Floating.Count && ocean.ElapsedTime == snapshot.OceanTime, "Load restores floating items and ocean phase");
                TestRejectedLoad(game.Saves, "{ broken json");
                var invalid = JsonUtility.FromJson<SaveData>(saved); invalid.Facilities[0].Kind = (FacilityKind)99; TestRejectedLoad(game.Saves, JsonUtility.ToJson(invalid));
                invalid = JsonUtility.FromJson<SaveData>(saved); invalid.Inventory.Water = -1; TestRejectedLoad(game.Saves, JsonUtility.ToJson(invalid));
                invalid = JsonUtility.FromJson<SaveData>(saved); invalid.Decks.Add(new DeckSave { X = 20, Z = 20, Complete = true }); TestRejectedLoad(game.Saves, JsonUtility.ToJson(invalid));
                File.WriteAllText(game.Saves.ManualPath, saved);
                Require(game.Saves.Save(true) && File.Exists(game.Saves.AutoPath) && game.Saves.AutoPath != game.Saves.ManualPath, "Autosave uses separate slot");
                Require(game.Saves.Load(true), "Automatic slot restores");
                await FlushFrames(); build.SetTool(BuildTool.DeckBlueprint);
                VisualPreviewCapture.Capture(Camera.main, canvas, panel, "Docs/Previews/Progression_Planning.png", 1600, 900);
                needs.SetHydration(18);
                VisualPreviewCapture.Capture(Camera.main, canvas, panel, "Docs/Previews/Progression_Compact.png", 1280, 800);
                var pointer = new PointerEventData(EventSystem.current);
                VisualPreviewCapture.Capture(Camera.main, canvas, panel, "Docs/Previews/Progression_Tooltip.png", 1600, 900, () =>
                {
                    pointer.position = RectTransformUtility.WorldToScreenPoint(Camera.main, panel.ResourceTiles[4].transform.TransformPoint(new Vector3(90, -70, 0)));
                    panel.ResourceTiles[4].OnPointerEnter(pointer);
                });
                panel.ResourceTiles[4].OnPointerExit(pointer);
                game.TimeControl.SetPaused(false); needs.SetHydration(80);
                StepUntil(() => build.Cells[new Vector2Int(2, 1)].State == DeckState.Complete, 20);
                Require(inventory.GetTotal(ResourceType.Wood) == snapshot.Inventory.Wood - build.DeckCost.Wood, "Partial construction resumes and charges once after load");
                File.WriteAllText("Docs/Previews/ProgressionValidation.txt", "PASS: " + assertions + " assertions. Actual Play mode and AA-loaded HUD; 1/2/4x and real-frame pause; paused planning/camera; crew builds purifier, produces target stock, drinks with mug, replenishes water; gentle combined efficiency floor; reservation ownership/cancellation/exactly-once/overflow; grill coexistence; cached controls; atomic saves/backup and separate autosave; roundtrip of layout, partial construction, inventory, needs, targets, clock, camera, floating items; malformed/negative/unknown-kind/disconnected saves rejected before mutation; five captures at 1600x900 and 1280x800.\n");
                Debug.Log("PROGRESSION_VALIDATION_OK assertions=" + assertions); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void TestTransactions(Vector2Int coordinate)
        {
            crew.CancelAssignments(); water.SetTarget(10); var cell = build.Grills[coordinate]; object owner = new object(), other = new object();
            int wood = inventory.GetTotal(ResourceType.Wood), stock = inventory.GetTotal(ResourceType.Water);
            var session = water.TryAcquire(owner, cell); Require(session != null && water.TryAcquire(other, cell) == null, "Single owner per purifier session");
            Require(!water.Begin(session, other, cell.WorkCell) && !water.Begin(session, owner, cell.Coordinate), "Foreign owner and wrong cell rejected");
            Require(water.Begin(session, owner, cell.WorkCell), "Purification fuel reserved"); water.Cancel(session, other);
            Require(inventory.GetReserved(ResourceType.Wood) == 1, "Foreign cancellation cannot release fuel"); water.Cancel(session, owner);
            Require(inventory.GetTotal(ResourceType.Wood) == wood && inventory.GetReserved(ResourceType.Wood) == 0 && !water.Advance(session, owner, cell.WorkCell, 100), "Cancellation refunds reservation without output");
            session = water.TryAcquire(owner, cell); Require(water.Begin(session, owner, cell.WorkCell) && water.Advance(session, owner, cell.WorkCell, 100), "Completed purification settles");
            Require(inventory.GetTotal(ResourceType.Water) == stock + 2 && inventory.GetTotal(ResourceType.Wood) == wood - 1 && !water.Advance(session, owner, cell.WorkCell, 100), "Purification output exactly once");
            needs.SetHydration(10); var drink = needs.TryBeginDrink(owner); Require(drink != null && !needs.AdvanceDrink(drink, other, 100), "Drink owner guard");
            needs.CancelDrink(drink, owner); Require(inventory.GetReserved(ResourceType.Water) == 0 && needs.Hydration == 10, "Drink cancellation releases water");
            var before = inventory.CaptureInventory(); inventory.RestoreInventory(new ResourceCost(before.Wood, before.Scrap, before.Fish, before.CookedFish, int.MaxValue));
            Require(!inventory.Add(ResourceType.Water, 1) && water.TryAcquire(owner, cell) == null, "Water overflow rejected"); inventory.RestoreInventory(before);
            needs.SetHydration(80); water.SetTarget(4);
        }
        private static void TestRejectedLoad(SaveManager saves, string json)
        {
            string before = JsonUtility.ToJson(saves.Capture()); File.WriteAllText(saves.ManualPath, json);
            Require(!saves.Load(), "Invalid save rejected");
            var left = JsonUtility.FromJson<SaveData>(before); var right = saves.Capture(); left.SavedUtc = right.SavedUtc;
            Require(JsonUtility.ToJson(left) == JsonUtility.ToJson(right), "Invalid load leaves live state unchanged");
        }
        private static void Step(float duration) { for (int step = 0; step < duration * 20; step++) { needs.Tick(.05f); crew.Tick(.05f); } }
        private static void StepUntil(Func<bool> condition, float seconds)
        {
            for (int step = 0; step < seconds * 20 && !condition(); step++)
            { needs.Tick(.05f); crew.Tick(.05f); Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Crew stays on walkable deck"); }
            Require(condition(), "Scenario completed within " + seconds + " simulated seconds; activity=" + crew.Activity);
        }
        private static async Task RealWait(double seconds) { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) await NextFrame(); }
        private static async Task FlushFrames() { int frame = Time.frameCount; while (Time.frameCount <= frame + 1) await NextFrame(); }
        private static Task NextFrame() { var completion = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => completion.TrySetResult(true); return completion.Task; }
        private static void Require(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Wait; Time.timeScale = 1;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode && Application.isBatchMode) EditorApplication.Exit(code); };
            EditorApplication.ExitPlaymode();
        }
    }
}
