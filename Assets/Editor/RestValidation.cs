using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.Crew;
using RaftGame.Fishing;
using RaftGame.Needs;
using RaftGame.Production;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class RestValidation
    {
        private const string Key = "RaftGame.RestValidation";
        private static GameManager game;
        private static BuildManager build;
        private static CrewManager crew;
        private static ResourceManager stock;
        private static FishingManager fishing;
        private static UIManager ui;
        private static UICommon_Build hud;
        private static int checks;
        public static void Validate()
        {
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 2; AssetDatabase.SaveAssets();
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 100);
            Install(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Install()
        { if (SessionState.GetBool(Key, false)) { EditorApplication.update -= Wait; EditorApplication.update += Wait; } }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying) return;
            ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address)) { EditorApplication.update -= Wait; Run(); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) { Debug.LogError("Rest startup timeout"); Finish(1); }
        }
        private static async void Run()
        {
            try
            {
                game = UnityEngine.Object.FindObjectOfType<GameManager>(); build = UnityEngine.Object.FindObjectOfType<BuildManager>(); crew = game.Crew;
                stock = UnityEngine.Object.FindObjectOfType<ResourceManager>(); fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
                hud = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && game.Rest.IsInitialized && crew.Rest == game.Rest && hud.EnergyFill != null && hud.BedButton != null, "GameManager initializes rest, energy HUD and built AA bed recipe");
                game.TimeControl.SetPaused(true); game.enabled = false; Reset(1, 8, 2);
                hud.BuildButton.onClick.Invoke(); hud.LivingCategoryButton.onClick.Invoke();
                Require(hud.BedButton.gameObject.activeInHierarchy && hud.CatalogHeading.text.Contains("5") && !hud.GrillButton.gameObject.activeSelf && hud.BedRecipeCost.text == "6" && hud.BedScrapCost.text == "1", "Living catalog includes the starter bed for six wood and one scrap");
                Directory.CreateDirectory("Docs/Previews"); Capture("Rest_Catalog", 1600, 900);
                hud.BedButton.onClick.Invoke(); Require(build.Tool == BuildTool.BedBlueprint && !build.CatalogOpen && hud.ModeText.text.Contains("床铺"), "Bed card enters placement tool");
                var cell = new Vector2Int(-1, 0);
                Require(!build.CanPlaceFacility(new Vector2Int(-2, 0), 1, FacilityKind.Bed, out _) && !build.CanPlaceFacility(cell, 3, FacilityKind.Bed, out _), "Bed requires complete footprint and a complete access cell");
                Require(build.TryPlaceFacilityBlueprint(cell, 1, FacilityKind.Bed, out _) && stock.ReservationCount == 0, "Bed planning is free");
                build.SetTool(BuildTool.None); game.enabled = true; game.TimeControl.SetSpeed(4);
                await Until(() => build.BedCount == 1 && crew.Activity == CrewActivity.Idle, 12);
                Require(stock.GetTotal(ResourceType.Wood) == 2 && stock.GetTotal(ResourceType.Scrap) == 1 && build.BlueprintCount == 0 && build.NetCount == 0, "Actual construction settles one bed without affecting net counts");
                fishing.SetAutomaticFishing(true); await Until(() => crew.Activity == CrewActivity.Fishing, 8);
                crew.Primary.Needs.SetEnergy(20); await Until(() => crew.Activity == CrewActivity.Resting && crew.WorkProgress > .03f, 10);
                game.TimeControl.SetPaused(true); var bed = build.Grills[cell];
                Require(crew.TaskCell == cell && game.Rest.IsOccupied(bed) && bed.BedView.RestMarker.activeSelf && bed.BedView.RestMarker.GetComponent<TextMesh>().font != null, "Tired fisher owns the bed and presents a sleeping marker");
                float energy = crew.Primary.Needs.Energy, progress = crew.WorkProgress;
                await RealWait(.2); Require(crew.Primary.Needs.Energy == energy && crew.WorkProgress == progress, "Pause freezes energy recovery and progress");
                var body = Body(crew.Primary); Require(Vector3.Dot(body.up, -(bed.BedView.SleepAnchor.forward)) > .99f && Vector3.Distance(body.position, bed.BedView.SleepAnchor.position) < .001f, "Actor lies face-up along the bed while its logical cell remains safely accessible");
                Require(game.Facilities.Select(bed), "Select occupied bed"); await Until(() => ui.IsOpen(UICommon_Facility.Address), 8);
                var panel = (UICommon_Facility)ui.GetView(UICommon_Facility.Address);
                Require(panel.TitleText.text == "基础床铺" && panel.WorkText.text.Contains("休息") && !panel.TargetPlusButton.gameObject.activeSelf, "Facility panel shows bed occupant without production target controls");
                Capture("Rest_Sleeping", 1600, 900); Capture("Rest_Compact", 1280, 800);
                game.Facilities.ClearSelection(); game.TimeControl.SetSpeed(4);
                await Until(() => crew.Activity == CrewActivity.Fishing && crew.Primary.Needs.Energy > 89, 15);
                Require(!game.Rest.IsOccupied(bed) && !bed.BedView.RestMarker.activeSelf && !crew.Primary.Needs.RestRequested && Vector3.Dot(body.up, Vector3.up) > .99f, "Recovered sailor wakes upright, releases bed and resumes preferred work");
                game.TimeControl.SetPaused(true); game.enabled = false;
                TestEnergy(); TestOwnership(); TestTwoCrew(); TestInterruptions(); TestSaves(); await TestPanel();
                File.WriteAllText("Docs/Previews/RestValidation.txt", "PASS: " + checks + " checks; actual Play mode with built AA HUD, living recipe, bed facility and crew Pop panel; six-wood/one-scrap construction, safe access, actual fishing-rest-work loop, pause and speed, face-up sleep pose and marker; independent work/idle drain, recovery, shared efficiency floor; bed/session/access ownership, two beds in parallel and one-bed turn-taking; rest preference continuity, food interruption, occupied-bed move/rotation/cancel/demolition; partial-energy and rest-intent saves, v1/v2/v3 migrations, corrupt-energy rejection; cached UI, live energy and 1600x900/1280x800 captures.\n");
                Debug.Log("REST_VALIDATION_OK checks=" + checks); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void TestEnergy()
        {
            Reset(2); var a = crew.Members[0].Needs; var b = crew.Members[1].Needs;
            a.SetEnergy(80); b.SetEnergy(40); a.TickEnergy(10, true); b.TickEnergy(10, false);
            Require(Mathf.Abs(a.Energy - 78.8f) < .001f && Mathf.Abs(b.Energy - 39.8f) < .001f, "Work and idle drain are independent per sailor");
            a.TickEnergy(0, true); a.TickEnergy(-1, true); Require(Mathf.Abs(a.Energy - 78.8f) < .001f, "Zero/negative time cannot drain energy");
            a.SetEnergy(float.NaN); a.SetEnergy(float.PositiveInfinity); Require(Mathf.Abs(a.Energy - 78.8f) < .001f, "Energy ignores nonfinite inputs");
            a.SetEnergy(-20); a.SetSatiety(0); a.SetHydration(0); Require(a.Energy == 0 && Mathf.Abs(a.Efficiency - .65f) < .001f && b.Energy > 39, "Three low needs share one gentle efficiency floor and do not affect the other sailor");
            a.SetSatiety(100); a.SetHydration(100); a.SetEnergy(20);
            Require(a.Efficiency < 1 && a.Efficiency > .65f, "Fatigue alone reduces efficiency gradually");
            fishing.SetAutomaticFishing(true); StepUntil(() => crew.Members[0].Activity == CrewActivity.Fishing, 150);
            Require(build.BedCount == 0 && crew.Members[0].Needs.Energy < 20, "No bed still allows useful work at reduced efficiency");
        }
        private static void TestOwnership()
        {
            Reset(1); var bed = Complete(new Vector2Int(-1, 0), 1); var needs = crew.Primary.Needs; needs.SetEnergy(20);
            object a = new object(), b = new object(); var session = game.Rest.TryAcquire(a, bed, needs);
            Require(session != null && game.Rest.TryAcquire(b, bed, needs) == null && game.Rest.IsOccupied(bed), "One bed and one personal need cannot have two owners");
            Require(!game.Rest.Begin(session, b, bed.WorkCell) && !game.Rest.Begin(session, a, new Vector2Int(1, 0)), "Rest cannot begin for a foreign owner or from the wrong cell");
            Require(game.Rest.Begin(session, a, bed.WorkCell), "Rest begins only after arrival");
            Require(!game.Rest.Advance(session, b, bed.WorkCell, 100) && needs.Energy == 20, "Foreign owner cannot recover energy");
            Require(game.Rest.Advance(session, a, bed.WorkCell, 0) && needs.Energy == 20, "Paused session has no recovery");
            game.Rest.Advance(session, a, bed.WorkCell, 5); Require(needs.Energy == 35 && needs.RestRequested, "Partial recovery persists above the entry threshold");
            game.Rest.Cancel(session, b); Require(game.Rest.IsOccupied(bed), "Foreign cancellation cannot evict the sleeper");
            game.Rest.Cancel(session, a); Require(!game.Rest.IsOccupied(bed) && needs.Energy == 35 && needs.RestRequested && !bed.BedView.RestMarker.activeSelf, "Cancellation releases bed while keeping recovered energy and rest intent");
            var distances = new Dictionary<Vector2Int, int> { { new Vector2Int(1, 0), 0 } };
            Require(!game.Rest.TryFindBed(distances, needs, out _), "Unreachable access is never chosen");
            StepUntil(() => crew.Activity == CrewActivity.Resting, 150); Require(crew.Primary.Needs.Energy > 34.9f, "Interrupted rest resumes above entry threshold after ordinary travel drain");
        }
        private static void TestTwoCrew()
        {
            Reset(2); var a = Complete(new Vector2Int(-1, 1), 2); var b = Complete(new Vector2Int(1, 1), 2);
            crew.Members[0].RestorePosition(a.WorkCell); crew.Members[1].RestorePosition(b.WorkCell);
            crew.Members[0].Needs.SetEnergy(20); crew.Members[1].Needs.SetEnergy(10); Step();
            Require(crew.Members[0].Activity == CrewActivity.Resting && crew.Members[1].Activity == CrewActivity.Resting && stock.ReservationCount == 0, "Two independent beds recover both sailors simultaneously without consuming resources");
            float before = crew.Members[1].Needs.Energy; Step(); Require(crew.Members[1].Needs.Energy > before, "Second sailor recovers its own energy");
            Reset(2); var bed = Complete(new Vector2Int(-1, 0), 1);
            crew.Members[0].Needs.SetEnergy(20); crew.Members[1].Needs.SetEnergy(15); Step();
            Require(crew.Members[0].Activity == CrewActivity.Resting && crew.Members[1].Activity != CrewActivity.Resting && crew.Members[1].Needs.Energy <= 15, "Shared bed has one sleeper and grants no remote recovery to the waiting sailor");
            StepUntil(() => crew.Members[1].Activity == CrewActivity.Resting && crew.Members[0].Activity != CrewActivity.Resting, 650);
            Require(crew.Members[0].Needs.Energy > 89 && game.Rest.IsOccupied(bed), "Recovered first sailor releases the bed for the second");
        }
        private static void TestInterruptions()
        {
            Reset(1); var bed = Complete(new Vector2Int(-1, 1), 2); var needs = crew.Primary.Needs; needs.SetEnergy(20);
            Step(); Step(); Require(crew.Activity == CrewActivity.Walking && crew.TaskCell == bed.Coordinate, "Tired sailor travels to the bed");
            Vector3 position = crew.Worker.position; crew.SetPreference(1, WorkPreference.GatheringFirst);
            Require(crew.Worker.position == position && crew.TaskCell == bed.Coordinate, "Preference changes preserve the active rest journey");
            StepUntil(() => crew.Activity == CrewActivity.Resting, 200);
            for (int i = 0; i < 110; i++) Step(); float energy = needs.Energy;
            Require(energy > 30 && game.Facilities.Select(bed) && game.Facilities.BeginMove(), "Begin moving an occupied bed during partial recovery");
            Require(crew.Activity != CrewActivity.Resting && !game.Rest.IsOccupied(bed) && needs.Energy == energy && Vector3.Dot(Body(crew.Primary).up, Vector3.up) > .99f, "Moving bed wakes the sailor without removing recovered energy");
            var previewSave = game.Saves.Capture(); Require(previewSave.Facilities[0].X == -1 && previewSave.Facilities[0].Z == 1 && game.Saves.Validate(previewSave, out _), "Move preview saves the committed bed and safe crew cell");
            game.Facilities.CancelMove(); StepUntil(() => crew.Activity == CrewActivity.Resting, 150);
            Require(game.Facilities.BeginMove(), "Restart moving occupied bed"); game.Facilities.Rotate();
            Require(game.Facilities.ConfirmMove(new Vector2Int(1, 1)), "Confirm free bed move with rotation");
            StepUntil(() => crew.Activity == CrewActivity.Resting && crew.CurrentCell == bed.WorkCell, 250);
            Require(Vector3.Dot(Body(crew.Primary).up, -bed.BedView.SleepAnchor.forward) > .99f, "Sleeping pose follows rotated bed");
            int wood = stock.GetTotal(ResourceType.Wood), scrap = stock.GetTotal(ResourceType.Scrap);
            Require(build.TryDemolishFacility(bed, out _) && build.BedCount == 0 && !game.Rest.IsOccupied(bed) && crew.Activity != CrewActivity.Resting && stock.GetTotal(ResourceType.Wood) == wood + 6 && stock.GetTotal(ResourceType.Scrap) == scrap + 1, "Demolishing occupied bed wakes its owner and refunds exact historical cost");
            Require(!build.TryDemolishFacility(bed, out _), "Demolished bed cannot refund twice");
            Reset(1); bed = Complete(new Vector2Int(-1, 0), 1); needs = crew.Primary.Needs; needs.SetEnergy(20); Step();
            for (int i = 0; i < 80; i++) Step(); Require(needs.Energy > 30, "Food interruption fixture has recovered above sleep entry threshold");
            stock.Add(ResourceType.CookedFish, 1); needs.SetSatiety(10); Step();
            Require(crew.Activity == CrewActivity.Eating && !game.Rest.IsOccupied(bed), "Food takes priority over sleep and releases the bed");
            StepUntil(() => crew.Activity == CrewActivity.Resting && needs.Satiety > 40, 200);
            Require(needs.RestRequested && stock.GetTotal(ResourceType.CookedFish) == 0, "After eating sailor resumes unfinished rest without duplicating food");
        }
        private static void TestSaves()
        {
            Reset(2); var a = Complete(new Vector2Int(-1, 1), 2); var b = Complete(new Vector2Int(1, 1), 2);
            crew.Members[0].RestorePosition(a.WorkCell); crew.Members[1].RestorePosition(b.WorkCell);
            crew.Members[0].Needs.SetEnergy(20); crew.Members[1].Needs.SetEnergy(10); Step(); for (int i = 0; i < 180; i++) Step();
            float first = crew.Members[0].Needs.Energy, second = crew.Members[1].Needs.Energy;
            var data = game.Saves.Capture(); Require(data.Version == SaveData.CurrentVersion && first > 30 && second > 30 && game.Saves.Validate(data, out _) && game.Saves.Save(), "Version four saves partial recovery and intent for both sleepers");
            Require(game.Saves.Load() && build.BedCount == 2 && !game.Rest.IsOccupied(build.Grills[a.Coordinate]) && crew.Members[0].Needs.Energy == first && crew.Members[1].Needs.Energy == second, "Load reconstructs beds and energy without stale sessions");
            StepUntil(() => crew.Members[0].Activity == CrewActivity.Resting && crew.Members[1].Activity == CrewActivity.Resting, 120);
            Step();
            Require(crew.Members[0].Needs.Energy >= first && crew.Members[1].Needs.Energy >= second && stock.ReservationCount == 0, "Both partially rested sailors reacquire beds and continue recovery");
            Reject(data, d => d.CrewMembers[1].Energy = -1); Reject(data, d => d.CrewMembers[0].Energy = float.NaN);
            Reject(data, d => d.CrewMembers[0].Energy = 101); Reject(data, d => d.Facilities[0].Kind = (FacilityKind)77);
            Reject(data, d => d.Facilities[0].NetContents = new List<NetSupply> { new NetSupply { Kind = FloatingKind.Wood, Amount = 1 } });
            foreach (int version in new[] { 1, 2, 3 })
            {
                var old = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)); old.Version = version;
                foreach (var member in old.CrewMembers) { member.Energy = 0; member.RestRequested = true; }
                File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(old));
                Require(game.Saves.Load() && game.Saves.Capture().Version == SaveData.CurrentVersion && crew.Count == (version == 3 ? 2 : 1) && crew.Primary.Needs.Energy == 85 && !crew.Primary.Needs.RestRequested, "Legacy version " + version + " seeds configured energy and migrates roster safely");
            }
            var bad = game.Saves.Capture(); bad.CrewMembers[0].Energy = -5; float energy = crew.Primary.Needs.Energy;
            File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(bad));
            Require(!game.Saves.Load() && crew.Primary.Needs.Energy == energy && build.BedCount == 2, "Corrupt energy is rejected before runtime mutation");
        }
        private static async Task TestPanel()
        {
            Reset(2); var bed = Complete(new Vector2Int(1, 1), 2); crew.Members[1].RestorePosition(bed.WorkCell);
            crew.Members[1].Needs.SetEnergy(20); crew.SetPreference(1, WorkPreference.GatheringFirst); fishing.SetAutomaticFishing(true);
            StepUntil(() => crew.Members[1].Activity == CrewActivity.Resting && crew.Members[0].Activity == CrewActivity.Fishing, 160);
            for (int i = 0; i < 36; i++) Step(); Require(game.Saves.Save(), "Save valid showcase state");
            await RealWait(.1); Capture("Rest_TwoCrew", 1600, 900);
            game.OpenCrewPanel(); await Until(() => ui.IsOpen(UIPop_Crew.Address), 8);
            var panel = (UIPop_Crew)ui.GetView(UIPop_Crew.Address);
            Require(panel.Rows[1].Energy != null && panel.Rows[1].Energy.text.Contains("精力") && panel.Rows[1].Activity.text.Contains("休息") && hud.EnergyText.text.Contains("平均"), "Crew panel and HUD show independent and average energy");
            float before = crew.Members[1].Needs.Energy; Step(); Require(crew.Members[1].Needs.Energy > before && panel.Rows[1].Activity.text.Contains("休息"), "Modal stays live during recovery");
            Capture("Rest_CrewPanel", 1600, 900); Capture("Rest_CrewCompact", 1280, 800);
            panel.Rows[1].Building.onClick.Invoke(); Require(crew.Members[1].Activity == CrewActivity.Resting && crew.Members[1].Preference == WorkPreference.BuildingProductionFirst, "Changing future work preference does not wake a resting sailor");
            game.CloseCrewPanel(); game.OpenCrewPanel(); await Until(() => ui.IsOpen(UIPop_Crew.Address), 5);
            int version = crew.Version; panel.Rows[1].Gathering.onClick.Invoke(); Require(crew.Version == version + 1, "Cached controls bind once with energy rows"); game.CloseCrewPanel();
        }
        private static void Reset(int count, int wood = 100, int scrap = 100)
        {
            game.CloseCrewPanel(); game.Facilities.ClearSelection(); crew.CancelAssignments();
            var decks = new List<DeckSave>(); for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) decks.Add(new DeckSave { X = x, Z = z, Complete = true, Progress = 1 });
            build.CatalogOpen = false; build.SetTool(BuildTool.None); build.RestoreUnlocks(false); build.RestoreLayout(decks, new List<FacilitySave>());
            stock.RestoreInventory(new ResourceCost(wood, scrap)); var members = new List<CrewSave>();
            for (int i = 0; i < count; i++) members.Add(new CrewSave { Id = i + 1, Name = i == 0 ? "阿航" : "小澜", X = 0, Z = -i, Satiety = 100, Hydration = 100, Energy = 85 });
            crew.RestoreMembers(members); game.Recruitment.Restore(new RescueSave());
            UnityEngine.Object.FindObjectOfType<SalvageManager>().RestoreItems(new List<FloatingSave>(), 1000, 41, 0);
            fishing.SetAutomaticFishing(false); UnityEngine.Object.FindObjectOfType<ProductionManager>().SetTarget(0); game.Water.SetTarget(0);
        }
        private static GrillCell Complete(Vector2Int cell, int rotation)
        { Require(build.TryPlaceFacilityBlueprint(cell, rotation, FacilityKind.Bed, out _) && build.TryBeginGrill(cell) && build.TryCompleteGrill(cell), "Construct bed fixture"); return build.Grills[cell]; }
        private static Transform Body(CrewWorker member) => (Transform)new SerializedObject(member.Worker.GetComponent<CrewView>()).FindProperty("body").objectReferenceValue;
        private static void Step()
        { crew.Tick(.05f); foreach (var member in crew.Members) Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(member.Worker.position)), "Travel and logical access remain on completed unblocked decks"); }
        private static void StepUntil(Func<bool> condition, int limit)
        { for (int i = 0; i < limit && !condition(); i++) Step(); Require(condition(), "Task transition: " + crew.StatusText); }
        private static void Reject(SaveData source, Action<SaveData> mutate)
        { var data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(source)); mutate(data); Require(!game.Saves.Validate(data, out _), "Malformed energy or bed data is rejected"); }
        private static void Capture(string name, int width, int height)
        {
            VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), hud, "Docs/Previews/" + name + ".png", width, height, () =>
            { for (int pass = 0; pass < 2; pass++) { Camera.main.Render(); foreach (var graphic in ui.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty(); Canvas.ForceUpdateCanvases(); } });
        }
        private static async Task Until(Func<bool> condition, double seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (!condition()) { Require(EditorApplication.timeSinceStartup < end, "Timed out waiting for rest/UI"); await NextFrame(); } }
        private static async Task RealWait(double seconds) { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) await NextFrame(); }
        private static Task NextFrame() { var t = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => t.TrySetResult(true); return t.Task; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Rest validation: " + message); checks++; }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Wait;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 0; AssetDatabase.SaveAssets(); Selection.activeObject = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); if (Application.isBatchMode) EditorApplication.Exit(code);
            };
            EditorApplication.ExitPlaymode();
        }
    }
}
