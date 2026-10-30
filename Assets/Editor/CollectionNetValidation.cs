using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
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
    public static class CollectionNetValidation
    {
        private const string Key = "RaftGame.ValidateCollectionNets";
        private static GameManager game;
        private static BuildManager build;
        private static CrewManager crew;
        private static ResourceManager inventory;
        private static SalvageManager salvage;
        private static CollectionNetManager nets;
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
        { if (!SessionState.GetBool(Key, false)) return; EditorApplication.update -= Wait; EditorApplication.update += Wait; }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying) return;
            ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address)) { EditorApplication.update -= Wait; Run(); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) { Debug.LogError("Collection net startup timeout"); Finish(1); }
        }
        private static async void Run()
        {
            try
            {
                game = UnityEngine.Object.FindObjectOfType<GameManager>(); build = UnityEngine.Object.FindObjectOfType<BuildManager>();
                crew = UnityEngine.Object.FindObjectOfType<CrewManager>(); inventory = UnityEngine.Object.FindObjectOfType<ResourceManager>();
                salvage = UnityEngine.Object.FindObjectOfType<SalvageManager>(); nets = game.CollectionNets;
                hud = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && nets != null && nets.IsInitialized && hud.NetButton != null, "GameManager initializes nets and loads AA recipe controls");
                game.TimeControl.SetPaused(true); game.enabled = false; Reset(8, 2);
                hud.BuildButton.onClick.Invoke(); hud.ProductionCategoryButton.onClick.Invoke();
                Require(hud.NetButton.gameObject.activeInHierarchy && !hud.WaterButton.gameObject.activeSelf && hud.CatalogHeading.text.Contains("2"), "Starter catalog contains grill and net while purifier stays locked");
                Require(hud.NetButton.GetComponent<RectTransform>().anchoredPosition.x == 436, "Unlocked cards pack together without an empty locked recipe slot");
                Directory.CreateDirectory("Docs/Previews"); Capture("CollectionNet_Catalog", 1600, 900);
                hud.NetButton.onClick.Invoke();
                Require(build.Tool == BuildTool.CollectionNetBlueprint && !build.CatalogOpen && hud.ModeText.text.Contains("收集网"), "Net card enters its placement tool and closes catalog");
                var coordinate = new Vector2Int(-1, 0);
                Require(build.TryPlaceFacilityBlueprint(coordinate, 1, FacilityKind.CollectionNet, out _), "Plan net at edge with inward work cell");
                Require(inventory.GetTotal(ResourceType.Wood) == 8 && inventory.ReservationCount == 0, "Planning is free");
                build.SetTool(BuildTool.None); game.enabled = true; game.TimeControl.SetSpeed(4);
                await Until(() => build.NetCount == 1 && crew.Activity == CrewActivity.Idle, 12);
                var net = build.Grills[coordinate];
                Require(inventory.GetTotal(ResourceType.Wood) == 2 && inventory.GetTotal(ResourceType.Scrap) == 0 && build.CompletedCount == 9 && build.BlueprintCount == 0,
                    "Real crew construction settles six wood and two scrap and keeps deck/blueprint counts correct");
                game.TimeControl.SetPaused(true);
                var item = salvage.Spawn(FloatingKind.Wood, build.CellToWorld(net.MouthCell));
                salvage.Tick(0); Require(net.Contents.Count == 0 && salvage.IsActive(item), "Paused drift cannot capture an item already in the mouth");
                int wood = inventory.GetTotal(ResourceType.Wood);
                game.TimeControl.SetSpeed(4);
                await Until(() => crew.Activity == CrewActivity.CollectingNet && crew.WorkProgress > .1f, 10);
                game.TimeControl.SetPaused(true);
                Require(net.Contents.Count == 1 && !salvage.IsActive(item) && inventory.GetTotal(ResourceType.Wood) == wood && crew.CurrentCell == net.WorkCell,
                    "GameManager drift captures to net, then worker reaches safe work cell without early credit");
                float progress = crew.WorkProgress; await RealWait(.2);
                Require(crew.WorkProgress == progress && net.Contents.Count == 1, "Pause freezes collection progress and contents");
                game.Facilities.Select(net); await Until(() => ui.IsOpen(UICommon_Facility.Address), 8);
                Capture("CollectionNet_Collecting", 1600, 900);
                game.TimeControl.SetSpeed(4);
                await Until(() => net.Contents.Count == 0 && inventory.GetTotal(ResourceType.Wood) == wood + 2, 8);
                Require(inventory.ReservationCount == 0, "Automatic collection awards captured amount exactly once");
                game.TimeControl.SetPaused(true); game.enabled = false;
                await TestStorageAndMove(); TestOwnershipAndOverflow(); TestLayoutAndPriority(); TestInterruptions();
                File.WriteAllText("Docs/Previews/CollectionNetValidation.txt", "PASS: " + checks + " checks; actual Play mode with built AA catalog and facility panel; " +
                    "starter unlock and six-wood/two-scrap construction; real drift interception and automatic crew collection; pause; four-slot capacity and visuals; " +
                    "manual-claim transfer; no early rewards or pity advancement; crates reward/unlock only on collection; move/cancel with cargo; nonempty demolition guard; " +
                    "save/load cargo and moving-preview recovery; malformed cargo/mouth saves; exact credit and atomic callback snapshots; ownership and overflow; " +
                    "swept capture without tunnelling through decks; inward work cells and protected mouths; full-net priority; safe travel and needs interruption; " +
                    "catalog and facility captures at 1600x900 and 1280x800.\n");
                Debug.Log("COLLECTION_NET_VALIDATION_OK checks=" + checks); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static async Task TestStorageAndMove()
        {
            Reset(); var net = Complete(new Vector2Int(-1, 0), 1);
            ResourceCost stock = inventory.CaptureInventory(); string lootBefore = JsonUtility.ToJson(game.Loot.Capture());
            var wood = salvage.Spawn(FloatingKind.Wood, build.CellToWorld(net.MouthCell));
            object manualOwner = new object(); salvage.SetMarked(wood, true); salvage.TryClaim(wood, manualOwner); salvage.Tick(.01f);
            Require(net.Contents.Count == 1 && !salvage.IsClaimValid(wood, manualOwner), "Capture transfers a marked/manual claim without double collection");
            Catch(net, FloatingKind.Scrap); Catch(net, FloatingKind.Bottle); Catch(net, FloatingKind.SupplyCrate);
            Require(net.Contents.Count == 4 && Same(stock, inventory.CaptureInventory()) && JsonUtility.ToJson(game.Loot.Capture()) == lootBefore && !build.PurifierUnlocked,
                "All four supply types stay stored, without inventory rewards, random rolls or blueprint progress");
            Require(net.NetView.Wood[0].activeSelf && net.NetView.Scrap[1].activeSelf && net.NetView.Bottles[2].activeSelf && net.NetView.Crates[3].activeSelf,
                "Four visual slots display the correct captured models");
            var fifth = salvage.Spawn(FloatingKind.Wood, build.CellToWorld(net.MouthCell)); salvage.Tick(.01f);
            Require(net.Contents.Count == 4 && salvage.IsActive(fifth), "Full net leaves fifth supply drifting"); salvage.Remove(fifth);
            salvage.Tick(100); Require(net.Contents.Count == 4, "Stored supplies do not expire or drift away");
            game.Facilities.Select(net); await Until(() => ui.IsOpen(UICommon_Facility.Address), 5);
            var panel = (UICommon_Facility)ui.GetView(UICommon_Facility.Address);
            Require(panel.TitleText.text.Contains("收集网") && panel.TargetText.text.Contains("4 / 4") && !panel.TargetPlusButton.gameObject.activeSelf && !panel.DemolishButton.interactable,
                "Facility panel shows storage/full state and protects nonempty nets");
                await RealWait(.15);
            Capture("CollectionNet_Full", 1600, 900); Capture("CollectionNet_Compact", 1280, 800);
            Require(!build.TryDemolishFacility(net, out _) && Same(stock, inventory.CaptureInventory()), "Nonempty demolition cannot destroy cargo or refund twice");
            Require(game.Saves.Save(), "Save four stored supplies");
            var snapshot = game.Saves.Capture();
            Require(snapshot.Facilities[0].NetContents.Count == 4 && snapshot.Floating.Count == salvage.Items.Count && game.Saves.Validate(snapshot, out _), "Snapshot stores cargo separately from floating items");
            Require(game.Facilities.BeginMove(), "Begin moving full net");
            var savedPreview = game.Saves.Capture(); Require(savedPreview.Facilities[0].X == -1 && savedPreview.Facilities[0].Z == 0 && savedPreview.Facilities[0].NetContents.Count == 4, "Preview save preserves committed position and cargo");
            game.Facilities.CancelMove();
            Require(game.Facilities.BeginMove(), "Restart free move");
            game.Facilities.Rotate(); game.Facilities.Rotate(); game.Facilities.Rotate();
            Require(game.Facilities.Rotation == 0 && game.Facilities.ConfirmMove(new Vector2Int(-1, -1)) && net.Contents.Count == 4 && Same(stock, inventory.CaptureInventory()), "Move transfers cargo and orientation without cost");
            Require(game.Saves.Load() && build.NetCount == 1, "Read original full-net save");
            net = build.Grills[new Vector2Int(-1, 0)];
            Require(net.Contents.Count == 4 && net.NetView.Crates[3].activeSelf && !net.IsBeingMoved && game.Facilities.Selected == null, "Load restores ordered cargo and visuals without stale selection/session");
            snapshot = game.Saves.Capture(); snapshot.Facilities[0].NetContents.Add(new NetSupply { Kind = FloatingKind.Wood, Amount = 1 });
            Require(!game.Saves.Validate(snapshot, out _), "Reject over-capacity saved cargo");
            snapshot = game.Saves.Capture(); snapshot.Facilities[0].NetContents[0].Amount = -1;
            Require(!game.Saves.Validate(snapshot, out _), "Reject negative saved cargo");
            snapshot = game.Saves.Capture(); snapshot.Facilities[0].NetContents[0].Kind = (FloatingKind)99;
            Require(!game.Saves.Validate(snapshot, out _), "Reject unknown saved cargo type");
            snapshot = game.Saves.Capture(); snapshot.Facilities[0].Kind = FacilityKind.Grill;
            Require(!game.Saves.Validate(snapshot, out _), "Other facilities cannot hide net cargo");
            snapshot = game.Saves.Capture(); snapshot.Decks.Add(new DeckSave { X = net.MouthCell.x, Z = net.MouthCell.y, Complete = false });
            Require(!game.Saves.Validate(snapshot, out _), "Save cannot put a blueprint across a net mouth");
            StepUntil(() => net.Contents.Count == 0, 400);
            Require(build.PurifierUnlocked && inventory.GetTotal(ResourceType.Wood) >= stock.Wood + 2 && inventory.GetTotal(ResourceType.Scrap) >= stock.Scrap + 1 && inventory.GetTotal(ResourceType.Water) >= 1,
                "Crew drains all stored types; guaranteed crate blueprint and resources settle only now");
            int beforeRefund = inventory.GetTotal(ResourceType.Wood);
            Require(build.TryDemolishFacility(net, out _) && inventory.GetTotal(ResourceType.Wood) == beforeRefund + 6 && build.NetCount == 0,
                "Empty net supports normal demolition/refund");
            hud.BuildButton.onClick.Invoke(); hud.ProductionCategoryButton.onClick.Invoke();
            Require(hud.WaterButton.gameObject.activeInHierarchy && hud.NetButton.GetComponent<RectTransform>().anchoredPosition.x == 660 && hud.CatalogHeading.text.Contains("3"),
                "Discovering purifier expands the catalog to three correctly positioned recipes");
            Capture("CollectionNet_Unlocked", 1600, 900); hud.CatalogCloseButton.onClick.Invoke();
        }
        private static void TestOwnershipAndOverflow()
        {
            Reset(); var net = Complete(new Vector2Int(-1, 0), 1); Catch(net, FloatingKind.SupplyCrate);
            object owner = new object(), other = new object();
            var session = nets.TryAcquire(owner, net);
            Require(session != null && nets.TryAcquire(other, net) == null && !nets.Advance(session, other, net.WorkCell, 2) && !nets.Advance(session, owner, Vector2Int.up, 2), "Owner and adjacent standing cell are required");
            Require(nets.Advance(session, owner, net.WorkCell, 0) && session.Progress == 0, "Zero delta cannot collect");
            nets.Cancel(session, other); Require(nets.IsValid(session, owner), "Other owner cannot cancel");
            nets.Cancel(session, owner); Require(net.Contents.Count == 1 && nets.TryAcquire(other, net) != null, "Cancellation releases the claim and keeps cargo");
            build.TryBeginFacilityMove(net); build.CancelFacilityMove(net);
            inventory.RestoreInventory(new ResourceCost(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
            session = nets.TryAcquire(owner, net); string loot = JsonUtility.ToJson(game.Loot.Capture());
            Require(!nets.Advance(session, owner, net.WorkCell, 3) && net.Contents.Count == 1 && !build.PurifierUnlocked && JsonUtility.ToJson(game.Loot.Capture()) == loot,
                "Failed crate grant keeps cargo, random state and blueprint pity"); nets.Cancel(session, owner);
            inventory.RestoreInventory(new ResourceCost(10, 10)); session = nets.TryAcquire(owner, net);
            bool reentered = false, coherent = false;
            Action callback = () =>
            {
                reentered |= nets.Advance(session, owner, net.WorkCell, 3);
                var captured = game.Saves.Capture();
                coherent = net.Contents.Count == 0 && captured.Facilities[0].NetContents.Count == 0 && captured.PurifierUnlocked && game.Saves.Validate(captured, out _);
            };
            inventory.Changed += callback;
            try { Require(nets.Advance(session, owner, net.WorkCell, 3), "Collect a crate after capacity is available"); }
            finally { inventory.Changed -= callback; }
            Require(!reentered && coherent && !nets.Advance(session, owner, net.WorkCell, 3), "Inventory callbacks observe one coherent crate transaction and cannot re-credit it");
        }
        private static void TestLayoutAndPriority()
        {
            Reset();
            Require(!build.CanPlaceFacility(Vector2Int.zero, 0, FacilityKind.CollectionNet, out _) && !build.CanPlaceFacility(new Vector2Int(-1, 0), 3, FacilityKind.CollectionNet, out _), "Interior and outward work-cell placements are invalid");
            var left = Complete(new Vector2Int(-1, 0), 1); var right = Complete(new Vector2Int(1, 0), 3);
            Require(!build.CanPlace(left.MouthCell, out _) && !build.CanPlaceFacility(left.MouthCell, 0, FacilityKind.Grill, out _), "Net mouth remains water for deck and facility tools");
            var source = salvage.Spawn(FloatingKind.Wood, build.CellToWorld(left.MouthCell) + Vector3.forward * 3);
            Vector3 a = source.transform.position, b = a - Vector3.forward * 6;
            Require(nets.TryCapture(source, a, b) && left.Contents.Count == 1 && !nets.TryCapture(source, a, b), "Swept capture catches a fast passing item once");
            for (int index = 0; index < 4; index++) Catch(right, FloatingKind.Wood);
            DeckPathfinder.FindReachable(build.Cells, crew.CurrentCell, out _, out var distances);
            Require(nets.TryFindTask(distances, out var chosen) && chosen == right, "Full reachable net is collected before a partially full net");
            var crossing = salvage.Spawn(FloatingKind.Scrap, build.CellToWorld(right.MouthCell));
            Require(!nets.TryCapture(crossing, crossing.transform.position, build.CellToWorld(left.MouthCell)), "Supplies cannot sweep through the raft to a far-side net"); salvage.Remove(crossing);
            build.TryBeginFacilityMove(left);
            source = salvage.Spawn(FloatingKind.Scrap, build.CellToWorld(left.MouthCell)); salvage.Tick(.01f);
            Require(left.Contents.Count == 1 && salvage.IsActive(source), "Moving net pauses capture and keeps stored cargo");
            build.CancelFacilityMove(left); salvage.Tick(.01f); Require(left.Contents.Count == 2, "Canceling move resumes capture");
            Require(build.TryPlaceBlueprint(new Vector2Int(-1, 2), out _) && !build.CanMoveFacility(left, new Vector2Int(-1, 1), 2, out _), "Pending deck at the new mouth blocks relocation");
        }
        private static void TestInterruptions()
        {
            Reset(); var net = Complete(new Vector2Int(-1, 1), 1); Catch(net, FloatingKind.Wood);
            Step(); Step();
            Require(crew.Activity == CrewActivity.Walking, "Crew starts travelling toward a stocked net");
            Vector3 position = crew.Worker.position;
            Require(build.TryBeginFacilityMove(net) && crew.Worker.position == position && net.Contents.Count == 1, "Move interrupts travel without teleporting or losing contents");
            build.CancelFacilityMove(net); StepUntil(() => crew.Activity == CrewActivity.CollectingNet, 250);
            inventory.Add(ResourceType.CookedFish, 1); UnityEngine.Object.FindObjectOfType<NeedsManager>().SetSatiety(20); Step();
            Require(crew.Activity == CrewActivity.Eating && net.Contents.Count == 1, "Eating interrupts net collection without consuming the stored item");
            StepUntil(() => net.Contents.Count == 0, 350);
            Catch(net, FloatingKind.Wood); StepUntil(() => crew.Activity == CrewActivity.CollectingNet, 250);
            crew.CancelAssignments();
            Require(net.Contents.Count == 1 && nets.TryAcquire(new object(), net) != null && inventory.ReservationCount == 0, "Cancel/shutdown path releases net claim without cargo or material loss");
        }
        private static GrillCell Complete(Vector2Int coordinate, int rotation)
        {
            Require(build.TryPlaceFacilityBlueprint(coordinate, rotation, FacilityKind.CollectionNet, out _) && build.TryBeginGrill(coordinate) && build.TryCompleteGrill(coordinate), "Construct fixture collection net");
            return build.Grills[coordinate];
        }
        private static void Catch(GrillCell net, FloatingKind kind)
        {
            var item = salvage.Spawn(kind, build.CellToWorld(net.MouthCell)); Require(item != null, "Spawn capture supply");
            salvage.Tick(.01f); Require(!salvage.IsActive(item), "Supply enters net");
        }
        private static void Reset(int wood = 100, int scrap = 100)
        {
            crew.CancelAssignments();
            var decks = new List<DeckSave>(); for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) decks.Add(new DeckSave { X = x, Z = z, Complete = true, Progress = 1 });
            build.CatalogOpen = false; build.RestoreUnlocks(false); build.RestoreLayout(decks, new List<FacilitySave>());
            inventory.RestoreInventory(new ResourceCost(wood, scrap)); crew.RestorePosition(Vector2Int.zero);
            salvage.RestoreItems(new List<FloatingSave>(), 1000, 41, 0); game.Loot.Restore(new LootState { RandomState = 17, CratesWithoutBlueprint = 5 });
            UnityEngine.Object.FindObjectOfType<FishingManager>().SetAutomaticFishing(false);
            UnityEngine.Object.FindObjectOfType<ProductionManager>().SetTarget(0); game.Water.SetTarget(0);
            var needs = UnityEngine.Object.FindObjectOfType<NeedsManager>(); needs.SetSatiety(100); needs.SetHydration(100);
        }
        private static void Step() { crew.Tick(.05f); Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(crew.Worker.position)), "Crew stays on unobstructed completed deck"); }
        private static void StepUntil(Func<bool> condition, int limit) { for (int step = 0; step < limit && !condition(); step++) Step(); Require(condition(), "Task transition: " + crew.StatusText); }
        private static bool Same(ResourceCost a, ResourceCost b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        private static void Capture(string name, int width, int height)
        {
            VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), hud, "Docs/Previews/" + name + ".png", width, height, () =>
            {
                for (int pass = 0; pass < 2; pass++) { Camera.main.Render(); foreach (var graphic in ui.GetComponentsInChildren<UnityEngine.UI.Graphic>()) graphic.SetAllDirty(); Canvas.ForceUpdateCanvases(); }
            });
        }
        private static async Task Until(Func<bool> predicate, float seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (!predicate()) { Require(EditorApplication.timeSinceStartup < end, "Timed out waiting for net operation"); await NextFrame(); } }
        private static async Task RealWait(double seconds) { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) await NextFrame(); }
        private static Task NextFrame() { var t = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => t.TrySetResult(true); return t.Task; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Collection net validation: " + message); checks++; }
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
