using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Fishing;
using RaftGame.Needs;
using RaftGame.Ocean;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class ExplorationValidation
    {
        private const string Key = "RaftGame.ValidateExploration";
        private static int assertions;
        private static BuildManager build;
        private static CrewManager crew;
        private static NeedsManager needs;
        private static ResourceManager inventory;
        private static SalvageManager salvage;
        private static FishingManager fishing;
        private static LootManager loot;
        private static OceanManager ocean;
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
            var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address)) { EditorApplication.update -= Wait; Run(ui); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) Finish(1);
        }
        public static void UseFishCatchFixture(LootManager manager)
        {
            var config = AssetDatabase.LoadAssetAtPath<LootConfig>("Assets/Configs/Loot_Default.asset");
            float chance = config.FishWeight / (float)(config.FishWeight + config.WaterWeight + config.WoodWeight + config.ScrapWeight);
            for (int seed = 1; seed < 100000; seed++)
            {
                int state = seed; bool match = true;
                for (int index = 0; index < 5; index++) if (SeaRandom.Value(ref state) >= chance) { match = false; break; }
                if (match) { manager.Restore(new LootState { RandomState = seed }); return; }
            }
            throw new InvalidOperationException("Unable to prepare fish-specific regression sample.");
        }
        private static async void Run(UIManager ui)
        {
            try
            {
                assertions = 0;
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                build = UnityEngine.Object.FindObjectOfType<BuildManager>(); crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
                inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>(); needs = UnityEngine.Object.FindObjectOfType<NeedsManager>();
                salvage = UnityEngine.Object.FindObjectOfType<SalvageManager>(); fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
                ocean = UnityEngine.Object.FindObjectOfType<OceanManager>(); loot = game.Loot;
                var view = (UICommon_Build)ui.GetView(UICommon_Build.Address); var canvas = ui.GetComponent<Canvas>();
                game.TimeControl.SetPaused(true); game.enabled = false; fishing.SetAutomaticFishing(false); crew.CancelAssignments();
                Require(game.IsInitialized && loot.IsInitialized && !build.PurifierUnlocked && inventory.GetTotal(ResourceType.Water) == 0, "New game has starter fishing, no purifier blueprint and no free water");
                Require(salvage.Items.Count > 0 && salvage.Items.Count <= 3 && salvage.MaximumItems == 7, "Sea starts sparse and caps normal supply at seven items");
                Require(!build.TryPlaceFacilityBlueprint(new Vector2Int(1, 0), 3, FacilityKind.Purifier, out _), "Direct placement API cannot bypass blueprint lock");
                build.SetTool(BuildTool.PurifierBlueprint); Require(build.Tool != BuildTool.PurifierBlueprint, "P shortcut cannot bypass blueprint lock");
                view.BuildButton.onClick.Invoke();
                Require(build.CatalogOpen && view.CatalogOverlay.activeInHierarchy && view.DeckRecipeButton.gameObject.activeInHierarchy, "B opens actual AA-loaded build catalog with basics");
                view.ProductionCategoryButton.onClick.Invoke(); Require(view.GrillButton.gameObject.activeInHierarchy && !view.WaterButton.gameObject.activeSelf, "Production category lists only unlocked recipes");
                await FlushFrames(); Directory.CreateDirectory("Docs/Previews");
                Capture(ui, view, "Exploration_Locked", 1600, 900);
                var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .5f) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(view.CatalogOverlay.transform), "Catalog intercepts world clicks");
                view.LivingCategoryButton.onClick.Invoke(); Require(!view.CatalogEmptyText.gameObject.activeInHierarchy && view.BedButton.gameObject.activeInHierarchy && !view.GrillButton.gameObject.activeSelf, "Living category contains the starter bed without locked production cards");
                view.BasicsCategoryButton.onClick.Invoke(); view.DeckRecipeButton.onClick.Invoke(); Require(!build.CatalogOpen && build.Tool == BuildTool.DeckBlueprint, "Recipe selection closes catalog and enters placement");
                build.SetTool(BuildTool.None);
                TestCatches();
                // Verify persistence before unlocking, including the next deterministic reward.
                Require(game.Saves.Save(), "Locked exploration state saves"); string lockedSave = File.ReadAllText(game.Saves.ManualPath);
                var lockedData = game.Saves.Capture();
                Require(loot.AwardCatch(1), "Reward after locked snapshot"); var nextReward = inventory.CaptureInventory(); var nextState = loot.Capture();
                Require(game.Saves.Load() && !build.PurifierUnlocked && JsonUtility.ToJson(loot.Capture()) == JsonUtility.ToJson(lockedData.Loot), "Load restores blueprint lock and loot stream");
                Require(loot.AwardCatch(1) && JsonUtility.ToJson(inventory.CaptureInventory()) == JsonUtility.ToJson(nextReward) && JsonUtility.ToJson(loot.Capture()) == JsonUtility.ToJson(nextState), "Same action after load gives same next reward");
                TestDriftReplay(game.Saves);
                TestDrift();
                ClearSea(); ocean.RestoreTime(18); needs.SetHydration(80); needs.SetSatiety(80); fishing.SetAutomaticFishing(false); crew.CancelAssignments();
                var bottle = salvage.Spawn(FloatingKind.Bottle, new Vector3(-4.3f, 0, 0)); Require(bottle != null && salvage.SetMarked(bottle, true), "Water bottle can be marked for salvage");
                int bottledBefore = inventory.GetTotal(ResourceType.Water);
                Until(() => !salvage.IsActive(bottle), 20);
                Require(inventory.GetTotal(ResourceType.Water) == bottledBefore + 1 && !build.PurifierUnlocked, "Crew salvages bottled water before purifier unlock");
                salvage.Spawn(FloatingKind.SupplyCrate, new Vector3(4.4f, 0, 0)); salvage.Spawn(FloatingKind.Wood, new Vector3(0, 0, 6));
                needs.SetHydration(20); Until(() => crew.Activity == CrewActivity.Drinking && crew.WorkProgress > .2f, 5);
                await FlushFrames(); Capture(ui, view, "Exploration_EarlyWater", 1600, 900);
                Until(() => needs.Hydration > 55 && crew.Activity != CrewActivity.Drinking, 5);
                ClearSea(); crew.CancelAssignments(); needs.SetHydration(90);
                int safeCrateSeed = NoBlueprintSeed();
                for (int index = 0; index < 6; index++)
                {
                    var lootState = loot.Capture(); lootState.RandomState = safeCrateSeed; lootState.CratesWithoutBlueprint = index; loot.Restore(lootState);
                    var crate = salvage.Spawn(FloatingKind.SupplyCrate, new Vector3(-4.3f, 0, 0)); Require(crate != null && salvage.SetMarked(crate, true), "Crate marked for collection");
                    Until(() => !salvage.IsActive(crate), 20);
                    Require(build.PurifierUnlocked == (index == 5), "Undiscovered blueprint stays rare with sixth-crate guarantee");
                    Require(!salvage.TryCollect(crate, crew, crew.CurrentCell), "Opened crate cannot settle twice");
                }
                Require(loot.Message.Contains("海水净化器") && view.LootNotice.activeSelf, "Discovery notification explains unlock and B catalog");
                view.BuildButton.onClick.Invoke(); view.ProductionCategoryButton.onClick.Invoke();
                Require(view.WaterButton.gameObject.activeInHierarchy && build.PurifierCost.Wood == 6 && build.PurifierCost.Scrap == 3, "New purifier recipe appears after discovery with stronger construction cost");
                await FlushFrames(); Capture(ui, view, "Exploration_Unlocked", 1600, 900); Capture(ui, view, "Exploration_Compact", 1280, 800);
                for (int index = 0; index < 3; index++) { ui.Close(UICommon_Build.Address); await ui.OpenAsync(UICommon_Build.Address); }
                view.BuildButton.onClick.Invoke(); Require(build.CatalogOpen, "Cached B control binds once"); view.ProductionCategoryButton.onClick.Invoke(); view.WaterButton.onClick.Invoke();
                Require(build.Tool == BuildTool.PurifierBlueprint && !build.CatalogOpen, "Unlocked recipe selects purifier placement");
                inventory.Add(ResourceType.Wood, 10); inventory.Add(ResourceType.Scrap, 5);
                int wood = inventory.GetTotal(ResourceType.Wood), scrap = inventory.GetTotal(ResourceType.Scrap);
                Require(build.TryPlaceFacilityBlueprint(new Vector2Int(1, 0), 3, FacilityKind.Purifier, out _), "Discovered purifier blueprint can be placed");
                Until(() => build.PurifierCount == 1, 20);
                Require(inventory.GetTotal(ResourceType.Wood) == wood - 6 && inventory.GetTotal(ResourceType.Scrap) == scrap - 3, "Crew charges unlocked purifier exactly once");
                crew.CancelAssignments();
                Require(game.Saves.Save(), "Unlocked state saves"); string unlockedSave = File.ReadAllText(game.Saves.ManualPath);
                File.WriteAllText(game.Saves.ManualPath, lockedSave); Require(game.Saves.Load() && !build.PurifierUnlocked && build.PurifierCount == 0, "Earlier locked save restores progression faithfully");
                File.WriteAllText(game.Saves.ManualPath, unlockedSave); Require(game.Saves.Load() && build.PurifierUnlocked && build.PurifierCount == 1, "Unlocked save restores blueprint and facility");
                TestRejectedSave(game.Saves, unlockedSave);
                TestLegacySave(game.Saves, unlockedSave);
                File.WriteAllText(game.Saves.ManualPath, unlockedSave); Require(game.Saves.Load(), "Final new-version state reloads");
                ClearSea(); salvage.Spawn(FloatingKind.Bottle, new Vector3(-4.3f, 0, 1.4f)); salvage.Spawn(FloatingKind.SupplyCrate, new Vector3(3.8f, 0, -2)); salvage.Spawn(FloatingKind.Wood, new Vector3(-1, 0, 6.5f));
                build.SetTool(BuildTool.None); await FlushFrames(); Capture(ui, view, "Exploration_Sea", 1600, 900);
                File.WriteAllText("Docs/Previews/ExplorationValidation.txt", "PASS: " + assertions + " assertions; actual AA-loaded catalog in Play mode; locked purifier rejects direct API and hotkey; category filtering/selection/empty state/cache/raycast; fish/water/wood/scrap catches, partial cancellation, owner and exactly-once guards, water guarantee; sparse variable spawn timing/position/type, ocean current and wind drift, per-item speed, pause and cap; crew salvages and drinks bottled water before unlock; crate blueprint chance plus sixth-crate guarantee and discover notification; permanent unlock then 6-wood/3-scrap construction; loot/drift state roundtrip and next-reward replay; invalid states rejected before mutation; v1 migration retains existing purifier and old floating supplies; 1600x900/1280x800 rendered UI and resource icon pixels.\n");
                Debug.Log("EXPLORATION_VALIDATION_OK assertions=" + assertions); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void TestCatches()
        {
            fishing.SetAutomaticFishing(true); var actor = new object();
            foreach (ResourceType kind in new[] { ResourceType.Fish, ResourceType.Water, ResourceType.Wood, ResourceType.Scrap })
            {
                int seed = CatchSeed(kind); loot.Restore(new LootState { RandomState = seed });
                DeckPathfinder.FindReachable(build.Cells, crew.CurrentCell, out _, out var distances);
                Require(fishing.TryFindSpot(distances, out var spot), "Mixed catch has a valid fishing spot");
                var session = fishing.TryAcquire(actor, spot); Require(session != null, "Acquire catch");
                var prior = inventory.CaptureInventory(); var random = loot.Capture();
                Require(!fishing.Advance(session, new object(), spot.Cell, 100, out _), "Catch rejects foreign owner");
                Require(fishing.Advance(session, actor, spot.Cell, .2f, out bool partial) && !partial && JsonUtility.ToJson(prior) == JsonUtility.ToJson(inventory.CaptureInventory()) && JsonUtility.ToJson(random) == JsonUtility.ToJson(loot.Capture()), "Partial cast awards no item and consumes no random result");
                int before = inventory.GetTotal(kind); Require(fishing.Advance(session, actor, spot.Cell, 100, out bool caught) && caught, "Finished cast grants selected item");
                Require(inventory.GetTotal(kind) == before + (kind == ResourceType.Wood ? 2 : 1) && loot.LastResource == kind, "Mixed outcome credits correct resource");
                var ended = loot.Capture(); Require(!fishing.Advance(session, actor, spot.Cell, 100, out _) && JsonUtility.ToJson(ended) == JsonUtility.ToJson(loot.Capture()), "Completed catch cannot award or reroll twice");
            }
            loot.Restore(new LootState { RandomState = 12231 }); var seen = new HashSet<ResourceType>();
            for (int index = 0; index < 240; index++) { Require(loot.AwardCatch(1), "Random catch awards"); seen.Add(loot.LastResource); Require(loot.Capture().CatchesWithoutWater < 6, "No more than five dry catches before guaranteed bottle"); }
            Require(seen.Count == 4 && !seen.Contains(ResourceType.CookedFish), "Fishing samples fish, bottles and materials without cooked meals");
            fishing.SetAutomaticFishing(false);
        }
        private static int CatchSeed(ResourceType kind)
        {
            for (int seed = 1; seed < 100000; seed++) { int state = seed; float value = SeaRandom.Value(ref state); if (kind == ResourceType.Fish && value < .55f || kind == ResourceType.Water && value >= .55f && value < .8f || kind == ResourceType.Wood && value >= .8f && value < .92f || kind == ResourceType.Scrap && value >= .92f) return seed; }
            throw new InvalidOperationException("Catch seed not found");
        }
        private static int NoBlueprintSeed()
        { for (int seed = 1; seed < 10000; seed++) { int state = seed; SeaRandom.Value(ref state); if (SeaRandom.Value(ref state) > .12f) return seed; } throw new InvalidOperationException(); }
        private static void TestDrift()
        {
            ClearSea(); salvage.RestoreItems(new List<FloatingSave>(), 0, 123412, 0);
            var kinds = new HashSet<FloatingKind>(); var speeds = new HashSet<int>(); var positions = new List<Vector2>(); var ids = new HashSet<int>();
            Vector2 direction = ocean.CurrentDirection; bool changed = false;
            for (int step = 0; step < 600; step++)
            {
                ocean.Tick(1); salvage.Tick(1); Require(salvage.Items.Count <= 7, "Sparse sea never exceeds configured cap");
                if (Vector2.Angle(direction, ocean.CurrentDirection) > 20) changed = true;
                foreach (var item in salvage.Items) if (ids.Add(item.Id))
                { kinds.Add(item.Kind); positions.Add(new Vector2(item.transform.position.x, item.transform.position.z)); var saved = salvage.CaptureItems(); foreach (var entry in saved) if (entry.Kind == item.Kind) speeds.Add(Mathf.RoundToInt(entry.DriftMultiplier * 100)); }
            }
            Require(changed && positions.Count > 12 && kinds.Count == 4 && speeds.Count > 5, "Changing ocean flow produces varied positions, types and per-item speeds");
            ClearSea(); var bottle = salvage.Spawn(FloatingKind.Bottle, new Vector3(18, 0, 18)); Vector3 before = bottle.transform.position;
            Vector2 expected = ocean.GetDriftDirection(before, .7f); salvage.Tick(.1f); Vector3 movement = bottle.transform.position - before;
            Require(movement.sqrMagnitude > .000001f && Vector2.Dot(expected, new Vector2(movement.x, movement.z).normalized) > .98f, "Supplies move with the ocean current and wind field");
            before = bottle.transform.position; var state = salvage.RandomState; float phase = ocean.ElapsedTime; ocean.Tick(0); salvage.Tick(0);
            Require(bottle.transform.position == before && salvage.RandomState == state && ocean.ElapsedTime == phase, "Pause freezes drifting and random supply generation");
        }
        private static void TestDriftReplay(SaveManager saves)
        {
            for (int step = 0; step < 25; step++) { ocean.Tick(1); salvage.Tick(1); }
            string expected = JsonUtility.ToJson(new FloatingSnapshot { Items = salvage.CaptureItems() });
            int expectedRandom = salvage.RandomState, expectedSequence = salvage.Sequence; float expectedTimer = salvage.SpawnTimer;
            Require(saves.Load(), "Reload before replaying floating supply stream");
            for (int step = 0; step < 25; step++) { ocean.Tick(1); salvage.Tick(1); }
            Require(JsonUtility.ToJson(new FloatingSnapshot { Items = salvage.CaptureItems() }) == expected && salvage.RandomState == expectedRandom && salvage.Sequence == expectedSequence && salvage.SpawnTimer == expectedTimer,
                "Restored ocean and spawn stream reproduce the next supply types, positions, speeds and timing");
            var ids = new HashSet<int>(); foreach (var item in salvage.Items) Require(ids.Add(item.Id), "Restored and newly spawned supplies have distinct ownership IDs");
        }
        [Serializable] private sealed class FloatingSnapshot { public List<FloatingSave> Items; }
        private static void TestRejectedSave(SaveManager saves, string json)
        {
            var invalid = JsonUtility.FromJson<SaveData>(json); invalid.PurifierUnlocked = false; Reject(saves, JsonUtility.ToJson(invalid));
            invalid = JsonUtility.FromJson<SaveData>(json); invalid.Loot.RandomState = 0; Reject(saves, JsonUtility.ToJson(invalid));
            invalid = JsonUtility.FromJson<SaveData>(json); invalid.Floating.Add(new FloatingSave { Kind = (FloatingKind)99, DriftMultiplier = 1 }); Reject(saves, JsonUtility.ToJson(invalid));
        }
        private static void Reject(SaveManager saves, string json)
        {
            var before = saves.Capture(); File.WriteAllText(saves.ManualPath, json); Require(!saves.Load(), "Invalid progression save rejected");
            var after = saves.Capture(); before.SavedUtc = after.SavedUtc; Require(JsonUtility.ToJson(before) == JsonUtility.ToJson(after), "Rejected save leaves live state unchanged");
        }
        private static void TestLegacySave(SaveManager saves, string json)
        {
            var legacy = JsonUtility.FromJson<SaveData>(json); legacy.Version = 1; legacy.PurifierUnlocked = false;
            legacy.Floating.Clear(); for (int index = 0; index < 8; index++) legacy.Floating.Add(new FloatingSave { Type = ResourceType.Wood, Position = new Vector3(12 + index, 0, 12), Phase = .3f });
            File.WriteAllText(saves.ManualPath, JsonUtility.ToJson(legacy));
            Require(saves.Load() && build.PurifierUnlocked && build.PurifierCount == 1 && salvage.Items.Count == 8, "Version one migration retains existing purifier and old floating supplies above new spawn cap");
        }
        private static void ClearSea() { var all = new List<FloatingResource>(salvage.Items); foreach (var item in all) salvage.Remove(item); }
        private static void Until(Func<bool> condition, float seconds)
        {
            for (int index = 0; index < seconds * 20 && !condition(); index++) { needs.Tick(.05f); crew.Tick(.05f); Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Worker stays on walkable deck"); }
            Require(condition(), "Crew completes task within " + seconds + " seconds: " + crew.Activity);
        }
        private static void Capture(UIManager ui, UICommon_Build view, string name, int width, int height) => VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), view, "Docs/Previews/" + name + ".png", width, height);
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
