using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class CookingPreviewValidation
    {
        private const string Key = "RaftGame.ValidateCooking";
        private static bool running;
        private static int assertions;

        public static void Validate()
        {
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 2;
            AssetDatabase.SaveAssets(); SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 100);
            Install(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Install()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= WaitForStartup; EditorApplication.update += WaitForStartup;
        }
        private static void WaitForStartup()
        {
            if (!EditorApplication.isPlaying || running) return;
            var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address))
            { running = true; EditorApplication.update -= WaitForStartup; RunChecks(); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Debug.LogError("Cooking initialization timeout"); Finish(1); }
        }

        private static async void RunChecks()
        {
            try
            {
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                var build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                var crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
                var inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                var fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
                var production = UnityEngine.Object.FindObjectOfType<ProductionManager>();
                var needs = UnityEngine.Object.FindObjectOfType<NeedsManager>();
                var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
                var panel = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && production.IsInitialized && needs.IsInitialized, "GameManager initializes production and needs");
                Require(panel.GrillButton != null && panel.CookedFishText != null && panel.NeedsText != null, "New MVC controls load from real AA bundles");
                Require(inventory.GetTotal(ResourceType.Fish) == 0 && inventory.GetTotal(ResourceType.CookedFish) == 0 && production.Target == 4,
                    "Fresh start has no free food and a four-meal stock target");
                Time.timeScale = 4f; fishing.SetAutomaticFishing(false);
                await Until(() => crew.Activity == CrewActivity.Idle, 8, build, crew);
                production.SetTarget(1); needs.SetSatiety(70);
                panel.GrillButton.onClick.Invoke(); Require(build.Tool == BuildTool.GrillBlueprint, "Grill button selects blueprint tool");
                Vector2Int coordinate = new Vector2Int(-1, 0);
                Require(build.TryPlaceGrillBlueprint(coordinate, 1, out _), "Place one-tile grill on completed deck with a work cell");
                Require(!build.Cells[coordinate].IsBlocked && build.BlueprintCount == 1, "Pending grill stays traversable during construction");
                build.SetTool(BuildTool.None);
                await Until(() => build.GrillCount == 1, 12, build, crew);
                Require(build.CompletedCount == 9 && inventory.GetTotal(ResourceType.Wood) == 4 && inventory.GetTotal(ResourceType.Scrap) == 0,
                    "Grill costs four wood and two scrap without changing deck count");
                Require(build.Cells[coordinate].IsBlocked && !DeckPathfinder.IsWalkable(build.Cells, coordinate) && DeckPathfinder.HasDeck(build.Cells, coordinate),
                    "Completed grill is a walking obstacle and still physical deck");
                fishing.SetAutomaticFishing(true);
                await Until(() => crew.Activity == CrewActivity.Cooking && crew.WorkProgress >= 0.35f, 15, build, crew);
                Require(inventory.GetTotal(ResourceType.Fish) == 1 && inventory.GetReserved(ResourceType.Fish) == 1 &&
                    inventory.GetReserved(ResourceType.Wood) == 1 && inventory.GetTotal(ResourceType.CookedFish) == 0, "Fish caught normally, recipe inputs reserved until finish");
                Require(crew.CurrentCell == build.Grills[coordinate].WorkCell, "Crew cooks from the designated adjacent work cell");
                var fields = new SerializedObject(crew.Worker.GetComponent<CrewView>());
                Require(((GameObject)fields.FindProperty("cookingTool").objectReferenceValue).activeInHierarchy, "Cooking tongs shown");
                Time.timeScale = 0; float progress = crew.WorkProgress, satiety = needs.Satiety;
                double stop = EditorApplication.timeSinceStartup + 0.3;
                while (EditorApplication.timeSinceStartup < stop) await NextFrame();
                Require(Mathf.Approximately(crew.WorkProgress, progress) && Mathf.Approximately(needs.Satiety, satiety), "Pause freezes hunger and cooking");
                Directory.CreateDirectory("Docs/Previews");
                RaftPreviewValidation.Capture("Docs/Previews/Cooking_Grill.png", Camera.main, ui.GetComponent<Canvas>(), panel);
                needs.SetSatiety(30); Time.timeScale = 4;
                await Until(() => crew.Activity == CrewActivity.Eating && crew.WorkProgress > 0.15f, 10, build, crew);
                Require(inventory.GetTotal(ResourceType.Fish) == 0 && inventory.GetTotal(ResourceType.Wood) == 3 &&
                    inventory.GetTotal(ResourceType.CookedFish) == 1 && inventory.GetReserved(ResourceType.CookedFish) == 1, "Recipe settles once; hungry crew reserves cooked fish");
                Require(((GameObject)fields.FindProperty("mealFood").objectReferenceValue).activeInHierarchy, "Eating shows a cooked fish prop");
                Time.timeScale = 0;
                RaftPreviewValidation.Capture("Docs/Previews/Cooking_Meal.png", Camera.main, ui.GetComponent<Canvas>(), panel);
                Time.timeScale = 4;
                await Until(() => needs.Satiety > 60 && crew.Activity != CrewActivity.Eating, 8, build, crew);
                Require(inventory.GetTotal(ResourceType.CookedFish) == 0 && inventory.GetReserved(ResourceType.CookedFish) == 0, "Eating consumes a single meal and restores satiety");
                await Until(() => inventory.GetTotal(ResourceType.CookedFish) == 1 && crew.Activity == CrewActivity.Fishing, 18, build, crew);
                fishing.SetAutomaticFishing(false);
                await Until(() => crew.Activity == CrewActivity.Idle, 8, build, crew);
                inventory.Add(ResourceType.Fish, 3);
                int wood = inventory.GetTotal(ResourceType.Wood);
                stop = EditorApplication.timeSinceStartup + 0.6;
                while (EditorApplication.timeSinceStartup < stop) await NextFrame();
                Require(crew.Activity == CrewActivity.Idle && inventory.GetTotal(ResourceType.Wood) == wood, "Production stops at its food stock target");
                for (int i = 0; i < 3; i++) { ui.Close(UICommon_Build.Address); await ui.OpenAsync(UICommon_Build.Address); }
                panel.TargetPlusButton.onClick.Invoke(); Require(production.Target == 2, "Cached target control binds once");
                await Until(() => crew.Activity == CrewActivity.Cooking, 8, build, crew);
                Require(inventory.GetReserved(ResourceType.Wood) == 1, "Raising target restarts cooking");
                game.enabled = false; Time.timeScale = 1;
                crew.Shutdown();
                Require(inventory.ReservationCount == 0, "Shutdown releases unfinished recipe inputs");
                TestTransactions(); TestLayout(); TestScheduling();
                File.WriteAllText("Docs/Previews/CookingValidation.txt", "PASS: " + assertions + " assertions; actual Play mode with built Addressables UI; " +
                    "starter inventory; grill blueprint construction/cost; adjacent work cell; obstacle routing; real fishing-to-cooking-to-eating loop; " +
                    "visible cooking/eating props; pause; recipe reservations and atomic output; satiety restoration; food target stop/restart; cached controls; " +
                    "ownership/cancellation/exactly-once/overflow; connectivity-safe placement; protected work cells; occupied-cell guard; construction preemption; " +
                    "hunger interruption, efficiency floor and shutdown release.\n");
                Debug.Log("COOKING_VALIDATION_OK assertions=" + assertions); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly ResourceManager Inventory;
            public readonly BuildManager Build;
            public readonly ProductionManager Production;
            public readonly NeedsManager Needs;
            public readonly CrewManager Crew;
            private readonly ProductionConfig productionConfig;
            private readonly NeedsConfig needsConfig;
            private readonly CrewConfig crewConfig;
            public Fixture(bool corridor = false)
            {
                Root = new GameObject("Cooking Test Fixture");
                var resources = ScriptableObject.CreateInstance<ResourceConfig>(); resources.StartingInventory = new ResourceCost(8, 4, 8);
                Inventory = Root.AddComponent<ResourceManager>(); Require(Inventory.Initialize(resources), "Fixture inventory"); UnityEngine.Object.Destroy(resources);
                Transform deck = new GameObject("Deck").transform; deck.SetParent(Root.transform, false);
                for (int x = -1; x <= 1; x++) for (int z = corridor ? 0 : -1; z <= (corridor ? 0 : 1); z++)
                {
                    var tile = new GameObject("Tile").AddComponent<DeckTile>(); tile.Coordinate = new Vector2Int(x, z); tile.transform.SetParent(deck);
                    tile.transform.position = new Vector3(x * 2, 0.48f, z * 2);
                }
                Build = Root.AddComponent<BuildManager>(); var fields = new SerializedObject(Build);
                fields.FindProperty("raftRoot").objectReferenceValue = deck; fields.ApplyModifiedPropertiesWithoutUndo();
                Require(Build.Initialize(AssetDatabase.LoadAssetAtPath<BuildConfig>("Assets/Configs/Build_Default.asset"), Camera.main, Inventory), "Fixture build");
                productionConfig = ScriptableObject.CreateInstance<ProductionConfig>(); productionConfig.CookingDuration = 0.5f; productionConfig.CookedFishTarget = 1;
                Production = Root.AddComponent<ProductionManager>(); Require(Production.Initialize(productionConfig, Build, Inventory), "Fixture production");
                needsConfig = ScriptableObject.CreateInstance<NeedsConfig>(); needsConfig.DrainPerSecond = 0; needsConfig.EatingDuration = 0.2f;
                Needs = Root.AddComponent<NeedsManager>(); Require(Needs.Initialize(needsConfig, Inventory), "Fixture needs");
                crewConfig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CrewConfig>("Assets/Configs/Crew_Default.asset"));
                crewConfig.WalkSpeed = 6; crewConfig.ConstructionDuration = 0.2f;
                Crew = Root.AddComponent<CrewManager>(); Require(Crew.Initialize(crewConfig, Build, Camera.main, null, null, Production, Needs), "Fixture crew");
            }
            public GrillCell MakeGrill()
            {
                var coordinate = new Vector2Int(-1, 0);
                Require(Build.TryPlaceGrillBlueprint(coordinate, 1, out _) && Build.TryBeginGrill(coordinate) && Build.TryCompleteGrill(coordinate), "Fixture grill built");
                return Build.Grills[coordinate];
            }
            public void Step()
            {
                Needs.Tick(0.05f); Crew.Tick(0.05f);
                Require(DeckPathfinder.IsWalkable(Build.Cells, Build.WorldToCell(Crew.Worker.position)), "Crew stays on unblocked completed deck");
            }
            public void Dispose()
            {
                Crew.Shutdown(); Needs.Shutdown(); Production.Shutdown(); Build.Shutdown(); Inventory.Shutdown();
                UnityEngine.Object.Destroy(productionConfig); UnityEngine.Object.Destroy(needsConfig); UnityEngine.Object.Destroy(crewConfig); UnityEngine.Object.Destroy(Root);
            }
        }

        private static void TestTransactions()
        {
            using (var f = new Fixture())
            {
                GrillCell grill = f.MakeGrill(); object owner = new object(), other = new object();
                CookingSession session = f.Production.TryAcquire(owner, grill);
                Require(session != null && f.Production.TryAcquire(other, grill) == null, "Exclusive grill claim");
                Require(!f.Production.Begin(session, other, grill.WorkCell) && !f.Production.Begin(session, owner, new Vector2Int(1, 1)), "Recipe validates owner and work cell");
                Require(f.Production.Begin(session, owner, grill.WorkCell), "Recipe reserves on arrival");
                Require(f.Production.Advance(session, owner, grill.WorkCell, 0) && session.Progress == 0, "Zero timestep cannot cook");
                f.Production.Cancel(session, other); Require(f.Inventory.GetReserved(ResourceType.Fish) == 1, "Other owner cannot cancel");
                f.Production.Cancel(session, owner); f.Production.Cancel(session, owner);
                Require(f.Inventory.GetTotal(ResourceType.Fish) == 8 && f.Inventory.GetTotal(ResourceType.Wood) == 4 && f.Inventory.ReservationCount == 0, "Cancel releases input exactly once");
                session = f.Production.TryAcquire(owner, grill); Require(f.Production.Begin(session, owner, grill.WorkCell), "Recipe restarted");
                bool duplicate = false;
                Action callback = () => duplicate |= f.Production.Advance(session, owner, grill.WorkCell, 1);
                f.Inventory.Changed += callback;
                Require(f.Production.Advance(session, owner, grill.WorkCell, 1) && !duplicate, "Reentrant completion gives one cooked fish");
                f.Inventory.Changed -= callback;
                Require(!f.Production.Advance(session, owner, grill.WorkCell, 1) && f.Inventory.GetTotal(ResourceType.CookedFish) == 1 && f.Inventory.GetTotal(ResourceType.Wood) == 3,
                    "Duplicate completion cannot consume or award again");
                f.Needs.SetSatiety(30); MealSession meal = f.Needs.TryBeginMeal(owner);
                Require(meal != null && f.Needs.TryBeginMeal(other) == null && !f.Needs.Advance(meal, other, 1), "Meal owner and exclusive food reservation");
                f.Needs.Cancel(meal, owner); Require(f.Inventory.GetAvailable(ResourceType.CookedFish) == 1 && f.Needs.Satiety == 30, "Interrupted meal returns food without satiety");
                meal = f.Needs.TryBeginMeal(owner);
                Require(f.Needs.Advance(meal, owner, 1) && !f.Needs.Advance(meal, owner, 1) && f.Needs.Satiety == 65 && f.Inventory.GetTotal(ResourceType.CookedFish) == 0, "Meal consumed once, restores 35");
                f.Needs.SetSatiety(0); f.Needs.Tick(10000);
                Require(f.Needs.Satiety == 0 && Mathf.Approximately(f.Needs.Efficiency, 0.65f), "Hunger clamps to zero with 65 percent efficiency");
                object token = new object(); Require(f.Inventory.Add(ResourceType.CookedFish, int.MaxValue) && f.Inventory.TryReserve(token, new ResourceCost(1, 0, 1)), "Set up output capacity boundary");
                Require(!f.Inventory.TryConvert(token, new ResourceCost(0, 0, 0, 1)) && f.Inventory.GetTotal(ResourceType.Wood) == 3 && f.Inventory.Release(token), "Output overflow leaves inputs and reservation intact");
            }
        }

        private static void TestLayout()
        {
            using (var f = new Fixture())
            {
                f.MakeGrill();
                Require(!f.Build.CanPlace(new Vector2Int(-2, 0), out _), "Cannot grow a deck solely from a blocked grill footprint");
                Require(f.Build.TryPlaceBlueprint(new Vector2Int(-2, 1), out _) && f.Build.TryPlaceBlueprint(new Vector2Int(-2, 0), out _), "Plan an accessible route around a grill");
                Require(!f.Build.CanCancel(new Vector2Int(-2, 1), out _), "Cancelling a bypass cannot leave a blueprint accessible only through a grill");
            }
            using (var f = new Fixture(true))
            { Require(!f.Build.CanPlaceGrill(Vector2Int.zero, 1, out _), "Grill cannot sever a one-tile corridor"); }
            using (var f = new Fixture())
            {
                Require(!f.Build.CanPlaceGrill(new Vector2Int(2, 0), 0, out _), "No grill on water");
                Require(!f.Build.CanPlaceGrill(new Vector2Int(-1, 1), 0, out _), "Work cell must be completed deck");
                Require(f.Build.TryPlaceGrillBlueprint(new Vector2Int(-1, 0), 1, out _), "Valid grill blueprint");
                Require(!f.Build.CanPlaceGrill(Vector2Int.zero, 0, out _), "Cannot occupy an existing grill's work cell");
                Require(f.Build.TryBeginGrill(new Vector2Int(-1, 0)), "Start grill construction");
                f.Build.IsCellOccupied = coordinate => true;
                Require(!f.Build.TryCompleteGrill(new Vector2Int(-1, 0)) && f.Inventory.GetTotal(ResourceType.Wood) == 8, "Cannot complete a grill under a crew member");
                Require(f.Build.TryCancelBlueprint(new Vector2Int(-1, 0), out _) && f.Inventory.ReservationCount == 0 && !f.Build.Cells[new Vector2Int(-1, 0)].IsBlocked,
                    "Cancel grill returns materials and leaves deck walkable");
                object hold = new object();
                Require(f.Inventory.TryReserve(hold, new ResourceCost(0, 3)) && f.Build.TryPlaceGrillBlueprint(new Vector2Int(-1, 0), 1, out _), "Reserve all but one scrap");
                f.Step();
                Require(f.Crew.Activity == CrewActivity.WaitingForMaterials, "Grill shortage reports materials even when deck would be affordable");
                f.Inventory.Release(hold);
            }
        }

        private static void TestScheduling()
        {
            using (var f = new Fixture())
            {
                f.MakeGrill();
                for (int i = 0; i < 100 && f.Crew.Activity != CrewActivity.Cooking; i++) f.Step();
                Require(f.Crew.Activity == CrewActivity.Cooking && f.Inventory.GetAvailable(ResourceType.Wood) == 3, "Crew starts recipe");
                Require(f.Build.TryPlaceBlueprint(new Vector2Int(2, 0), out _), "Add four-wood deck while last wood is reserved for cooking");
                f.Step();
                Require(f.Inventory.GetReserved(ResourceType.Fish) == 0 && f.Inventory.GetTotal(ResourceType.CookedFish) == 0, "Higher-priority construction releases cooking input");
                for (int i = 0; i < 150 && f.Build.CompletedCount < 10; i++) f.Step();
                Require(f.Build.CompletedCount == 10 && f.Inventory.GetTotal(ResourceType.Wood) == 0, "Released wood lets construction finish without deadlock");
                f.Inventory.Add(ResourceType.CookedFish, 1); f.Needs.SetSatiety(0);
                for (int i = 0; i < 50 && f.Needs.Satiety < 35; i++) f.Step();
                Require(f.Needs.Satiety == 35 && f.Inventory.GetTotal(ResourceType.CookedFish) == 0, "Crew automatically eats at zero satiety");
            }
            using (var f = new Fixture())
            {
                f.MakeGrill();
                for (int i = 0; i < 100 && f.Crew.Activity != CrewActivity.Cooking; i++) f.Step();
                f.Inventory.Add(ResourceType.CookedFish, 1); f.Needs.SetSatiety(30); f.Step();
                Require(f.Crew.Activity == CrewActivity.Eating && f.Inventory.GetReserved(ResourceType.Fish) == 0, "Eating preempts cooking and returns raw inputs");
                f.Crew.Shutdown(); Require(f.Inventory.ReservationCount == 0 && f.Inventory.GetAvailable(ResourceType.CookedFish) == 1, "Shutdown returns an unfinished meal");
            }
        }

        private static async Task Until(Func<bool> condition, float timeout, BuildManager build, CrewManager crew)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!condition())
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Timeout waiting for " + crew.Activity + " / " + crew.StatusText);
                Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Sailor stays on walkable deck"); await NextFrame();
            }
        }
        private static Task NextFrame()
        { var source = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => source.TrySetResult(true); return source.Task; }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Cooking validation failed: " + message); assertions++; }
        private static void Finish(int code)
        {
            Time.timeScale = 1; SessionState.SetBool(Key, false); EditorApplication.update -= WaitForStartup;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 0; AssetDatabase.SaveAssets();
                Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (Application.isBatchMode) EditorApplication.Exit(code);
            };
            EditorApplication.ExitPlaymode();
        }
    }
}
