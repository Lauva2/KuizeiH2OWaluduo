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
using RaftGame.Production;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class ComfortValidation
    {
        private const string Key = "RaftGame.ComfortValidation";
        private static GameManager game;
        private static BuildManager build;
        private static CrewManager crew;
        private static ResourceManager stock;
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
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) { Debug.LogError("Comfort startup timeout"); Finish(1); }
        }
        private static async void Run()
        {
            try
            {
                game = UnityEngine.Object.FindObjectOfType<GameManager>(); crew = game.Crew;
                build = UnityEngine.Object.FindObjectOfType<BuildManager>(); stock = UnityEngine.Object.FindObjectOfType<ResourceManager>(); hud = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && game.Comfort != null && game.Comfort.IsInitialized && hud.ComfortButton != null, "GameManager initializes comfort and built AA HUD controls");
                game.TimeControl.SetPaused(true); game.enabled = false; Reset(1, 8, 2);
                Require(game.Comfort.Score == 0 && game.Rest.RecoveryMultiplier == 1 && build.DecorationCount == 0, "Undecorated starter camp has base recovery and no free decor");
                hud.BuildButton.onClick.Invoke(); hud.LivingCategoryButton.onClick.Invoke();
                Require(hud.CatalogHeading.text.Contains("5") && hud.PlantButton.gameObject.activeInHierarchy && hud.RugButton.gameObject.activeInHierarchy && hud.DecorLampButton.gameObject.activeInHierarchy && hud.BedButton.gameObject.activeInHierarchy && !hud.GrillButton.gameObject.activeSelf, "Living catalog shows the bed and three starter decorations");
                Require(hud.PlantRecipeCost.text == "2" && hud.PlantScrapCost.text == "1" && hud.RugRecipeCost.text == "3" && hud.RugScrapCost.text == "0" && hud.DecorLampScrapCost.text == "2", "Catalog displays authored material costs");
                Directory.CreateDirectory("Docs/Previews"); Capture("Comfort_Catalog", 1600, 900); Capture("Comfort_CatalogCompact", 1280, 800);
                hud.RugButton.onClick.Invoke(); Require(build.Tool == BuildTool.RugBlueprint && !build.CatalogOpen && hud.ModeText.text.Contains("地毯"), "Rug recipe selects placement tool");
                Require(build.TryPlaceFacilityBlueprint(Vector2Int.zero, 0, FacilityKind.Rug, out _) && game.Comfort.Score == 0 && stock.ReservationCount == 0, "Free rug planning adds no comfort before construction");
                build.SetTool(BuildTool.None); game.enabled = true; game.TimeControl.SetSpeed(4);
                await Until(() => build.DecorationCount == 1 && crew.Activity == CrewActivity.Idle, 12);
                game.TimeControl.SetPaused(true); game.enabled = false;
                Require(stock.GetTotal(ResourceType.Wood) == 5 && stock.GetTotal(ResourceType.Scrap) == 2 && game.Comfort.Score == 20 && Near(game.Rest.RecoveryMultiplier, 1.05f) && !build.Cells[Vector2Int.zero].IsBlocked && build.BlueprintCount == 0, "Actual worker completes rug underfoot once, preserves access and earns five-percent recovery bonus");
                Require(build.TryPlaceFacilityBlueprint(new Vector2Int(1, 1), 0, FacilityKind.Plant, out _) && game.Comfort.Score == 20 && build.TryCancelBlueprint(new Vector2Int(1, 1), out _) && game.Comfort.Score == 20, "Canceled decoration blueprint contributes no score or reward");
                TestDiminishingAndMove(); TestAccess(); TestRecovery(); TestSaves(); await TestPanelsAndLighting();
                File.WriteAllText("Docs/Previews/ComfortValidation.txt", "PASS: " + checks + " checks; actual Play mode with built AA living catalog, HUD, facility and cached comfort Pop panel; rug worker construction and exact materials, no score from plans; three decoration types, geometric repeat returns, score/recovery cap, committed move contribution and exact demolition refund; nonblocking corner decor/rugs, narrow-corridor routes/expansion/cancellation, decorated bed access and crew/save cells; live comfort recovery rate and paused/foreign-session checks; version-five decor saves, move-preview recovery, version-four energy/rest-intent migration, invalid decor data rejection; day/night lamp behavior and restored paused lighting; live contribution UI, next-item estimates, modal switching/cache, and 1600x900/1280x800 day/night captures.\n");
                Debug.Log("COMFORT_VALIDATION_OK checks=" + checks); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void TestDiminishingAndMove()
        {
            Reset(1); Complete(new Vector2Int(0, 0), FacilityKind.Rug); Complete(new Vector2Int(-1, 0), FacilityKind.Plant); Complete(new Vector2Int(1, 0), FacilityKind.DecorLamp);
            Require(game.Comfort.Score == 60 && Near(game.Rest.RecoveryMultiplier, 1.15f) && game.Comfort.Count == 3, "Three different decorations earn sixty comfort and fifteen-percent recovery bonus");
            var second = Complete(new Vector2Int(-1, 1), FacilityKind.Plant); var third = Complete(new Vector2Int(1, 1), FacilityKind.Plant);
            Require(game.Comfort.Score == 75 && game.Comfort.Contribution(FacilityKind.Plant) == 35 && game.Comfort.NextContribution(FacilityKind.Plant) == 2.5f, "Repeated same-type contributions fall from twenty to ten to five");
            int wood = stock.GetTotal(ResourceType.Wood), scrap = stock.GetTotal(ResourceType.Scrap);
            Require(game.Facilities.Select(second) && game.Facilities.BeginMove() && game.Comfort.Score == 75, "Move preview keeps committed decoration contribution");
            game.Facilities.Rotate(); Require(game.Facilities.ConfirmMove(new Vector2Int(-1, -1)) && game.Comfort.Score == 75 && stock.GetTotal(ResourceType.Wood) == wood, "Free rotation and relocation preserve score and materials");
            Require(game.Facilities.BeginMove(), "Start second decoration move"); game.Facilities.CancelMove();
            Require(second.Coordinate == new Vector2Int(-1, -1) && game.Comfort.Count == 5, "Cancel move keeps original committed decoration");
            Require(build.TryDemolishFacility(third, out _) && game.Comfort.Score == 70 && stock.GetTotal(ResourceType.Wood) == wood + 2 && stock.GetTotal(ResourceType.Scrap) == scrap + 1 && !build.TryDemolishFacility(third, out _), "Demolition removes the correct marginal contribution and refunds once");
            Reset(1); int index = 0;
            foreach (var kind in RaftGame.Needs.ComfortManager.Kinds) for (int copy = 0; copy < 3; copy++) { Complete(new Vector2Int(index / 3 - 1, index % 3 - 1), kind); index++; }
            Require(game.Comfort.Score == 100 && Near(game.Comfort.RestMultiplier, 1.25f) && build.DecorationCount == 9 && build.BlueprintCount == 0, "Comfort caps at one hundred and recovery at twenty-five percent");
            Require(game.Comfort.RemovalContribution(FacilityKind.Plant) == 0, "At excess comfort, removing a redundant decoration correctly forecasts no score loss");
            foreach (var decor in new List<GrillCell>(build.Grills.Values)) Require(build.TryDemolishFacility(decor, out _), "Recover decoration fixture");
            Require(game.Comfort.Score == 0 && game.Comfort.RestMultiplier == 1 && build.DecorationCount == 0, "Removing all decor restores base recovery without negative score");
        }
        private static void TestAccess()
        {
            Reset(1); var decks = new List<DeckSave>(); for (int x = 0; x <= 2; x++) decks.Add(new DeckSave { X = x, Complete = true, Progress = 1 });
            build.RestoreLayout(decks, new List<FacilitySave>()); crew.RestorePosition(Vector2Int.zero);
            Complete(Vector2Int.zero, FacilityKind.Plant); Complete(new Vector2Int(1, 0), FacilityKind.Rug); Complete(new Vector2Int(2, 0), FacilityKind.DecorLamp);
            DeckPathfinder.FindReachable(build.Cells, Vector2Int.zero, out _, out var distances);
            Require(distances.Count == 3 && !build.CanPlaceFacility(new Vector2Int(1, 0), 0, FacilityKind.Rug, out _), "All corner decor remains walkable while one object occupies each tile");
            Require(build.TryPlaceBlueprint(new Vector2Int(3, 0), out _) && build.TryCancelBlueprint(new Vector2Int(3, 0), out _) && build.TryPlaceBlueprint(new Vector2Int(3, 0), out _), "Expansion and cancellation count decorated corridor cells as connected deck");
            StepUntil(() => build.Cells[new Vector2Int(3, 0)].State == DeckState.Complete, 350);
            Require(crew.CurrentCell == new Vector2Int(2, 0) && game.Saves.Validate(game.Saves.Capture(), out _), "Worker crosses rug and lamp tiles to build and save at decorated access");
            Reset(1); var bed = Complete(new Vector2Int(-1, 1), FacilityKind.Bed, 2);
            var rug = Complete(bed.WorkCell, FacilityKind.Rug);
            Require(!build.Cells[rug.Coordinate].IsBlocked && build.CanMoveFacility(rug, Vector2Int.zero, 1, out _) && game.Saves.Validate(game.Saves.Capture(), out _), "Rug can occupy bed access and move under a sailor without blocking the bed");
        }
        private static void TestRecovery()
        {
            Reset(1); var bed = Complete(new Vector2Int(-1, 1), FacilityKind.Bed, 2); var rug = Complete(bed.WorkCell, FacilityKind.Rug);
            var plant = Complete(new Vector2Int(0, 1), FacilityKind.Plant); Complete(new Vector2Int(1, 1), FacilityKind.DecorLamp);
            var needs = crew.Primary.Needs; needs.SetEnergy(20); object owner = new object(); var session = game.Rest.TryAcquire(owner, bed, needs);
            Require(session != null && game.Rest.Begin(session, owner, bed.WorkCell), "Acquire rest fixture on decorated bed access");
            Require(game.Rest.Advance(session, owner, bed.WorkCell, 1) && Near(needs.Energy, 23.45f), "Comfort multiplies the actual recovery rate");
            float energy = needs.Energy; Require(!game.Rest.Advance(session, new object(), bed.WorkCell, 10) && game.Rest.Advance(session, owner, bed.WorkCell, 0) && needs.Energy == energy, "Comfort gives no remote or paused recovery");
            Require(build.TryDemolishFacility(plant, out _) && game.Comfort.Score == 40, "Change comfort during an active rest session");
            game.Rest.Advance(session, owner, bed.WorkCell, 1); Require(Near(needs.Energy - energy, 3.3f), "Active sleeper uses the new comfort rate immediately"); game.Rest.Cancel(session, owner);
            crew.Primary.RestorePosition(bed.WorkCell); StepUntil(() => crew.Activity == CrewActivity.Resting, 120);
            Require(build.TryBeginFacilityMove(rug) && build.TryMoveFacility(rug, new Vector2Int(1, -1), 1, out _) && crew.Activity == CrewActivity.Resting, "Moving access rug preserves a sleeper's bed session");
            Require(build.TryBeginFacilityMove(rug) && build.TryMoveFacility(rug, bed.WorkCell, 2, out _) && crew.Activity == CrewActivity.Resting && game.Saves.Validate(game.Saves.Capture(), out _), "Rug can be returned beneath occupied access without moving the sailor");
        }
        private static void TestSaves()
        {
            Reset(1); var bed = Complete(new Vector2Int(-1, 0), FacilityKind.Bed, 1); crew.Primary.Needs.SetEnergy(44); crew.Primary.Needs.SetRestRequested(true);
            var legacy = game.Saves.Capture(); legacy.Version = 4;
            Complete(new Vector2Int(0, 0), FacilityKind.Rug); var plant = Complete(new Vector2Int(1, 1), FacilityKind.Plant); var lamp = Complete(new Vector2Int(1, -1), FacilityKind.DecorLamp);
            Hour(21); var saved = game.Saves.Capture();
            Require(saved.Version == SaveData.CurrentVersion && game.Saves.Validate(saved, out _) && game.Saves.Save(), "Version five captures decorations, safe decorated crew cell and energy");
            Require(game.Facilities.Select(plant) && game.Facilities.BeginMove(), "Begin decor move before loading");
            var preview = game.Saves.Capture(); Require(preview.Facilities.Find(f => f.Kind == FacilityKind.Plant).Coordinate == new Vector2Int(1, 1) && game.Comfort.Score == 60, "Move-preview save retains committed decor and score");
            game.Facilities.CancelMove(); Require(build.TryDemolishFacility(plant, out _) && game.Saves.Load() && game.Comfort.Score == 60 && game.Comfort.Count == 3 && build.DecorationCount == 3 && build.BedCount == 1 && Near(game.Rest.RecoveryMultiplier, 1.15f), "Load restores decor once and derives comfort without stale cached values");
            lamp = build.Grills[lamp.Coordinate]; Require(lamp.DecorationView.Lamp.enabled && lamp.DecorationView.Lamp.intensity > 1, "Paused night save restores decor lighting immediately");
            Reject(saved, d => d.Facilities.Add(d.Facilities[1])); Reject(saved, d => d.Facilities[1].Kind = (FacilityKind)55);
            Reject(saved, d => d.Facilities.Find(f => f.Kind == FacilityKind.Rug).X = 100);
            Reject(saved, d => d.Facilities.Find(f => f.Kind == FacilityKind.Rug).NetContents = new List<NetSupply> { new NetSupply { Kind = FloatingKind.Wood, Amount = 1 } });
            File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(legacy));
            Require(game.Saves.Load() && game.Saves.Capture().Version == SaveData.CurrentVersion && game.Comfort.Score == 0 && build.DecorationCount == 0 && build.BedCount == 1 && crew.Primary.Needs.Energy == 44 && crew.Primary.Needs.RestRequested, "Version four migrates without changing energy, beds or unfinished rest intent");
        }
        private static async Task TestPanelsAndLighting()
        {
            Reset(2); var bed = Complete(new Vector2Int(-1, 1), FacilityKind.Bed, 2); Complete(bed.WorkCell, FacilityKind.Rug);
            var plant = Complete(new Vector2Int(1, 1), FacilityKind.Plant); var lamp = Complete(new Vector2Int(1, -1), FacilityKind.DecorLamp);
            crew.Members[0].RestorePosition(bed.WorkCell); crew.Primary.Needs.SetEnergy(20);
            UnityEngine.Object.FindObjectOfType<FishingManager>().SetAutomaticFishing(true);
            StepUntil(() => crew.Activity == CrewActivity.Resting && crew.Members[1].Activity == CrewActivity.Fishing, 250);
            for (int i = 0; i < 35; i++) Step(); Hour(12); Require(!lamp.DecorationView.Lamp.enabled, "Decor lamp is off in daylight");
            Require(game.Saves.Save(), "Save valid decorated showcase"); await RealWait(.1); Capture("Comfort_Day", 1600, 900);
            Hour(21); Require(lamp.DecorationView.Lamp.enabled && Near(lamp.DecorationView.Lamp.intensity, 1.1f), "Decor lamp automatically lights the camp at night");
            Capture("Comfort_Night", 1600, 900); Capture("Comfort_NightCompact", 1280, 800); Hour(12);
            Require(game.Facilities.Select(plant), "Select decor contribution"); await Until(() => ui.IsOpen(UICommon_Facility.Address), 8);
            var facility = (UICommon_Facility)ui.GetView(UICommon_Facility.Address);
            Require(facility.TitleText.text.Contains("盆栽") && facility.WorkText.text.Contains("20") && !facility.TargetPlusButton.gameObject.activeSelf && facility.TargetText.text.Contains("可通行"), "Selected decoration shows contribution and recovery controls");
            Capture("Comfort_Selected", 1600, 900); game.Facilities.ClearSelection();
            hud.ComfortButton.onClick.Invoke(); await Until(() => ui.IsOpen(UIPop_Comfort.Address), 8);
            var panel = (UIPop_Comfort)ui.GetView(UIPop_Comfort.Address);
            Require(panel.Layer == UILayer.Pop && panel.cache && game.ComfortPanelOpen && build.WorldModalOpen && panel.ScoreText.text.Contains("60") && panel.ScoreText.text.Contains("15") && panel.Rows[0].Contribution.text.Contains("20") && panel.Rows[0].Next.text.Contains("10"), "AA Pop shows comfort, bonus, counts, contributions and diminished next-item estimate");
            Capture("Comfort_Panel", 1600, 900); Capture("Comfort_PanelCompact", 1280, 800);
            float energy = crew.Primary.Needs.Energy; Step(); Require(crew.Primary.Needs.Energy > energy && game.WorldPanelOpen, "World modal blocks commands while rest simulation may continue");
            panel.CloseButton.onClick.Invoke(); Require(!game.ComfortPanelOpen && !build.WorldModalOpen && !ui.IsOpen(UIPop_Comfort.Address), "Closing popup restores world input");
            for (int i = 0; i < 3; i++) { game.OpenComfortPanel(); await Until(() => ui.IsOpen(UIPop_Comfort.Address), 5); game.CloseComfortPanel(); }
            game.OpenComfortPanel(); await Until(() => ui.IsOpen(UIPop_Comfort.Address), 5); Require(ReferenceEquals(panel, ui.GetView(UIPop_Comfort.Address)), "Comfort popup reuses cached view");
            game.OpenCrewPanel(); await Until(() => ui.IsOpen(UIPop_Crew.Address), 5);
            Require(!game.ComfortPanelOpen && game.CrewPanelOpen && build.WorldModalOpen && !ui.IsOpen(UIPop_Comfort.Address), "Crew popup replaces comfort popup without releasing world input lock");
            var workers = (UIPop_Crew)ui.GetView(UIPop_Crew.Address); Require(workers.Rows[0].Energy.text.Contains("15"), "Sleeping crew row shows comfort recovery bonus");
            game.OpenComfortPanel(); await Until(() => ui.IsOpen(UIPop_Comfort.Address), 5);
            Require(!game.CrewPanelOpen && game.ComfortPanelOpen && build.WorldModalOpen, "Comfort popup replaces crew popup consistently");
            Require(build.TryDemolishFacility(plant, out _) && panel.ScoreText.text.Contains("40") && panel.Rows[0].Contribution.text.Contains("0") && hud.ComfortText.text.Contains("40"), "Contribution popup and HUD refresh immediately when decorations change");
            game.CloseComfortPanel();
        }
        private static void Reset(int count, int wood = 200, int scrap = 100)
        {
            game.CloseComfortPanel(); game.CloseCrewPanel(); game.Facilities.ClearSelection(); crew.CancelAssignments();
            var decks = new List<DeckSave>(); for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) decks.Add(new DeckSave { X = x, Z = z, Complete = true, Progress = 1 });
            build.CatalogOpen = false; build.SetTool(BuildTool.None); build.RestoreUnlocks(false); build.RestoreLayout(decks, new List<FacilitySave>());
            stock.RestoreInventory(new ResourceCost(wood, scrap)); var entries = new List<CrewSave>();
            for (int i = 0; i < count; i++) entries.Add(new CrewSave { Id = i + 1, Name = i == 0 ? "阿航" : "小澜", X = 0, Z = -i, Satiety = 100, Hydration = 100, Energy = 85 });
            crew.RestoreMembers(entries); game.Recruitment.Restore(new RescueSave());
            UnityEngine.Object.FindObjectOfType<SalvageManager>().RestoreItems(new List<FloatingSave>(), 1000, 41, 0);
            UnityEngine.Object.FindObjectOfType<FishingManager>().SetAutomaticFishing(false); UnityEngine.Object.FindObjectOfType<ProductionManager>().SetTarget(0); game.Water.SetTarget(0); Hour(12);
        }
        private static GrillCell Complete(Vector2Int cell, FacilityKind kind, int rotation = 0)
        { Require(build.TryPlaceFacilityBlueprint(cell, rotation, kind, out _) && build.TryBeginGrill(cell) && build.TryCompleteGrill(cell), "Construct fixture " + kind + " at " + cell); return build.Grills[cell]; }
        private static void Hour(float value)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Configs/Game_Raft.asset").DayNight;
            float hours = value - config.StartHour; if (hours < 0) hours += 24;
            game.TimeControl.Restore(1, true, hours / 24 * config.DayDuration); game.DayNight.Tick(); game.Comfort.Tick(); game.OceanLife.Tick();
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
        private static void Step()
        { crew.Tick(.05f); foreach (var worker in crew.Members) Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(worker.Worker.position)), "Sailors remain on completed, unblocked deck including decorated tiles"); }
        private static void StepUntil(Func<bool> condition, int limit)
        { for (int i = 0; i < limit && !condition(); i++) Step(); Require(condition(), "Task transition: " + crew.StatusText); }
        private static void Reject(SaveData original, Action<SaveData> mutate)
        { var data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original)); mutate(data); Require(!game.Saves.Validate(data, out _), "Reject malformed decoration data"); }
        private static void Capture(string name, int width, int height)
        {
            VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), hud, "Docs/Previews/" + name + ".png", width, height, () =>
            { for (int pass = 0; pass < 2; pass++) { Camera.main.Render(); foreach (var graphic in ui.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty(); Canvas.ForceUpdateCanvases(); } });
        }
        private static async Task Until(Func<bool> condition, double seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (!condition()) { Require(EditorApplication.timeSinceStartup < end, "Timed out waiting for comfort/UI"); await NextFrame(); } }
        private static async Task RealWait(double seconds) { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) await NextFrame(); }
        private static Task NextFrame() { var t = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => t.TrySetResult(true); return t.Task; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Comfort validation: " + message); checks++; }
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
