using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using RaftGame.Core;
using RaftGame.Crew;
using RaftGame.Building;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Ocean;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class SalvagePreviewValidation
    {
        private const string Key = "RaftGame.ValidateSalvage";
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
            var salvage = UnityEngine.Object.FindObjectOfType<SalvageManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address) && salvage != null && salvage.IsInitialized)
            {
                running = true;
                EditorApplication.update -= WaitForStartup;
                RunChecks();
            }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Debug.LogError("Salvage validation timed out waiting for initialization."); Finish(1); }
        }

        private static async void RunChecks()
        {
            try
            {
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                UnityEngine.Object.FindObjectOfType<RaftGame.Fishing.FishingManager>()?.SetAutomaticFishing(false);
                var resources = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                var salvage = UnityEngine.Object.FindObjectOfType<SalvageManager>();
                var build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                var crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
                var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
                var panel = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && resources.GetTotal(ResourceType.Wood) == 8 && resources.GetTotal(ResourceType.Scrap) == 2,
                    "Configured initial inventory");
                Require(panel.WoodText != null && panel.ScrapText != null && panel.CostText != null, "Inventory and cost labels loaded via real AA bundles");
                Require(salvage.Items.Count >= 6 && salvage.Items.Count <= 18, "Continuous floating supply starts populated");
                FloatingResource drifting = salvage.Items[0];
                Vector3 start = drifting.transform.position;
                await Until(() => (drifting.transform.position - start).sqrMagnitude > 0.001f, 2f, build, crew, resources);
                foreach (int x in new[] { 2, 3, 4 }) Require(build.TryPlaceBlueprint(new Vector2Int(x, 0), out _), "Place a chain without charging materials");
                await Until(() => build.CompletedCount == 11 && crew.Activity == CrewActivity.WaitingForMaterials, 22f, build, crew, resources);
                Require(resources.GetTotal(ResourceType.Wood) == 0 && resources.GetTotal(ResourceType.Scrap) == 0 && resources.ReservationCount == 0,
                    "Exactly two initial tiles consume the starting budget");
                Require(build.Cells[new Vector2Int(4, 0)].State == DeckState.Planned, "Third tile waits for resources");

                var woodA = salvage.Spawn(ResourceType.Wood, new Vector3(-4.1f, 0, 1.8f));
                var scrap = salvage.Spawn(ResourceType.Scrap, new Vector3(-4.1f, 0, 0.8f));
                var woodB = salvage.Spawn(ResourceType.Wood, new Vector3(-4.1f, 0, 2.8f));
                Require(woodA != null && woodB != null && scrap != null, "Supply can spawn within the configured cap");
                salvage.SetMarked(woodA, true); salvage.SetMarked(scrap, true); salvage.SetMarked(woodB, true);
                await Until(() => crew.Activity == CrewActivity.Gathering && crew.WorkProgress > 0.2f, 12f, build, crew, resources);
                Time.timeScale = 0f;
                float pausedProgress = crew.WorkProgress;
                Vector3 pausedPosition = woodB.transform.position;
                double pauseEnd = EditorApplication.timeSinceStartup + 0.25;
                while (EditorApplication.timeSinceStartup < pauseEnd) await NextFrame();
                Require(Mathf.Approximately(pausedProgress, crew.WorkProgress) && (woodB.transform.position - pausedPosition).sqrMagnitude < 0.00001f,
                    "Pause freezes drifting and gathering");
                Directory.CreateDirectory("Docs/Previews");
                RaftPreviewValidation.Capture("Docs/Previews/Salvage_Gathering.png", Camera.main, ui.GetComponent<Canvas>(), panel);
                Time.timeScale = 1f;
                await Until(() => build.Cells[new Vector2Int(4, 0)].State == DeckState.Constructing, 22f, build, crew, resources);
                Require(resources.GetTotal(ResourceType.Wood) == 4 && resources.GetAvailable(ResourceType.Wood) == 0 &&
                    resources.GetReserved(ResourceType.Wood) == 4 && resources.GetReserved(ResourceType.Scrap) == 1,
                    "Gathered resources are reserved on construction start, not spent early");
                Time.timeScale = 0f;
                RaftPreviewValidation.Capture("Docs/Previews/Salvage_Reserved.png", Camera.main, ui.GetComponent<Canvas>(), panel);
                Require(build.TryCancelBlueprint(new Vector2Int(4, 0), out _), "Cancel paid construction");
                Require(resources.GetAvailable(ResourceType.Wood) == 4 && resources.GetAvailable(ResourceType.Scrap) == 1 && resources.ReservationCount == 0,
                    "Cancellation releases the entire reservation");
                Require(build.TryPlaceBlueprint(new Vector2Int(4, 0), out _), "Replace cancelled blueprint");
                Time.timeScale = 1f;
                await Until(() => build.CompletedCount == 12, 10f, build, crew, resources);
                Require(resources.GetTotal(ResourceType.Wood) == 0 && resources.GetTotal(ResourceType.Scrap) == 0 && resources.ReservationCount == 0,
                    "Replacement tile commits its cost exactly once");
                RaftPreviewValidation.Capture("Docs/Previews/Salvage_Completed.png", Camera.main, ui.GetComponent<Canvas>(), panel);

                game.enabled = false;
                TestLedger();
                TestBlueprintReservations();
                TestCollectionOwnershipAndRange();
                File.WriteAllText("Docs/Previews/SalvageValidation.txt",
                    "PASS: actual Play mode + built AA UI; sea-current drift; material-limited construction; marked gathering from deck; " +
                    "paused drift/gathering; gathering unblocks construction; reserve/available/total invariants; cancel/requeue/commit-once; " +
                    "negative/overflow rejection; reservation exclusivity; suspension releases material; owner/range validation; " +
                    "reentrant double-collection prevention; current-position raycast marking and tool isolation; far marks; unmark while walking; out-of-range gathering recovery.\n");
                Debug.Log("SALVAGE_VALIDATION_OK");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static async Task Until(Func<bool> condition, float timeout, BuildManager build, CrewManager crew, ResourceManager resources)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!condition())
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out waiting for the salvage/build loop");
                Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Worker never enters water");
                CheckInvariant(resources);
                await NextFrame();
            }
        }

        private static void CheckInvariant(ResourceManager inventory)
        {
            foreach (ResourceType type in new[] { ResourceType.Wood, ResourceType.Scrap })
                Require(inventory.GetTotal(type) >= 0 && inventory.GetReserved(type) >= 0 && inventory.GetAvailable(type) >= 0 &&
                    inventory.GetTotal(type) == inventory.GetAvailable(type) + inventory.GetReserved(type), "Inventory invariant");
        }

        private static ResourceManager CreateInventory(GameObject owner, int wood, int scrap)
        {
            var config = ScriptableObject.CreateInstance<ResourceConfig>();
            config.StartingInventory = new ResourceCost(wood, scrap);
            var inventory = owner.AddComponent<ResourceManager>();
            Require(inventory.Initialize(config), "Inventory fixture initialization");
            UnityEngine.Object.Destroy(config);
            return inventory;
        }

        private static void TestLedger()
        {
            var owner = new GameObject("Ledger Test");
            var r = CreateInventory(owner, 10, 3);
            object a = new object(), b = new object();
            Require(r.TryReserve(a, new ResourceCost(4, 1)) && r.TryReserve(b, new ResourceCost(6, 2)), "Exclusive reservations");
            Require(!r.TryReserve(a, new ResourceCost(0, 0)) && !r.TryReserve(new object(), new ResourceCost(1, 0)), "No duplicate/overbooked reservations");
            Require(r.Commit(a) && !r.Commit(a) && r.GetTotal(ResourceType.Wood) == 6, "Commit once");
            Require(r.Release(b) && !r.Release(b) && r.GetAvailable(ResourceType.Wood) == 6, "Release once");
            Require(!r.TryReserve(new object(), new ResourceCost(-1, 0)) && !r.Add(ResourceType.Wood, -3) &&
                !r.Add(ResourceType.Wood, int.MaxValue), "Reject negative amounts and overflow");
            CheckInvariant(r);
            r.Shutdown(); UnityEngine.Object.Destroy(owner);
        }

        private sealed class Fixture : IDisposable
        {
            public GameObject Root;
            public BuildManager Build;
            public ResourceManager Resources;
            public SalvageManager Salvage;
            public CrewManager Crew;
            private SalvageConfig salvageConfig;
            private CrewConfig crewConfig;

            public Fixture(int wood, int scrap)
            {
                Root = new GameObject("Salvage Test Fixture");
                Resources = CreateInventory(Root, wood, scrap);
                Transform deck = new GameObject("Deck").transform;
                deck.SetParent(Root.transform);
                for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                {
                    DeckTile tile = new GameObject("Tile").AddComponent<DeckTile>();
                    tile.Coordinate = new Vector2Int(x, z); tile.transform.SetParent(deck);
                    tile.transform.position = new Vector3(x * 2f, 0.48f, z * 2f);
                }
                Build = Root.AddComponent<BuildManager>();
                var serialized = new SerializedObject(Build);
                serialized.FindProperty("raftRoot").objectReferenceValue = deck;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Require(Build.Initialize(AssetDatabase.LoadAssetAtPath<BuildConfig>("Assets/Configs/Build_Default.asset"), Camera.main, Resources), "Build fixture initialized");
                salvageConfig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SalvageConfig>("Assets/Configs/Salvage_Default.asset"));
                salvageConfig.DriftSpeed = 0f; salvageConfig.SpawnInterval = 100000; salvageConfig.Lifetime = 1000; salvageConfig.MaximumItems = 50;
                Salvage = Root.AddComponent<SalvageManager>();
                Require(Salvage.Initialize(salvageConfig, Build, UnityEngine.Object.FindObjectOfType<OceanManager>(), Resources, Camera.main), "Salvage fixture initialized");
                while (Salvage.Items.Count > 0) Salvage.Remove(Salvage.Items[0]);
                crewConfig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CrewConfig>("Assets/Configs/Crew_Default.asset"));
                crewConfig.WalkSpeed = 6f; crewConfig.ConstructionDuration = 0.4f;
                Crew = Root.AddComponent<CrewManager>();
                Require(Crew.Initialize(crewConfig, Build, Camera.main, Salvage), "Crew fixture initialized");
            }

            public void Step(int count = 1)
            {
                for (int i = 0; i < count; i++)
                {
                    Salvage.Tick(0.05f); Crew.Tick(0.05f);
                    Require(DeckPathfinder.IsWalkable(Build.Cells, Build.WorldToCell(Crew.Worker.position)), "Fixture crew stays on deck");
                    CheckInvariant(Resources);
                }
            }

            public void Dispose()
            {
                Crew.Shutdown(); Salvage.Shutdown(); Build.Shutdown(); Resources.Shutdown();
                UnityEngine.Object.Destroy(Root); UnityEngine.Object.Destroy(salvageConfig); UnityEngine.Object.Destroy(crewConfig);
            }
        }

        private static void TestBlueprintReservations()
        {
            using (var f = new Fixture(4, 1))
            {
                Vector2Int p = new Vector2Int(2, 0);
                f.Build.TryPlaceBlueprint(p, out _);
                DeckCell old = f.Build.Cells[p];
                Require(f.Resources.ReservationCount == 0 && f.Build.TryBeginConstruction(p) && !f.Build.TryBeginConstruction(p), "Reserve only at start");
                f.Build.SetConstructionProgress(p, 0.4f);
                Require(f.Build.TrySuspendConstruction(p) && f.Resources.GetAvailable(ResourceType.Wood) == 4, "Suspend releases reserved resources");
                Require(f.Build.TryBeginConstruction(p) && f.Build.TryCancelBlueprint(p, out _) && f.Resources.ReservationCount == 0, "Cancel active reservation");
                f.Build.TryPlaceBlueprint(p, out _);
                Require(!f.Resources.Commit(old) && f.Build.TryBeginConstruction(p) && f.Build.TryCompleteConstruction(p) && !f.Build.TryCompleteConstruction(p), "New blueprint owns a distinct single-use reservation");
                Require(f.Resources.GetTotal(ResourceType.Wood) == 0 && f.Resources.GetTotal(ResourceType.Scrap) == 0, "Completed tile consumes full cost once");
                f.Build.TryPlaceBlueprint(new Vector2Int(3, 0), out _);
                Require(!f.Build.TryBeginConstruction(new Vector2Int(3, 0)), "Direct build API cannot bypass missing materials");
            }
        }

        private static void TestCollectionOwnershipAndRange()
        {
            using (var f = new Fixture(0, 0))
            {
                object owner = new object();
                FloatingResource item = f.Salvage.Spawn(ResourceType.Wood, new Vector3(4.1f, 0, 0));
                Require(!f.Salvage.TryClaim(item, owner), "Unmarked items are not tasks");
                f.Salvage.SetMarked(item, true);
                Require(f.Salvage.TryClaim(item, owner) && !f.Salvage.TryClaim(item, new object()), "One owner per item");
                Require(!f.Salvage.TryCollect(item, new object(), new Vector2Int(1, 0)) && !f.Salvage.TryCollect(item, owner, Vector2Int.zero), "Wrong owner/interior collection rejected");
                bool duplicate = false;
                Action callback = () => duplicate |= f.Salvage.TryCollect(item, owner, new Vector2Int(1, 0));
                f.Resources.Changed += callback;
                Require(f.Salvage.TryCollect(item, owner, new Vector2Int(1, 0)) && !duplicate, "Reentrant credit blocked");
                f.Resources.Changed -= callback;
                Require(!f.Salvage.TryCollect(item, owner, new Vector2Int(1, 0)) && f.Resources.GetTotal(ResourceType.Wood) == 2, "Item credited exactly once");
                FloatingResource far = f.Salvage.Spawn(ResourceType.Wood, new Vector3(-14, 0, 11));
                Ray click = new Ray(new Vector3(-14, 10, 11), Vector3.down);
                Require(f.Salvage.TryToggleMarkAt(click) && far.IsMarked && f.Salvage.TryToggleMarkAt(click) && !far.IsMarked,
                    "Raycast toggles the resource at its current transform");
                f.Build.SetTool(BuildTool.DeckBlueprint);
                Require(!f.Salvage.TryToggleMarkAt(click), "Build tool does not also mark salvage");
                f.Build.SetTool(BuildTool.None);
                f.Salvage.SetMarked(far, true); f.Step(25);
                Require(f.Crew.Activity == CrewActivity.WaitingForSalvage && f.Crew.CurrentCell == Vector2Int.zero, "Far mark never sends sailor into water");
                FloatingResource near = f.Salvage.Spawn(ResourceType.Scrap, new Vector3(4.1f, 0, 0));
                f.Salvage.SetMarked(near, true);
                for (int i = 0; i < 150 && f.Resources.GetTotal(ResourceType.Scrap) < 1; i++) f.Step();
                Require(f.Resources.GetTotal(ResourceType.Scrap) == 1, "Reachable mark is gathered despite the far mark");
                FloatingResource walking = f.Salvage.Spawn(ResourceType.Wood, new Vector3(-4.1f, 0, 2));
                f.Salvage.SetMarked(walking, true); f.Step(2);
                Require(f.Crew.Activity == CrewActivity.Walking, "Travel to salvage point");
                f.Salvage.SetMarked(walking, false); f.Step(30);
                Require(f.Resources.GetTotal(ResourceType.Wood) == 2, "Unmark cancels the walking task without credit");
                FloatingResource driftingAway = f.Salvage.Spawn(ResourceType.Wood, new Vector3(4.1f, 0, 0));
                f.Salvage.SetMarked(driftingAway, true);
                for (int i = 0; i < 100 && f.Crew.Activity != CrewActivity.Gathering; i++) f.Step();
                Require(f.Crew.Activity == CrewActivity.Gathering, "Start gathering in reach");
                driftingAway.transform.position = new Vector3(20, 0, 20); f.Step(30);
                Require(f.Resources.GetTotal(ResourceType.Wood) == 2 && f.Crew.Activity == CrewActivity.WaitingForSalvage, "Moving target leaves reach safely");
            }
        }

        private static Task NextFrame()
        {
            var source = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () => source.TrySetResult(true);
            return source.Task;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Salvage validation failed: " + message); }

        private static void Finish(int code)
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
                if (Application.isBatchMode) EditorApplication.Exit(code);
            };
            EditorApplication.ExitPlaymode();
        }
    }
}
