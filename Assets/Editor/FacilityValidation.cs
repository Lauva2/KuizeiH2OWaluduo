using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using RaftGame.Core;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Production;
using RaftGame.Fishing;
using RaftGame.Needs;
using RaftGame.UI;

namespace RaftGame.Editor
{
    public static class FacilityValidation
    {
        private const string Key = "RaftGame.ValidateFacilities";
        private static int assertions;
        private static GameManager game;
        private static BuildManager build;
        private static CrewManager crew;
        private static ResourceManager inventory;
        private static FacilityManager facilities;
        private static ProductionManager food;
        private static WaterManager water;
        private static UIManager ui;
        private static UICommon_Facility panel;
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
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) { Debug.LogError("Facility validation initialization timeout"); Finish(1); }
        }
        private static async void Run()
        {
            try
            {
                game = UnityEngine.Object.FindObjectOfType<GameManager>(); build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                crew = UnityEngine.Object.FindObjectOfType<CrewManager>(); inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                facilities = game.Facilities; food = UnityEngine.Object.FindObjectOfType<ProductionManager>(); water = game.Water;
                Require(game.IsInitialized && facilities != null && facilities.IsInitialized, "GameManager initializes the facility manager");
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(FacilitySceneBuilder.PanelPath));
                Require(entry != null && entry.address == UICommon_Facility.Address && entry.parentGroup.Name == "UI", "Panel is an AA asset named UICommon_Facility in group UI");
                game.TimeControl.SetPaused(true); game.enabled = false;
                UnityEngine.Object.FindObjectOfType<FishingManager>().SetAutomaticFishing(false);
                Reset();
                var grill = Complete(new Vector2Int(-1, 0), 0, FacilityKind.Grill);
                var purifier = Complete(new Vector2Int(1, 0), 0, FacilityKind.Purifier);
                Vector3 point = build.CellToWorld(purifier.Coordinate) + Vector3.up * .8f;
                Require(facilities.TrySelectAt(Camera.main.ScreenPointToRay(Camera.main.WorldToScreenPoint(point))), "Click ray selects the actual tall facility bounds");
                await Until(() => ui.IsOpen(UICommon_Facility.Address), 10);
                panel = (UICommon_Facility)ui.GetView(UICommon_Facility.Address);
                Require(panel != null && panel.cache && panel.Layer == UILayer.Common && facilities.Selected == purifier, "Selected facility opens the cached Common MVC panel from built AA");
                Require(panel.TitleText.text == "海水净化器" && panel.RefundText.text.Contains("6") && panel.RefundText.text.Contains("3"), "Panel presents purifier type and refund");
                Require(facilities.Select(grill), "Switch selection to a completed grill");
                Require(panel.TitleText.text == "简易烤架", "Cached panel refreshes its selected type");
                int target = food.Target;
                for (int i = 0; i < 3; i++)
                {
                    panel.CloseButton.onClick.Invoke(); Require(facilities.Selected == null && !ui.IsOpen(UICommon_Facility.Address), "Closing clears selection and hides UI");
                    facilities.Select(grill); await Until(() => ui.IsOpen(UICommon_Facility.Address), 5);
                }
                panel.TargetPlusButton.onClick.Invoke(); Require(food.Target == target + 1, "Cached panel target listener fires once");
                food.SetTarget(1); water.SetTarget(0);
                StepUntil(() => crew.Activity == CrewActivity.Cooking, 300);
                Require(inventory.GetReserved(ResourceType.Fish) == 1 && inventory.GetReserved(ResourceType.Wood) == 1, "Crew is actually cooking with reserved fish and fuel");
                ResourceCost stock = inventory.CaptureInventory();
                panel.MoveButton.onClick.Invoke();
                Require(facilities.IsMoving && grill.IsBeingMoved && inventory.ReservationCount == 0 && crew.Activity != CrewActivity.Cooking, "Beginning a move immediately interrupts work and releases ingredients even while paused");
                Require(Same(stock, inventory.CaptureInventory()) && grill.Coordinate == new Vector2Int(-1, 0) && build.Cells[grill.Coordinate].IsBlocked, "Move preview keeps original footprint and inventory");
                DeckPathfinder.FindReachable(build.Cells, crew.CurrentCell, out _, out var distances);
                Require(!food.TryFindTask(distances, out _), "A moving grill cannot accept new production");
                var oldSnapshot = game.Saves.Capture();
                Require(game.Saves.Validate(oldSnapshot, out _) && oldSnapshot.Facilities.Exists(f => f.Kind == FacilityKind.Grill && f.X == -1 && f.Z == 0), "Saving a preview records original committed placement");
                Require(!facilities.ConfirmMove(new Vector2Int(8, 8)) && facilities.IsMoving && grill.Coordinate == new Vector2Int(-1, 0), "Invalid move is rejected without moving or charging");
                Require(!build.CanMoveFacility(grill, crew.CurrentCell, 0, out _), "Cannot move a facility under its worker");
                Require(!build.CanMoveFacility(grill, purifier.WorkCell, 3, out _), "Another facility's work cell stays protected");
                facilities.Rotate();
                Require(facilities.Rotation == 1 && facilities.Preview(new Vector2Int(-1, -1)), "R-equivalent rotates a valid new footprint and work cell");
                Directory.CreateDirectory("Docs/Previews");
                Capture("Facility_Moving", 1600, 900);
                panel.MoveButton.onClick.Invoke();
                Require(!facilities.IsMoving && grill.Coordinate == new Vector2Int(-1, 0) && Same(stock, inventory.CaptureInventory()), "Cancel leaves layout and inventory intact");
                StepUntil(() => crew.Activity == CrewActivity.Cooking, 300);
                panel.MoveButton.onClick.Invoke(); facilities.Rotate();
                Require(facilities.ConfirmMove(new Vector2Int(-1, -1)), "Commit valid free relocation");
                Require(grill.Rotation == 1 && grill.Coordinate == new Vector2Int(-1, -1) && !build.Cells[new Vector2Int(-1, 0)].IsBlocked &&
                    build.Cells[grill.Coordinate].IsBlocked && build.GrillCount == 1 && build.PurifierCount == 1 && Same(stock, inventory.CaptureInventory()), "Move changes the same facility identity, obstacle and transform without charge");
                StepUntil(() => crew.Activity == CrewActivity.Cooking && crew.CurrentCell == grill.WorkCell, 400);
                Require(crew.CurrentCell == new Vector2Int(0, -1), "Crew finds and operates from the relocated work cell");
                Capture("Facility_Selected", 1600, 900); Capture("Facility_Compact", 1280, 800);
                Require(game.Saves.Save(), "Save relocated layout and original paid cost");
                Require(build.TryMoveFacility(grill, new Vector2Int(-1, 0), 0, out _), "Temporarily move away from saved layout");
                Require(game.Saves.Load(), "Load restores relocation");
                Require(facilities.Selected == null && !ui.IsOpen(UICommon_Facility.Address) && build.Grills[new Vector2Int(-1, -1)].Rotation == 1 &&
                    build.Grills[new Vector2Int(-1, -1)].BuildCost.Wood == 4, "Load clears stale selection and restores position, facing and paid cost");
                grill = build.Grills[new Vector2Int(-1, -1)];
                Require(facilities.Select(grill) && facilities.BeginMove() && game.Saves.Save() && game.Saves.Load(), "Save/load works during a move preview");
                Require(!facilities.IsMoving && facilities.Selected == null && !build.Grills[new Vector2Int(-1, -1)].IsBeingMoved, "Restoring discards transient moving state");
                TestDemolition(); TestWaterAndTravel(); TestPlacement(); TestLegacyCost();
                File.WriteAllText("Docs/Previews/FacilityValidation.txt", "PASS: " + assertions + " checks; actual Play mode and built AA panel; bounds picking; Common/cache MVC; " +
                    "target controls and listener cleanup; paused cooking interruption and reservation release; free move/rotation/cancel; worker/work-cell/connectivity protection; " +
                    "new work-cell scheduling; original placement during preview saves; new layout and historical paid cost after load; transient state cleanup; " +
                    "confirmation/cancel demolition; exact refund and reentrant guard; water/travel interruption; overflow leaves facility intact; old save cost fallback; " +
                    "1600x900 and 1280x800 text/raycast/render checks.\n");
                Debug.Log("FACILITY_VALIDATION_OK checks=" + assertions); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static void TestDemolition()
        {
            Reset(); var grill = Complete(new Vector2Int(-1, 0), 0, FacilityKind.Grill);
            facilities.Select(grill); ResourceCost before = inventory.CaptureInventory();
            facilities.RequestDemolition();
            Require(facilities.ConfirmingDemolition && build.GrillCount == 1 && Same(before, inventory.CaptureInventory()), "First demolition click only asks for confirmation");
            Capture("Facility_Confirm", 1600, 900);
            facilities.CancelDemolition(); Require(!facilities.ConfirmingDemolition && build.GrillCount == 1, "Back keeps facility");
            bool duplicate = false, coherent = false;
            Action callback = () =>
            {
                duplicate |= build.TryDemolishFacility(grill, out _);
                coherent = build.GrillCount == 0 && !build.Cells[grill.Coordinate].IsBlocked && game.Saves.Capture().Facilities.Count == 0;
            };
            inventory.Changed += callback;
            var config = AssetDatabase.LoadAssetAtPath<BuildConfig>("Assets/Configs/Build_Default.asset"); ResourceCost cost = config.GrillCost;
            try
            {
                config.GrillCost = new ResourceCost(19, 17);
                facilities.RequestDemolition(); Require(facilities.ConfirmDemolition(), "Confirmed demolition succeeds");
            }
            finally { config.GrillCost = cost; inventory.Changed -= callback; }
            Require(!duplicate && coherent && inventory.GetTotal(ResourceType.Wood) == before.Wood + 4 && inventory.GetTotal(ResourceType.Scrap) == before.Scrap + 2,
                "Refund uses the historical paid cost exactly once, with coherent state inside callbacks");
            Require(!build.TryDemolishFacility(grill, out _) && !facilities.ConfirmDemolition() && build.CompletedCount == 9 && facilities.Selected == null,
                "Repeated requests never refund twice; deck and selection are correct");
            grill = Complete(new Vector2Int(-1, 0), 0, FacilityKind.Grill);
            inventory.RestoreInventory(new ResourceCost(int.MaxValue, 100));
            Require(!build.TryDemolishFacility(grill, out _) && build.GrillCount == 1 && build.Cells[grill.Coordinate].IsBlocked, "Refund overflow leaves facility and inventory intact");
        }
        private static void TestWaterAndTravel()
        {
            Reset(); var purifier = Complete(new Vector2Int(1, 0), 0, FacilityKind.Purifier);
            water.SetTarget(4); StepUntil(() => crew.Activity == CrewActivity.Walking, 20); Step();
            Vector3 walkingPosition = crew.Worker.position;
            facilities.Select(purifier); Require(facilities.BeginMove(), "Move purifier while worker is en route");
            Require(inventory.ReservationCount == 0 && crew.Worker.position == walkingPosition, "Travel interruption does not teleport the worker or leak claims");
            facilities.CancelMove(); StepUntil(() => crew.Activity == CrewActivity.Purifying, 300);
            Require(inventory.GetReserved(ResourceType.Wood) == 1, "Water batch reserves fuel");
            ResourceCost before = inventory.CaptureInventory();
            Require(build.TryMoveFacility(purifier, new Vector2Int(1, -1), 3, out _) && inventory.ReservationCount == 0 && Same(before, inventory.CaptureInventory()), "Direct purifier move cancels fuel immediately");
            StepUntil(() => crew.Activity == CrewActivity.Purifying && crew.CurrentCell == purifier.WorkCell, 300);
            Require(build.TryDemolishFacility(purifier, out _) && inventory.ReservationCount == 0 && inventory.GetTotal(ResourceType.Wood) == before.Wood + 6 &&
                inventory.GetTotal(ResourceType.Scrap) == before.Scrap + 3 && inventory.GetTotal(ResourceType.Water) == 0, "Demolishing a running purifier releases fuel and refunds construction, without free water");
        }
        private static void TestPlacement()
        {
            Reset(); var grill = Complete(new Vector2Int(-1, 0), 0, FacilityKind.Grill);
            Require(build.TryMoveFacility(grill, new Vector2Int(-1, 1), 2, out _) && grill.WorkCell == new Vector2Int(-1, 0), "New work cell may use the released original footprint");
            Require(build.TryMoveFacility(grill, new Vector2Int(-1, 0), 0, out _) && build.TryPlaceBlueprint(new Vector2Int(-2, 1), out _), "Set up dependent outer blueprint");
            Require(!build.CanMoveFacility(grill, new Vector2Int(-1, 1), 1, out _), "Move cannot strand pending deck blueprints");
            var decks = new List<DeckSave>();
            for (int x = -2; x <= 2; x++) decks.Add(new DeckSave { X = x, Z = 0, Complete = true, Progress = 1 });
            decks.Add(new DeckSave { X = 2, Z = 1, Complete = true, Progress = 1 });
            crew.CancelAssignments(); build.RestoreLayout(decks, new List<FacilitySave>()); crew.RestorePosition(Vector2Int.zero);
            grill = Complete(new Vector2Int(2, 1), 2, FacilityKind.Grill);
            Require(!build.CanMoveFacility(grill, Vector2Int.zero, 1, out _), "Cannot sever a narrow deck corridor");
        }
        private static void TestLegacyCost()
        {
            Reset(); var snapshot = game.Saves.Capture();
            snapshot.Facilities.Add(new FacilitySave { X = -1, Z = 0, Kind = FacilityKind.Grill, Rotation = 0, Complete = true, Progress = 1 });
            Require(game.Saves.Validate(snapshot, out _), "Version 2 saves without paid cost remain valid");
            build.RestoreLayout(snapshot.Decks, snapshot.Facilities);
            var grill = build.Grills[new Vector2Int(-1, 0)];
            Require(grill.BuildCost.Wood == 4 && grill.BuildCost.Scrap == 2, "Old facilities fall back to configured construction cost");
            snapshot.Facilities[0].HasBuildCost = true; snapshot.Facilities[0].BuildCost = new ResourceCost(-1, 0);
            Require(!game.Saves.Validate(snapshot, out _), "Negative saved refund cost is rejected");
        }
        private static void Reset()
        {
            crew.CancelAssignments();
            var decks = new List<DeckSave>();
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) decks.Add(new DeckSave { X = x, Z = z, Complete = true, Progress = 1 });
            build.RestoreUnlocks(true); build.RestoreLayout(decks, new List<FacilitySave>());
            inventory.RestoreInventory(new ResourceCost(100, 100, 8)); crew.RestorePosition(Vector2Int.zero);
            var needs = UnityEngine.Object.FindObjectOfType<NeedsManager>(); needs.SetSatiety(100); needs.SetHydration(100);
            food.SetTarget(0); water.SetTarget(0);
        }
        private static GrillCell Complete(Vector2Int coordinate, int rotation, FacilityKind kind)
        {
            Require(build.TryPlaceFacilityBlueprint(coordinate, rotation, kind, out _) && build.TryBeginGrill(coordinate) && build.TryCompleteGrill(coordinate), "Build fixture facility " + kind);
            return build.Grills[coordinate];
        }
        private static void Step()
        {
            crew.Tick(.05f);
            Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Worker remains on walkable deck");
        }
        private static void StepUntil(Func<bool> condition, int max)
        { for (int index = 0; index < max && !condition(); index++) Step(); Require(condition(), "Task transition: " + crew.StatusText); }
        private static bool Same(ResourceCost a, ResourceCost b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        private static void Capture(string name, int width, int height)
        {
            var hud = (UICommon_Build)ui.GetView(UICommon_Build.Address);
            VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), hud, "Docs/Previews/" + name + ".png", width, height, () =>
            {
                // Camera rendering updates the offscreen pixel rect. Settle responsive layouts before
                // inspecting bounds; the editor's normal 640x480 canvas is not the capture geometry.
                for (int pass = 0; pass < 2; pass++)
                {
                    Camera.main.Render();
                    foreach (var graphic in ui.GetComponentsInChildren<UnityEngine.UI.Graphic>()) graphic.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                }
                if (panel == null || !panel.gameObject.activeInHierarchy) return;
                var layout = hud.GetComponent<ManagementHudLayout>();
                Rect cardBounds = ScreenRect(panel.Card), timeBounds = ScreenRect(layout.TimeControls);
                Require(!cardBounds.Overlaps(timeBounds), "Facility panel leaves time controls visible at " + width + "x" + height +
                    "; card=" + cardBounds + "; time=" + timeBounds + "; root=" + ((RectTransform)panel.transform).rect + "; offset=" + panel.Card.anchoredPosition);
                RectTransform rect = panel.MoveButton.GetComponent<RectTransform>();
                Vector2 point = RectTransformUtility.WorldToScreenPoint(Camera.main, rect.TransformPoint(rect.rect.center));
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(panel.transform), "Facility card intercepts UI clicks");
            });
        }
        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(Camera.main, corners[0]), max = RectTransformUtility.WorldToScreenPoint(Camera.main, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static async Task Until(Func<bool> condition, float timeout)
        {
            double end = EditorApplication.timeSinceStartup + timeout;
            while (!condition()) { Require(EditorApplication.timeSinceStartup < end, "Timed out waiting for AA panel"); var t = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => t.TrySetResult(true); await t.Task; }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Facility validation: " + message); assertions++; }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Wait;
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
