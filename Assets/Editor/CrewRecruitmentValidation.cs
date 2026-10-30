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
    public static class CrewRecruitmentValidation
    {
        private const string Key = "RaftGame.CrewRecruitmentValidation";
        private static GameManager game;
        private static CrewManager crew;
        private static BuildManager build;
        private static ResourceManager stock;
        private static SalvageManager salvage;
        private static FishingManager fishing;
        private static ProductionManager production;
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
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) Finish(1);
        }
        private static async void Run()
        {
            try
            {
                game = UnityEngine.Object.FindObjectOfType<GameManager>(); crew = game.Crew;
                build = UnityEngine.Object.FindObjectOfType<BuildManager>(); stock = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                salvage = UnityEngine.Object.FindObjectOfType<SalvageManager>(); fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
                production = UnityEngine.Object.FindObjectOfType<ProductionManager>(); hud = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && game.Recruitment.IsInitialized && crew.Count == 1 && hud.CrewButton != null, "GameManager initializes one sailor, rescue and AA HUD button");
                game.TimeControl.SetPaused(true); game.enabled = false; Reset(1);
                game.Recruitment.Tick(0); Require(!game.Recruitment.HasRaft, "Pause does not advance rescue arrival");
                game.Recruitment.Tick(44); Require(!game.Recruitment.HasRaft, "Rescue waits for first arrival timer");
                game.Recruitment.Tick(1); Require(game.Recruitment.HasRaft && !game.Recruitment.Marked, "Rescue raft appears after 45 simulated seconds");
                Require(hud.StatusText.text.Contains("救生筏"), "HUD announces the rescue without opening a menu");
                Directory.CreateDirectory("Docs/Previews");
                Capture("Crew_Rescue", 1600, 900);
                Vector3 point = game.Recruitment.Position;
                fishing.SetAutomaticFishing(true); StepUntil(() => crew.Activity == CrewActivity.Fishing, 160);
                Require(game.Recruitment.TryToggleAt(new Ray(Camera.main.transform.position, point - Camera.main.transform.position)), "World ray marks the rescue raft");
                Step(); Require(crew.Activity != CrewActivity.Fishing, "Marked rescue preempts active fishing and releases the shared rod");
                game.Recruitment.Tick(10); Require(Vector2.Distance(new Vector2(point.x, point.z), new Vector2(game.Recruitment.Position.x, game.Recruitment.Position.z)) < .001f, "Marked raft stays within reach");
                Require(game.Saves.Save() && game.Saves.Load() && game.Recruitment.Marked, "Pending marked rescue survives save/load without a live task session");
                fishing.SetAutomaticFishing(true);
                game.enabled = true; game.TimeControl.SetSpeed(4);
                await Until(() => crew.Activity == CrewActivity.Rescuing && crew.WorkProgress > .1f, 12);
                game.TimeControl.SetPaused(true); float progress = crew.WorkProgress;
                await RealWait(.2); Require(crew.WorkProgress == progress && crew.Count == 1, "Pause freezes rescue progress before recruitment");
                Require(game.Recruitment.SetMarked(false), "Unmark rescue during work"); crew.Tick(0);
                Require(crew.Activity != CrewActivity.Rescuing && crew.Count == 1, "Unmark cancels the owned rescue session without recruiting");
                game.Recruitment.SetMarked(true); game.TimeControl.SetSpeed(4);
                await Until(() => crew.Count == 2, 12);
                game.TimeControl.SetPaused(true); game.enabled = false;
                Require(!game.Recruitment.HasRaft && !crew.TryRecruit(out _) && !game.Recruitment.SpawnNearRaft(), "Completion removes the raft exactly once and enforces cap two");
                Require(crew.Members[1].DisplayName == "小澜" && crew.Members[0].Needs != crew.Members[1].Needs && crew.Members[0].Worker != crew.Members[1].Worker, "Recruit has its own name, actor and needs");
                Require(crew.IsOccupied(crew.Members[0].CurrentCell) && crew.IsOccupied(crew.Members[1].CurrentCell), "Facility placement protects both sailors");
                TestNeeds(); TestCoordination(); TestProduction(); TestSaves();
                await TestPanel();
                    File.WriteAllText("Docs/Previews/CrewRecruitmentValidation.txt", "PASS: " + checks + " checks; actual Play mode and built Addressables HUD/Pop panel; timed rescue, HUD notice, world-ray marking, frozen marked raft, fishing preemption, pause/cancel, recruitment cap and landing; independent needs, shared-stock reservations; exclusive construction, stations and rod, live work preferences and fallback; parallel grills and purifiers with target limits and reservation save/load; roster/rescue persistence, v1/v2 migration, invalid-save rejection; cached modal UI, live coordinates and 1600x900/1280x800 captures.\n");
                Debug.Log("CREW_RECRUITMENT_VALIDATION_OK checks=" + checks); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void TestNeeds()
        {
            Reset(2); var a = crew.Members[0].Needs; var b = crew.Members[1].Needs;
            a.SetSatiety(80); b.SetSatiety(40); a.SetHydration(90); b.SetHydration(50); game.Needs.Tick(10);
            Require(Mathf.Abs(a.Satiety - 79.2f) < .001f && Mathf.Abs(b.Satiety - 39.2f) < .001f && Mathf.Abs(a.Hydration - 89.3f) < .001f && Mathf.Abs(b.Hydration - 49.3f) < .001f, "Needs tick once per member and maintain separate levels");
            a.SetSatiety(20); b.SetSatiety(20); stock.RestoreInventory(new ResourceCost(0, 0, 0, 1, 1));
            object ownerA = new object(), ownerB = new object(); var meal = a.TryBeginMeal(ownerA);
            Require(meal != null && b.TryBeginMeal(ownerB) == null && stock.GetTotal(ResourceType.CookedFish) == 1 && stock.GetAvailable(ResourceType.CookedFish) == 0, "One meal can be reserved by only one person");
            Require(!a.Advance(meal, ownerB, 100), "Foreign meal owner cannot consume another sailor's ration");
            Require(a.Advance(meal, ownerA, 100) && a.Satiety == 55 && b.Satiety == 20 && stock.GetTotal(ResourceType.CookedFish) == 0, "Eating restores only its owner and consumes exactly one meal");
            a.SetHydration(20); b.SetHydration(20); var drink = b.TryBeginDrink(ownerB);
            Require(drink != null && a.TryBeginDrink(ownerA) == null, "Shared water is reserved exclusively across personal needs");
            b.CancelDrink(drink, ownerB); Require(stock.GetAvailable(ResourceType.Water) == 1, "Canceled drink returns the reserved water");
            drink = b.TryBeginDrink(ownerB); Require(b.AdvanceDrink(drink, ownerB, 100) && b.Hydration == 60 && a.Hydration == 20, "Drinking restores only the correct sailor");
        }
        private static void TestCoordination()
        {
            Reset(2); var target = new Vector2Int(0, 2); Require(build.TryPlaceBlueprint(target, out _), "Plan one construction target");
            Step(); Require((crew.Members[0].TaskCell == target ? 1 : 0) + (crew.Members[1].TaskCell == target ? 1 : 0) == 1, "One blueprint has one owner before arrival");
            int wood = stock.GetTotal(ResourceType.Wood), scrap = stock.GetTotal(ResourceType.Scrap);
            StepUntil(() => build.Cells[target].State == DeckState.Complete, 400);
            Require(stock.GetTotal(ResourceType.Wood) == wood - build.DeckCost.Wood && stock.GetTotal(ResourceType.Scrap) == scrap - build.DeckCost.Scrap, "Two workers finish one blueprint for one material charge");
            Reset(2); crew.Members[0].RestorePosition(new Vector2Int(-1, 0));
            crew.SetPreference(1, WorkPreference.GatheringFirst); crew.SetPreference(2, WorkPreference.BuildingProductionFirst);
            Require(build.TryPlaceBlueprint(target, out _), "Plan construction alongside fishing"); fishing.SetAutomaticFishing(true);
            StepUntil(() => crew.Members[0].Activity == CrewActivity.Fishing && crew.Members[1].Activity == CrewActivity.Constructing, 180);
            Require(crew.Members[0].TaskCell == null && crew.Members[1].TaskCell == target, "Gathering-first sailor fishes while building-first sailor constructs");
            int fishers = 0; foreach (var member in crew.Members) if (member.Activity == CrewActivity.Fishing) fishers++;
            Require(fishers == 1, "Starter rod has one owner");
            Vector3 position = crew.Members[1].Worker.position; crew.SetPreference(2, WorkPreference.GatheringFirst);
            Require(crew.Members[1].Worker.position == position && stock.ReservationCount == 0, "Changing preference releases materials without teleporting");
            fishing.SetAutomaticFishing(false); StepUntil(() => build.Cells[target].State == DeckState.Complete, 400);
            Require(build.BlueprintCount == 0, "Gathering-first crew fills in for construction when gathering is unavailable");
            Reset(1);
            var oneCell = new List<DeckSave> { new DeckSave { X = 0, Z = 0, Complete = true, Progress = 1 } };
            build.RestoreLayout(oneCell, new List<FacilitySave>());
            Require(!crew.CanRecruit(out _) && !game.Recruitment.SpawnNearRaft(), "Recruitment waits for an unoccupied connected landing cell");
        }
        private static void TestProduction()
        {
            Reset(2); var a = Complete(new Vector2Int(-1, 1), 2); var b = Complete(new Vector2Int(1, 1), 2);
            crew.Members[0].RestorePosition(a.WorkCell); crew.Members[1].RestorePosition(b.WorkCell);
            stock.RestoreInventory(new ResourceCost(10, 0, 4)); production.SetTarget(2); Step();
            Require(crew.Members[0].Activity == CrewActivity.Cooking && crew.Members[1].Activity == CrewActivity.Cooking && stock.ReservationCount == 2, "Two different grills operate simultaneously with separate reservations");
            Require(game.Saves.Save() && game.Saves.Load() && stock.GetTotal(ResourceType.Wood) == 10 && stock.GetTotal(ResourceType.Fish) == 4 && stock.ReservationCount == 0, "Save during two batches restores stock without preserving live reservations");
            StepUntil(() => stock.GetTotal(ResourceType.CookedFish) == 2, 350);
            for (int i = 0; i < 150; i++) Step();
            Require(stock.GetTotal(ResourceType.CookedFish) == 2 && stock.GetTotal(ResourceType.Wood) == 8 && stock.GetTotal(ResourceType.Fish) == 2 && stock.ReservationCount == 0, "Combined target counts prevent duplicate batches or excess consumption");
            production.SetTarget(3); Step();
            int cooking = 0; foreach (var member in crew.Members) if (member.Activity == CrewActivity.Cooking || member.Activity == CrewActivity.Walking) cooking++;
            Require(cooking == 1, "Only one remaining output slot is scheduled");
            Reset(2); Complete(new Vector2Int(-1, 0), 1); Complete(new Vector2Int(1, 0), 3);
            stock.RestoreInventory(new ResourceCost(10, 0, 4)); production.SetTarget(4); Step();
            Require(stock.ReservationCount == 1 && crew.Members[0].Activity == CrewActivity.Cooking && crew.Members[1].Activity != CrewActivity.Cooking, "Different facilities sharing one work cell cannot be operated together");
            Reset(2); build.RestoreUnlocks(true);
            a = Complete(new Vector2Int(-1, 1), 2, FacilityKind.Purifier); b = Complete(new Vector2Int(1, 1), 2, FacilityKind.Purifier);
            crew.Members[0].RestorePosition(a.WorkCell); crew.Members[1].RestorePosition(b.WorkCell);
            stock.RestoreInventory(new ResourceCost(10, 0)); game.Water.SetTarget(4); Step();
            Require(crew.Members[0].Activity == CrewActivity.Purifying && crew.Members[1].Activity == CrewActivity.Purifying && stock.ReservationCount == 2, "Independent purifiers run two fuel reservations");
            StepUntil(() => stock.GetTotal(ResourceType.Water) == 4, 350);
            for (int i = 0; i < 100; i++) Step();
            Require(stock.GetTotal(ResourceType.Wood) == 8 && stock.GetTotal(ResourceType.Water) == 4 && stock.ReservationCount == 0, "Parallel water batches credit once and stop at combined target");
        }
        private static void TestSaves()
        {
            Reset(2); crew.SetPreference(1, WorkPreference.GatheringFirst); crew.SetPreference(2, WorkPreference.BuildingProductionFirst);
            crew.Members[0].Needs.SetSatiety(61); crew.Members[1].Needs.SetSatiety(32); crew.Members[1].Needs.SetHydration(47);
            var saved = game.Saves.Capture(); Require(saved.Version == SaveData.CurrentVersion && game.Saves.Validate(saved, out _) && game.Saves.Save(), "Current version captures complete two-person roster");
            crew.Members[1].Needs.SetSatiety(99); crew.SetPreference(2, WorkPreference.Automatic);
            Require(game.Saves.Load() && crew.Count == 2 && crew.Members[1].Needs.Satiety == 32 && crew.Members[1].Needs.Hydration == 47 && crew.Members[1].Preference == WorkPreference.BuildingProductionFirst, "Load restores independent needs and preferences");
            Reject(saved, d => d.CrewMembers.Add(d.CrewMembers[1])); Reject(saved, d => d.CrewMembers[1].Id = 1);
            Reject(saved, d => d.CrewMembers[1].Name = ""); Reject(saved, d => d.CrewMembers[1].Name = "bad\nname");
            Reject(saved, d => d.CrewMembers[1].Preference = (WorkPreference)99); Reject(saved, d => d.CrewMembers[1].Hydration = float.NaN);
            Reject(saved, d => d.CrewMembers[1].X = 100); Reject(saved, d => d.Recruitment.Active = true);
            Reject(saved, d => d.CrewMembers[0].Satiety = 8);
            var old = Clone(saved); old.Version = 2; old.CrewMembers = null; old.Recruitment = null;
            File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(old));
            Require(game.Saves.Load() && crew.Count == 1 && game.Needs.GetMember(2) == null && crew.Members[0].Needs.Satiety == 61 && crew.Primary.Preference == WorkPreference.Automatic, "Version two migrates to one sailor and releases the removed sailor's needs");
            old = game.Saves.Capture(); old.Version = 1; old.CrewMembers = null; old.Recruitment = null;
            File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(old)); Require(game.Saves.Load() && crew.Count == 1 && game.Saves.Capture().Version == SaveData.CurrentVersion, "Version one migrates through schema upgrades");
            Reset(1); Require(game.Recruitment.SpawnNearRaft(), "Spawn rescue fixture"); game.Recruitment.SetMarked(true);
            var pending = game.Saves.Capture(); Require(game.Saves.Validate(pending, out _) && game.Saves.Save(), "Pending rescue captures its location and mark");
            game.Recruitment.SetMarked(false); Require(game.Saves.Load() && game.Recruitment.Marked && crew.Count == 1, "Loading pending rescue does not duplicate its passenger");
            var malformed = Clone(pending); malformed.CrewMembers[0].Id = 9;
            File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(malformed));
            Require(!game.Saves.Load() && crew.Count == 1 && game.Recruitment.Marked, "Invalid roster is rejected before changing runtime state");
            Reset(1); game.Recruitment.Tick(45); Require(game.Recruitment.HasRaft, "Arrival fixture");
            game.Recruitment.Tick(181); Require(!game.Recruitment.HasRaft, "Unmarked rescue can drift away");
            game.Recruitment.Tick(30); Require(game.Recruitment.HasRaft, "A missed rescue retries after thirty simulated seconds");
        }
        private static async Task TestPanel()
        {
            Reset(2); crew.SetPreference(1, WorkPreference.GatheringFirst); crew.SetPreference(2, WorkPreference.BuildingProductionFirst);
            Complete(new Vector2Int(1, 1), 2); production.SetTarget(4); stock.Add(ResourceType.Fish, 4); fishing.SetAutomaticFishing(true);
            StepUntil(() => crew.Members[0].Activity == CrewActivity.Fishing && crew.Members[1].Activity == CrewActivity.Cooking, 220);
            crew.Members[0].Needs.SetSatiety(74); crew.Members[0].Needs.SetHydration(68);
            crew.Members[1].Needs.SetSatiety(65); crew.Members[1].Needs.SetHydration(82);
            Require(game.Saves.Save(), "Valid showcase save replaces deliberately rejected fixture status");
            game.Facilities.ClearSelection(); await RealWait(.1); Capture("Crew_Working", 1600, 900);
            hud.CrewButton.onClick.Invoke(); await Until(() => ui.IsOpen(UIPop_Crew.Address), 8);
            var panel = (UIPop_Crew)ui.GetView(UIPop_Crew.Address);
            Require(panel.Layer == UILayer.Pop && panel.cache && game.CrewPanelOpen && build.WorldModalOpen, "Crew panel uses cached AA Pop layer and blocks world commands");
            Require(panel.Rows[0].Name.text == "阿航" && panel.Rows[1].Name.text == "小澜" && panel.Rows[1].Activity.text.Contains("烤鱼"), "Rows show each sailor's current work");
            Capture("Crew_Panel", 1600, 900); Capture("Crew_Compact", 1280, 800);
            panel.Rows[0].Building.onClick.Invoke(); Require(crew.Primary.Preference == WorkPreference.BuildingProductionFirst, "Panel changes preference immediately");
            panel.CloseButton.onClick.Invoke(); Require(!game.CrewPanelOpen && !build.WorldModalOpen && !ui.IsOpen(UIPop_Crew.Address), "Close restores world input");
            for (int repeat = 0; repeat < 3; repeat++) { game.OpenCrewPanel(); await Until(() => ui.IsOpen(UIPop_Crew.Address), 5); game.CloseCrewPanel(); }
            game.OpenCrewPanel(); await Until(() => ui.IsOpen(UIPop_Crew.Address), 5);
            Require(ReferenceEquals(panel, ui.GetView(UIPop_Crew.Address)), "Reopening reuses cached view");
            int version = crew.Version; panel.Rows[0].Gathering.onClick.Invoke(); Require(crew.Version == version + 1, "Cached preference control binds exactly one handler");
            game.CloseCrewPanel(); Reset(1); game.OpenCrewPanel(); await Until(() => ui.IsOpen(UIPop_Crew.Address), 5);
            Require(!panel.Rows[1].Automatic.interactable && panel.Rows[1].Name.text.Contains("等待"), "Empty slot is explained and cannot receive preferences");
            Require(build.TryPlaceBlueprint(new Vector2Int(1, 2), out _), "Plan travel fixture with live modal row");
            StepUntil(() => crew.CurrentCell != Vector2Int.zero, 100);
            Require(panel.Rows[0].Location.text.Contains($"({crew.CurrentCell.x}, {crew.CurrentCell.y})"), "Crew row refreshes coordinates during travel");
            game.CloseCrewPanel();
        }
        private static GrillCell Complete(Vector2Int cell, int rotation, FacilityKind kind = FacilityKind.Grill)
        { Require(build.TryPlaceFacilityBlueprint(cell, rotation, kind, out _) && build.TryBeginGrill(cell) && build.TryCompleteGrill(cell), "Construct production fixture"); return build.Grills[cell]; }
        private static void Reset(int count)
        {
            game.CloseCrewPanel(); crew.CancelAssignments();
            var decks = new List<DeckSave>(); for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) decks.Add(new DeckSave { X = x, Z = z, Complete = true, Progress = 1 });
            build.CatalogOpen = false; build.SetTool(BuildTool.None); build.RestoreUnlocks(false); build.RestoreLayout(decks, new List<FacilitySave>());
            stock.RestoreInventory(new ResourceCost(100, 100));
            var roster = new List<CrewSave>();
            for (int index = 0; index < count; index++) roster.Add(new CrewSave { Id = index + 1, Name = index == 0 ? "阿航" : "小澜", X = 0, Z = -index, Satiety = 100, Hydration = 100 });
            crew.RestoreMembers(roster); game.Recruitment.Restore(new RescueSave());
            salvage.RestoreItems(new List<FloatingSave>(), 1000, 41, 0); game.Loot.Restore(new LootState { RandomState = 17 });
            fishing.SetAutomaticFishing(false); production.SetTarget(0); game.Water.SetTarget(0);
        }
        private static void Step()
        {
            crew.Tick(.05f);
            foreach (var member in crew.Members) Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(member.Worker.position)), "Both workers remain on completed unblocked decks");
        }
        private static void StepUntil(Func<bool> condition, int limit)
        { for (int i = 0; i < limit && !condition(); i++) Step(); Require(condition(), "Task transition: " + crew.StatusText); }
        private static SaveData Clone(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        private static void Reject(SaveData original, Action<SaveData> mutate)
        { var data = Clone(original); mutate(data); Require(!game.Saves.Validate(data, out _), "Malformed roster rejected"); }
        private static void Capture(string name, int width, int height)
        {
            VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), hud, "Docs/Previews/" + name + ".png", width, height, () =>
            {
                for (int pass = 0; pass < 2; pass++) { Camera.main.Render(); foreach (var graphic in ui.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty(); Canvas.ForceUpdateCanvases(); }
            });
        }
        private static async Task Until(Func<bool> condition, double seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (!condition()) { Require(EditorApplication.timeSinceStartup < end, "Timed out waiting for rescue/UI"); await NextFrame(); } }
        private static async Task RealWait(double seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) await NextFrame(); }
        private static Task NextFrame() { var t = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => t.TrySetResult(true); return t.Task; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Crew recruitment validation: " + message); checks++; }
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
