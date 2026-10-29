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
using RaftGame.Fishing;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Ocean;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class FishingPreviewValidation
    {
        private const string Key = "RaftGame.ValidateFishing";
        private static bool running;

        public static void Validate()
        {
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
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
            var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            var fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address) && fishing != null && fishing.IsInitialized)
            { running = true; EditorApplication.update -= WaitForStartup; RunChecks(); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Debug.LogError("Fishing validation timed out during initialization."); Finish(1); }
        }

        private static async void RunChecks()
        {
            try
            {
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                var fishing = UnityEngine.Object.FindObjectOfType<FishingManager>();
                var crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
                var build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                var inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                var salvage = UnityEngine.Object.FindObjectOfType<SalvageManager>();
                var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
                var panel = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && fishing.RodCount == 1, "One starter rod configured by GameManager");
                Require(panel.FishText != null && panel.FishingButton != null, "Fishing controls loaded from built AA UI");
                fishing.SetAutomaticFishing(false);
                await Until(() => crew.Activity == CrewActivity.Idle, 5f, build, crew);
                int initialFish = inventory.GetTotal(ResourceType.Fish);
                fishing.SetAutomaticFishing(true);
                await Until(() => crew.Activity == CrewActivity.Fishing && crew.WorkProgress >= 0.25f, 12f, build, crew);
                Time.timeScale = 0f;
                float paused = crew.WorkProgress;
                var fields = new SerializedObject(crew.Worker.GetComponent<CrewView>());
                var rod = (GameObject)fields.FindProperty("fishingRod").objectReferenceValue;
                var tip = (Transform)fields.FindProperty("rodTip").objectReferenceValue;
                var line = (LineRenderer)fields.FindProperty("fishingLine").objectReferenceValue;
                var bobber = (Transform)fields.FindProperty("bobber").objectReferenceValue;
                Require(rod.activeInHierarchy && line.gameObject.activeInHierarchy && bobber.gameObject.activeInHierarchy, "Rod, line and float are visible while fishing");
                Require(!build.Cells.ContainsKey(build.WorldToCell(bobber.position)) && (line.GetPosition(0) - tip.position).sqrMagnitude < 0.001f,
                    "Fishing line connects the hand-held tip to open water");
                Vector3 bobberAtPause = bobber.position;
                double stop = EditorApplication.timeSinceStartup + 0.25;
                while (EditorApplication.timeSinceStartup < stop) await NextFrame();
                Require(Mathf.Approximately(paused, crew.WorkProgress) && (bobber.position - bobberAtPause).sqrMagnitude < 0.00001f,
                    "Pause freezes the catch and bobber");
                Directory.CreateDirectory("Docs/Previews");
                RaftPreviewValidation.Capture("Docs/Previews/Fishing_Casting.png", Camera.main, ui.GetComponent<Canvas>(), panel);
                Require(inventory.GetTotal(ResourceType.Fish) == initialFish, "Partial fishing does not award fish");
                Time.timeScale = 1f;
                build.TryPlaceBlueprint(new Vector2Int(2, 0), out _);
                await Until(() => crew.Activity == CrewActivity.Constructing, 8f, build, crew);
                Require(inventory.GetTotal(ResourceType.Fish) == initialFish && fishing.AvailableRods == 1 && !rod.activeSelf,
                    "Construction preempts fishing without catch credit or rod leak");
                await Until(() => build.CompletedCount == 10, 8f, build, crew);
                await Until(() => inventory.GetTotal(ResourceType.Fish) == initialFish + 1, 13f, build, crew);
                Require(inventory.GetTotal(ResourceType.Wood) == 4 && inventory.GetTotal(ResourceType.Scrap) == 1,
                    "Fish is a separate inventory resource; fishing consumes no construction materials");
                Require(panel.FishText.text.StartsWith("鲜鱼 " + (initialFish + 1)), "MVC fish count refreshes");
                await Until(() => crew.Activity == CrewActivity.Fishing && crew.WorkProgress >= 0.1f, 10f, build, crew);
                int beforeSalvage = inventory.GetTotal(ResourceType.Fish);
                FloatingResource item = salvage.Spawn(ResourceType.Wood, new Vector3(-4.1f, 0, 1.8f));
                salvage.SetMarked(item, true);
                await Until(() => crew.Activity == CrewActivity.Gathering, 10f, build, crew);
                Require(fishing.AvailableRods == 1 && inventory.GetTotal(ResourceType.Fish) == beforeSalvage,
                    "Marked salvage preempts a partial cast");
                await Until(() => inventory.GetTotal(ResourceType.Wood) == 6, 5f, build, crew);
                await Until(() => crew.Activity == CrewActivity.Fishing && crew.WorkProgress >= 0.1f, 10f, build, crew);
                RaftPreviewValidation.Capture("Docs/Previews/Fishing_Caught.png", Camera.main, ui.GetComponent<Canvas>(), panel);
                for (int i = 0; i < 3; i++) { ui.Close(UICommon_Build.Address); await ui.OpenAsync(UICommon_Build.Address); }
                panel = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                panel.FishingButton.onClick.Invoke();
                crew.Tick(0f);
                Require(!fishing.AutomaticFishing && fishing.AvailableRods == 1 && !rod.activeSelf,
                    "Cached UI toggle binds once and stows the rod");
                game.enabled = false;
                TestOwnershipAndInventory();
                TestTravelInterruption();
                TestNoRod();
                File.WriteAllText("Docs/Previews/FishingValidation.txt",
                    "PASS: actual Play mode + built AA UI; one starter rod; deck-only travel to clear water; visible rod/line/bobber; " +
                    "pause; partial catches give no fish; construction and marked salvage preemption; automatic resume; fish-only inventory credit; " +
                    "cache-safe UI toggle; exclusive rod ownership; casting position checks; planned-water invalidation; catch-once and reentrant protection; " +
                    "fish reservation invariants; interrupted walking recovery; no-rod behavior.\n");
                Debug.Log("FISHING_VALIDATION_OK");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static async Task Until(Func<bool> condition, float timeout, BuildManager build, CrewManager crew)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!condition())
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out waiting for fishing/task transition");
                Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Sailor remains on finished deck");
                await NextFrame();
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly BuildManager Build;
            public readonly ResourceManager Inventory;
            public readonly FishingManager Fishing;
            public readonly CrewManager Crew;
            private readonly FishingConfig config;
            private readonly CrewConfig crewConfig;

            public Fixture(int rods = 1)
            {
                Root = new GameObject("Fishing Test Fixture");
                var resourceConfig = ScriptableObject.CreateInstance<ResourceConfig>();
                Inventory = Root.AddComponent<ResourceManager>();
                Require(Inventory.Initialize(resourceConfig), "Fixture inventory initialized");
                UnityEngine.Object.Destroy(resourceConfig);
                Transform deck = new GameObject("Deck").transform; deck.SetParent(Root.transform);
                for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                {
                    DeckTile tile = new GameObject("Tile").AddComponent<DeckTile>();
                    tile.transform.SetParent(deck); tile.Coordinate = new Vector2Int(x, z);
                    tile.transform.position = new Vector3(x * 2, 0.48f, z * 2);
                }
                Build = Root.AddComponent<BuildManager>();
                var fields = new SerializedObject(Build); fields.FindProperty("raftRoot").objectReferenceValue = deck; fields.ApplyModifiedPropertiesWithoutUndo();
                Require(Build.Initialize(AssetDatabase.LoadAssetAtPath<BuildConfig>("Assets/Configs/Build_Default.asset"), Camera.main, Inventory), "Fixture build initialized");
                config = ScriptableObject.CreateInstance<FishingConfig>(); config.StartingRods = rods; config.CatchDuration = 0.4f;
                Fishing = Root.AddComponent<FishingManager>();
                Require(Fishing.Initialize(config, Build, Inventory, UnityEngine.Object.FindObjectOfType<OceanManager>()), "Fixture fishing initialized");
                crewConfig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CrewConfig>("Assets/Configs/Crew_Default.asset"));
                crewConfig.WalkSpeed = 6f; crewConfig.ConstructionDuration = 0.3f;
                Crew = Root.AddComponent<CrewManager>();
                Require(Crew.Initialize(crewConfig, Build, Camera.main, null, Fishing), "Fixture crew initialized");
            }

            public void Step()
            {
                Fishing.ProcessInput(); Crew.Tick(0.05f);
                Require(DeckPathfinder.IsWalkable(Build.Cells, Build.WorldToCell(Crew.Worker.position)), "Fixture crew stays on deck");
            }
            public void Dispose()
            {
                Crew.Shutdown(); Fishing.Shutdown(); Build.Shutdown(); Inventory.Shutdown();
                UnityEngine.Object.Destroy(config); UnityEngine.Object.Destroy(crewConfig); UnityEngine.Object.Destroy(Root);
            }
        }

        private static void TestOwnershipAndInventory()
        {
            using (var f = new Fixture())
            {
                DeckPathfinder.FindReachable(f.Build.Cells, Vector2Int.zero, out _, out var distances);
                Require(f.Fishing.TryFindSpot(distances, out FishingSpot spot) && distances.ContainsKey(spot.Cell), "Reachable edge spot selected");
                object owner = new object(), other = new object();
                FishingSession session = f.Fishing.TryAcquire(owner, spot);
                Require(session != null && f.Fishing.TryAcquire(other, spot) == null, "Single rod cannot be double-booked");
                Require(!f.Fishing.Advance(session, other, spot.Cell, 1f, out _) &&
                    !f.Fishing.Advance(session, owner, Vector2Int.zero, 1f, out _), "Owner and standing position validated");
                Require(f.Fishing.Advance(session, owner, spot.Cell, 0f, out _) && session.Progress == 0f, "Zero timestep gives no catch progress");
                f.Build.TryPlaceBlueprint(spot.Cell + spot.Direction, out _);
                Require(!f.Fishing.IsSessionValid(session, owner) && !f.Fishing.Cancel(session, other), "Planned deck blocks the casting water");
                f.Fishing.ProcessInput();
                Require(f.Fishing.AvailableRods == 1 && f.Inventory.GetTotal(ResourceType.Fish) == 0, "Invalidated cast returns the rod without fish");
                f.Build.TryCancelBlueprint(spot.Cell + spot.Direction, out _);
                session = f.Fishing.TryAcquire(owner, spot);
                bool duplicate = false;
                Action callback = () => duplicate |= f.Fishing.Advance(session, owner, spot.Cell, 1f, out _);
                f.Inventory.Changed += callback;
                Require(f.Fishing.Advance(session, owner, spot.Cell, 1f, out bool caught) && caught && !duplicate, "Catch credits once before callbacks");
                f.Inventory.Changed -= callback;
                Require(!f.Fishing.Advance(session, owner, spot.Cell, 1f, out _) && f.Inventory.GetTotal(ResourceType.Fish) == 1 &&
                    f.Inventory.GetTotal(ResourceType.Scrap) == 2 && f.Inventory.GetTotal(ResourceType.Wood) == 8, "Fish does not become scrap or credit twice");
                object reservation = new object();
                Require(f.Inventory.TryReserve(reservation, new ResourceCost(0, 0, 1)) && f.Inventory.GetAvailable(ResourceType.Fish) == 0 &&
                    f.Inventory.Release(reservation) && f.Inventory.GetAvailable(ResourceType.Fish) == 1, "Fish participates in inventory reservations");
                Require(f.Inventory.TryReserve(reservation, new ResourceCost(0, 0, 1)) && f.Inventory.Commit(reservation) &&
                    f.Inventory.GetTotal(ResourceType.Fish) == 0 && !f.Inventory.TryReserve(other, new ResourceCost(0, 0, -1)), "Fish commit and validation invariants");
            }
        }

        private static void TestTravelInterruption()
        {
            using (var f = new Fixture())
            {
                f.Step(); f.Step();
                Require(f.Crew.Activity == CrewActivity.Walking && f.Fishing.AvailableRods == 0, "Rod reserved while travelling to the edge");
                f.Build.TryPlaceBlueprint(new Vector2Int(2, 0), out _);
                for (int i = 0; i < 150 && f.Build.CompletedCount < 10; i++) f.Step();
                Require(f.Build.CompletedCount == 10 && f.Inventory.GetTotal(ResourceType.Fish) == 0 && f.Fishing.AvailableRods == 1,
                    "Interrupted fishing walk returns safely before construction");
                for (int i = 0; i < 100 && f.Crew.Activity != CrewActivity.Fishing; i++) f.Step();
                Require(f.Crew.Activity == CrewActivity.Fishing, "Fishing resumes after high-priority work");
                f.Crew.Shutdown();
                Require(f.Fishing.AvailableRods == 1, "Crew shutdown returns its rod");
            }
        }

        private static void TestNoRod()
        {
            using (var f = new Fixture(0))
            {
                for (int i = 0; i < 30; i++) f.Step();
                Require(f.Crew.Activity == CrewActivity.Idle && f.Inventory.GetTotal(ResourceType.Fish) == 0 && f.Fishing.RodCount == 0,
                    "No rod means no fishing or free catches");
            }
        }

        private static Task NextFrame()
        {
            var source = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => source.TrySetResult(true); return source.Task;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Fishing validation failed: " + message); }
        private static void Finish(int code)
        {
            Time.timeScale = 1f; SessionState.SetBool(Key, false); EditorApplication.update -= WaitForStartup;
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
