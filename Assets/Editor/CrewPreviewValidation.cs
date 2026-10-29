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
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class CrewPreviewValidation
    {
        private const string Key = "RaftGame.ValidateCrew";
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
            UIManager ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            CrewManager crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address) && crew != null && crew.IsInitialized)
            {
                running = true;
                EditorApplication.update -= WaitForStartup;
                RunChecks();
            }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Debug.LogError("Crew test timed out waiting for initialization/AA panel."); Finish(1); }
        }

        private static async void RunChecks()
        {
            try
            {
                var game = UnityEngine.Object.FindObjectOfType<GameManager>();
                UnityEngine.Object.FindObjectOfType<RaftGame.Fishing.FishingManager>()?.SetAutomaticFishing(false);
                var build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                var crew = UnityEngine.Object.FindObjectOfType<CrewManager>();
                var ui = UnityEngine.Object.FindObjectOfType<UIManager>();
                var panel = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && DeckPathfinder.IsWalkable(build.Cells, crew.CurrentCell), "GameManager initializes one sailor on finished deck");
                Require(UnityEngine.Object.FindObjectsOfType<CrewView>().Length == 1, "Exactly one sailor");
                Vector2Int first = new Vector2Int(2, 1), second = new Vector2Int(3, 1);
                Require(build.TryPlaceBlueprint(first, out _), "Place first construction task");
                await Until(() => crew.Activity == CrewActivity.Constructing && crew.WorkProgress >= 0.35f, 15f, build, crew);
                Require(crew.CurrentCell != first && build.Cells[first].State == DeckState.Constructing,
                    "Sailor works from the neighboring finished tile");
                Require(panel.CrewText != null && panel.CrewText.text.Contains("施工"), "AA-loaded MVC panel shows crew activity");
                Time.timeScale = 0f;
                float progress = crew.WorkProgress;
                Vector3 position = crew.Worker.position;
                Require(build.TryPlaceBlueprint(second, out _), "Planning remains available during pause");
                double pauseUntil = EditorApplication.timeSinceStartup + 0.3;
                while (EditorApplication.timeSinceStartup < pauseUntil) await NextFrame();
                Require(Mathf.Approximately(progress, crew.WorkProgress) && Vector3.Distance(position, crew.Worker.position) < 0.001f,
                    "Pause freezes locomotion and construction");
                Directory.CreateDirectory("Docs/Previews");
                RaftPreviewValidation.Capture("Docs/Previews/Crew_Construction.png", Camera.main, ui.GetComponent<Canvas>(), panel);
                Time.timeScale = 1f;
                await Until(() => build.CompletedCount == 11, 20f, build, crew);
                Require(build.Cells[first].State == DeckState.Complete && build.Cells[second].State == DeckState.Complete,
                    "Continuous blueprints complete in reachable order");
                Require(build.BlueprintCount == 0 && panel.CompletedText.text.Contains("11"), "Counts refresh after automatic completion");
                RaftPreviewValidation.Capture("Docs/Previews/Crew_Completed.png", Camera.main, ui.GetComponent<Canvas>(), panel);

                // Deterministic edge cases are isolated from the live GameManager tick.
                game.enabled = false;
                TestDetour();
                TestUnreachableAndRecovery();
                TestCancellation();
                TestNegativeChain();
                File.WriteAllText("Docs/Previews/CrewValidation.txt",
                    "PASS: actual Play mode + AA bundle UI; one configured sailor; automatic travel/work/completion; " +
                    "paused movement/progress with continued blueprint planning; chained expansion; MVC status/counts; " +
                    "deck-only detour routing; disconnected task rejection/retry; cancellation while walking and working; " +
                    "replacement task identity; negative-coordinate chain; preserved interrupted progress.\n");
                Debug.Log("CREW_VALIDATION_OK");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static async Task Until(Func<bool> condition, float timeout, BuildManager build, CrewManager crew)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!condition())
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out waiting for automatic construction");
                Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Live sailor never enters unbuilt sea cells");
                await NextFrame();
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly BuildManager Build;
            public readonly CrewManager Crew;
            private readonly CrewConfig config;

            public Fixture(params Vector2Int[] completeCells)
            {
                Root = new GameObject("Crew Validation Fixture");
                Transform deck = new GameObject("Deck").transform;
                deck.SetParent(Root.transform);
                foreach (Vector2Int cell in completeCells)
                {
                    var tile = new GameObject("Deck").AddComponent<DeckTile>();
                    tile.transform.SetParent(deck);
                    tile.Coordinate = cell;
                    tile.transform.position = new Vector3(cell.x * 2f, 0.48f, cell.y * 2f);
                }
                Build = Root.AddComponent<BuildManager>();
                var serialized = new SerializedObject(Build);
                serialized.FindProperty("raftRoot").objectReferenceValue = deck;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Require(Build.Initialize(AssetDatabase.LoadAssetAtPath<BuildConfig>("Assets/Configs/Build_Default.asset"), Camera.main), "Fixture build initialization");
                config = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CrewConfig>("Assets/Configs/Crew_Default.asset"));
                config.WalkSpeed = 8f;
                config.ConstructionDuration = 0.4f;
                Crew = Root.AddComponent<CrewManager>();
                Require(Crew.Initialize(config, Build, Camera.main), "Fixture crew initialization");
            }

            public void Step(int count = 1)
            {
                for (int i = 0; i < count; i++)
                {
                    Crew.Tick(0.025f);
                    Require(DeckPathfinder.IsWalkable(Build.Cells, Build.WorldToCell(Crew.Worker.position)), "Fixture sailor stays on finished deck");
                }
            }

            public void FinishAll(int expectedCount)
            {
                for (int i = 0; i < 1000 && Build.CompletedCount < expectedCount; i++) Step();
                Require(Build.CompletedCount == expectedCount, "Reachable tasks eventually finish");
            }

            public void Dispose()
            {
                Crew.Shutdown();
                Build.Shutdown();
                UnityEngine.Object.Destroy(Root);
                UnityEngine.Object.Destroy(config);
            }
        }

        private static void TestDetour()
        {
            using (var f = new Fixture(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(0, 2)))
            {
                DeckPathfinder.FindReachable(f.Build.Cells, Vector2Int.zero, out var parents, out var distance);
                Require(distance[new Vector2Int(0, 2)] == 4 && !parents.ContainsKey(new Vector2Int(0, 1)), "BFS detours around the sea gap");
                f.Build.TryPlaceBlueprint(new Vector2Int(-1, 2), out _);
                f.FinishAll(6);
            }
        }

        private static void TestUnreachableAndRecovery()
        {
            using (var f = new Fixture(Vector2Int.zero, new Vector2Int(8, 0)))
            {
                f.Build.TryPlaceBlueprint(new Vector2Int(9, 0), out _);
                Vector3 start = f.Crew.Worker.position;
                f.Step(40);
                Require(f.Crew.Activity == CrewActivity.WaitingForAccess && f.Crew.Worker.position == start, "Unreachable island task is not taken");
                f.Build.TryPlaceBlueprint(new Vector2Int(1, 0), out _);
                f.FinishAll(3);
                Require(f.Build.Cells[new Vector2Int(9, 0)].State == DeckState.Planned, "Unreachable task cannot steal reachable work");
            }
        }

        private static void TestCancellation()
        {
            using (var f = new Fixture(Vector2Int.zero, new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0)))
            {
                Vector2Int far = new Vector2Int(4, 0), near = new Vector2Int(0, 1);
                f.Build.TryPlaceBlueprint(far, out _);
                f.Step(3);
                Require(f.Crew.Activity == CrewActivity.Walking, "Walking task acquired");
                Require(f.Build.TryCancelBlueprint(far, out _), "Cancel walking target");
                f.Build.TryPlaceBlueprint(near, out _);
                for (int i = 0; i < 100 && f.Crew.Activity != CrewActivity.Constructing; i++) f.Step();
                Require(f.Crew.TaskCell == near && f.Crew.Activity == CrewActivity.Constructing, "Cancelled route returns safely and selects new work");
                Require(f.Build.TryCancelBlueprint(near, out _), "Cancel active construction");
                f.Step(5);
                Require(f.Build.CompletedCount == 4 && f.Crew.TaskCell == null, "Cancelled construction never completes");
                f.Build.TryPlaceBlueprint(near, out _);
                f.Step(); // Select the replacement object at the same coordinate.
                f.Step(3);
                Require(f.Crew.WorkProgress > 0f && f.Crew.WorkProgress < 1f, "Replacement blueprint starts its own progress");
                float preserved = f.Crew.WorkProgress;
                Require(f.Build.TrySuspendConstruction(near), "Suspend interrupted work");
                f.Step(2);
                Require(f.Crew.WorkProgress >= preserved, "Interrupted progress survives task reassignment");
                f.FinishAll(5);
            }
        }

        private static void TestNegativeChain()
        {
            using (var f = new Fixture(Vector2Int.zero))
            {
                f.Build.TryPlaceBlueprint(new Vector2Int(-1, 0), out _);
                f.Build.TryPlaceBlueprint(new Vector2Int(-2, 0), out _);
                f.Build.TryPlaceBlueprint(new Vector2Int(-3, 0), out _);
                f.FinishAll(4);
            }
        }

        private static Task NextFrame()
        {
            var source = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () => source.TrySetResult(true);
            return source.Task;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Crew validation failed: " + message);
        }

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
