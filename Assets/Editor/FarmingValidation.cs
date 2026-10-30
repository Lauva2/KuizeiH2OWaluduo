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
using RaftGame.Farming;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.UI;
namespace RaftGame.Editor
{
    public static class FarmingValidation
    {
        private const string Key = "RaftGame.FarmingValidation";
        private static GameManager game;
        private static BuildManager build;
        private static CrewManager crew;
        private static FarmingManager farm;
        private static ResourceManager stock;
        private static ProductionManager cooking;
        private static UIManager ui;
        private static UICommon_Build hud;
        private static int checks;
        public static void Validate()
        {
            EditorSceneManager.OpenScene(RaftSceneBuilder.ScenePath);
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 2; AssetDatabase.SaveAssets();
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 100); Install(); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Install()
        { if (SessionState.GetBool(Key, false)) { EditorApplication.update -= Wait; EditorApplication.update += Wait; } }
        private static void Wait()
        {
            if (!EditorApplication.isPlaying) return; ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (ui != null && ui.IsOpen(UICommon_Build.Address)) { EditorApplication.update -= Wait; Run(); }
            else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0)) { Debug.LogError("Farming startup timeout"); Finish(1); }
        }
        private static async void Run()
        {
            try
            {
                game = UnityEngine.Object.FindObjectOfType<GameManager>(); build = UnityEngine.Object.FindObjectOfType<BuildManager>(); crew = game.Crew; farm = game.Farming;
                stock = UnityEngine.Object.FindObjectOfType<ResourceManager>(); cooking = UnityEngine.Object.FindObjectOfType<ProductionManager>(); hud = (UICommon_Build)ui.GetView(UICommon_Build.Address);
                Require(game.IsInitialized && farm.IsInitialized && crew.Farming == farm && stock.GetTotal(ResourceType.Potato) == 2 && stock.GetTotal(ResourceType.BakedPotato) == 0 && build.PlanterCount == 0, "Authored game initializes farming with two seed potatoes and no free planter or meal");
                game.TimeControl.SetPaused(true); game.enabled = false; Reset(1, new ResourceCost(8, 2, 0, 0, 2, 2));
                hud.BuildButton.onClick.Invoke(); hud.LivingCategoryButton.onClick.Invoke();
                Require(hud.CatalogHeading.text.Contains("5") && hud.PlanterButton.gameObject.activeInHierarchy && hud.PlanterRecipeCost.text == "4" && hud.PlanterScrapCost.text == "1" && hud.ResourceTiles.Length == 7, "AA catalog includes the four-wood/one-scrap planter and both potato resource tiles");
                Directory.CreateDirectory("Docs/Previews"); Capture("Farming_Catalog", 1600, 900); Capture("Farming_CatalogCompact", 1280, 800);
                hud.PlanterButton.onClick.Invoke(); Require(build.Tool == BuildTool.PlanterBlueprint && !build.CatalogOpen, "Planter recipe enters placement tool");
                var coordinate = new Vector2Int(-1, 0); Require(build.TryPlaceFacilityBlueprint(coordinate, 1, FacilityKind.Planter, out _) && stock.ReservationCount == 0, "Planter blueprint is free until worker arrival");
                build.SetTool(BuildTool.None); game.enabled = true; game.TimeControl.SetSpeed(4);
                await Until(() => build.PlanterCount == 1 && crew.Activity == CrewActivity.Planting && crew.WorkProgress > .1f, 12);
                game.TimeControl.SetPaused(true); var plot = build.Grills[coordinate];
                Require(stock.GetTotal(ResourceType.Wood) == 4 && stock.GetTotal(ResourceType.Scrap) == 1 && stock.GetTotal(ResourceType.Potato) == 2 && stock.GetReserved(ResourceType.Potato) == 1 && plot.CropStage == CropStage.Empty && build.BlueprintCount == 0, "Real construction charges materials once; planting reserves but does not yet consume its seed");
                float progress = crew.WorkProgress; await RealWait(.15); Require(crew.WorkProgress == progress, "Pause freezes planting");
                game.TimeControl.SetSpeed(4); await Until(() => crew.Activity == CrewActivity.Watering && crew.WorkProgress > .1f, 8); game.TimeControl.SetPaused(true);
                Require(plot.CropStage == CropStage.Planted && stock.GetTotal(ResourceType.Potato) == 1 && stock.GetTotal(ResourceType.Water) == 2 && stock.GetReserved(ResourceType.Water) == 1 && crew.Worker.GetComponent<CrewView>().WateringCan.activeSelf, "Planting consumes one potato; separate watering reserves water and presents its tool");
                Require(game.Facilities.Select(plot), "Select watered planter"); await Until(() => ui.IsOpen(UICommon_Facility.Address), 8); Capture("Farming_Watering", 1600, 900);
                game.TimeControl.SetSpeed(4); await Until(() => plot.CropStage == CropStage.Growing && plot.GrowthProgress > .18f, 10); game.TimeControl.SetPaused(true);
                Require(stock.GetTotal(ResourceType.Water) == 1 && stock.ReservationCount == 0 && plot.CropView.Plants.gameObject.activeSelf && !plot.CropView.Potatoes.activeSelf, "Water is consumed once; crops grow without tying up a worker");
                var panel = (UICommon_Facility)ui.GetView(UICommon_Facility.Address); panel.TargetPlusButton.onClick.Invoke();
                Require(!plot.RepeatPlanting && panel.TargetText.text.Contains("关") && !panel.DemolishButton.interactable, "Continuous planting can stop after this crop without destroying it");
                float growth = plot.GrowthProgress; await RealWait(.15); Require(plot.GrowthProgress == growth, "Pause freezes passive growth"); Capture("Farming_Growing", 1600, 900); Capture("Farming_Compact", 1280, 800);
                game.TimeControl.SetSpeed(4); await Until(() => plot.CropStage == CropStage.Mature && crew.Activity == CrewActivity.Harvesting, 20); game.TimeControl.SetPaused(true);
                Require(plot.CropView.Potatoes.activeSelf && stock.GetTotal(ResourceType.Potato) == 1 && !build.TryDemolishFacility(plot, out _), "Mature roots are visible but produce no reward before harvest; demolition protects the crop"); Capture("Farming_Mature", 1600, 900);
                game.TimeControl.SetSpeed(4); await Until(() => plot.CropStage == CropStage.Empty && stock.GetTotal(ResourceType.Potato) == 4, 8); game.TimeControl.SetPaused(true); game.enabled = false;
                Require(!plot.RepeatPlanting && stock.ReservationCount == 0 && !plot.CropView.Plants.gameObject.activeSelf, "Harvest awards three potatoes once and disabled repeat leaves the empty box available");
                TestResources(); TestReservationsAndWater(); TestCrewCoordination(); TestMoveAndSaves(); TestOverflow(); TestFood(); await TestActualFood();
                File.WriteAllText("Docs/Previews/FarmingValidation.txt", "PASS: " + checks + " checks; actual Play with built AA five-card living catalog and seven-resource HUD; starter seed potatoes; four-wood/one-scrap crew construction, seed reservation/planting, separate watering tool, passive sixty-second growth, mature model, exact harvest and stop-after-cycle; seven-resource reservation/conversion/overflow invariants; owner and station checks, drinking priority including newly thirsty crew, partial/canceled inputs and atomic callbacks; crop-preserving move/rotation/paused preview, version-six crop snapshots and malformed-state rejection, version-five migration; mature-overflow retention and exactly-once harvest; shared cooked-food target, seed protection, parallel potato cooking, fish fallback and eating baked potatoes; actual cooking/eating visuals and cached crop controls at 1600x900/1280x800.\n");
                Debug.Log("FARMING_VALIDATION_OK checks=" + checks); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void TestResources()
        {
            Reset(1, new ResourceCost(10, 10, 10, 10, 10, 10, 10)); object owner = new object(); var cost = new ResourceCost(1, 2, 1, 1, 3, 2, 1);
            Require(stock.TryReserve(owner, cost) && !stock.TryReserve(owner, cost), "All seven resource reservations are exclusive"); Invariant();
            Require(!stock.TryConvert(new object(), ResourceCost.Single(ResourceType.BakedPotato)), "Foreign conversion cannot consume reserved ingredients");
            Require(stock.TryConvert(owner, ResourceCost.Single(ResourceType.BakedPotato, 2)) && stock.GetTotal(ResourceType.Potato) == 8 && stock.GetTotal(ResourceType.BakedPotato) == 11 && !stock.Commit(owner), "Potato input and food output settle atomically and once"); Invariant();
            stock.RestoreInventory(new ResourceCost(2, 0, 0, 0, 0, 2, int.MaxValue));
            Require(stock.TryReserve(owner, new ResourceCost(1, 0, 0, 0, 0, 1)) && !stock.TryConvert(owner, ResourceCost.Single(ResourceType.BakedPotato)) && stock.GetTotal(ResourceType.Wood) == 2 && stock.GetTotal(ResourceType.Potato) == 2 && stock.GetReserved(ResourceType.Potato) == 1, "Overflow cannot consume raw potatoes or fuel");
            Require(stock.Release(owner) && !stock.Release(owner), "Cancellation releases potato input once"); Invariant();
            Require(!stock.CanAfford(new ResourceCost(0, 0, 0, 0, 0, -1)), "Negative crop resources are rejected");
            stock.RestoreInventory(default); Require(game.Loot.OpenCrate() && stock.GetTotal(ResourceType.Potato) == 1, "Supply crates replenish a seed potato without changing existing loot rolls");
        }
        private static void TestReservationsAndWater()
        {
            Reset(1, new ResourceCost(100, 100, 0, 0, 1, 1)); var plot = Complete(new Vector2Int(-1, 0), FacilityKind.Planter, 1); object owner = new object();
            var plant = farm.TryAcquire(owner, plot, FarmAction.Plant); Require(plant != null && farm.TryAcquire(new object(), plot, FarmAction.Plant) == null && !farm.Begin(plant, new object(), plot.WorkCell) && !farm.Begin(plant, owner, Vector2Int.one), "Planter owner and actual arrival control planting");
            Require(farm.Begin(plant, owner, plot.WorkCell), "Reserve one seed at planting start"); farm.Advance(plant, owner, plot.WorkCell, .5f); farm.Cancel(plant, new object());
            Require(stock.GetReserved(ResourceType.Potato) == 1 && plot.CropStage == CropStage.Empty, "Foreign cancellation and partial planting cannot consume seeds"); farm.Cancel(plant, owner);
            Require(stock.GetAvailable(ResourceType.Potato) == 1 && plot.CropStage == CropStage.Empty, "Canceled planting returns its entire seed");
            plant = farm.TryAcquire(owner, plot, FarmAction.Plant); farm.Begin(plant, owner, plot.WorkCell); bool coherent = false;
            Action snapshot = () => coherent = plot.CropStage == CropStage.Planted && stock.GetTotal(ResourceType.Potato) == 0 && stock.GetReserved(ResourceType.Potato) == 0 && game.Saves.Validate(game.Saves.Capture(), out _);
            stock.Changed += snapshot; Require(farm.Advance(plant, owner, plot.WorkCell, 10), "Commit planted crop"); stock.Changed -= snapshot; Require(coherent, "Inventory callbacks see matching committed seed and planted state");
            crew.Primary.Needs.SetHydration(10); Require(farm.TryAcquire(owner, plot, FarmAction.Water) == null, "Last bottle is kept for a thirsty sailor");
            crew.Primary.Needs.SetHydration(100); var watering = farm.TryAcquire(owner, plot, FarmAction.Water); Require(watering != null && farm.Begin(watering, owner, plot.WorkCell), "Begin watering when drinking stock is not needed");
            crew.Primary.Needs.SetHydration(10); Require(!farm.IsValid(watering, owner), "New drinking demand invalidates a reserved watering action"); farm.Cancel(watering, owner);
            var drink = crew.Primary.Needs.TryBeginDrink(owner); Require(drink != null && stock.GetReserved(ResourceType.Water) == 1, "Released irrigation water can immediately satisfy drinking"); crew.Primary.Needs.CancelDrink(drink, owner);
            crew.Primary.Needs.SetHydration(100); watering = farm.TryAcquire(owner, plot, FarmAction.Water); farm.Begin(watering, owner, plot.WorkCell);
            farm.Advance(watering, owner, plot.WorkCell, .3f); farm.Cancel(watering, owner); Require(plot.CropStage == CropStage.Planted && stock.GetAvailable(ResourceType.Water) == 1, "Canceled watering keeps the seedling and releases water");
            farm.SetRepeat(plot, false); Do(plot, FarmAction.Water); Require(plot.CropStage == CropStage.Growing && stock.GetTotal(ResourceType.Water) == 0, "Stopping repeat still waters the existing crop");
            farm.Tick(30); Require(Near(plot.GrowthProgress, .5f), "Half a minute advances half a crop"); farm.Tick(0); Require(Near(plot.GrowthProgress, .5f), "Paused passive growth has no effect");
            Reset(2, new ResourceCost(100, 100, 0, 0, 1, 1)); plot = Complete(new Vector2Int(-1, 0), FacilityKind.Planter, 1); Do(plot, FarmAction.Plant);
            crew.Members[1].Needs.SetHydration(10); Require(farm.TryAcquire(owner, plot, FarmAction.Water) == null, "Watering also protects the other sailor's drinking demand");
        }
        private static void TestCrewCoordination()
        {
            Reset(2, new ResourceCost(100, 100, 0, 0, 2, 2)); var a = Complete(new Vector2Int(-1, 1), FacilityKind.Planter, 2); var b = Complete(new Vector2Int(1, 1), FacilityKind.Planter, 2);
            crew.Members[0].RestorePosition(a.WorkCell); crew.Members[1].RestorePosition(b.WorkCell); crew.Tick(.05f);
            Require(crew.Members[0].Activity == CrewActivity.Planting && crew.Members[1].Activity == CrewActivity.Planting && stock.GetReserved(ResourceType.Potato) == 2, "Two plots with separate access can be planted simultaneously with separate seeds");
            StepUntil(() => a.CropStage == CropStage.Growing && b.CropStage == CropStage.Growing, 250);
            Require(stock.GetTotal(ResourceType.Potato) == 0 && stock.GetTotal(ResourceType.Water) == 0 && stock.ReservationCount == 0, "Two farmers consume exactly two seeds and two irrigation units");
            farm.SetRepeat(a, false); farm.SetRepeat(b, false); farm.Tick(60); StepUntil(() => a.CropStage == CropStage.Empty && b.CropStage == CropStage.Empty, 180);
            Require(stock.GetTotal(ResourceType.Potato) == 6, "Parallel harvests award each crop once");
            Reset(2, new ResourceCost(100, 100, 0, 0, 2, 2)); Complete(new Vector2Int(-1, 0), FacilityKind.Planter, 1); Complete(new Vector2Int(1, 0), FacilityKind.Planter, 3); crew.Tick(.05f);
            Require(stock.GetReserved(ResourceType.Potato) == 1 && crew.Members[0].Activity == CrewActivity.Planting && crew.Members[1].Activity != CrewActivity.Planting, "Two boxes sharing one access cell cannot be worked simultaneously");
        }
        private static void TestMoveAndSaves()
        {
            Reset(1, new ResourceCost(100, 100, 0, 0, 2, 2)); var plot = Complete(new Vector2Int(-1, 1), FacilityKind.Planter, 2); Do(plot, FarmAction.Plant); Do(plot, FarmAction.Water); farm.SetRepeat(plot, false); farm.Tick(15);
            Require(game.Facilities.Select(plot) && game.Facilities.BeginMove(), "Start moving a growing crop"); farm.Tick(10);
            Require(Near(plot.GrowthProgress, .25f) && game.Saves.Save(), "Moving preview pauses growth and can be saved"); var preview = game.Saves.Capture();
            Require(preview.Facilities[0].Coordinate == new Vector2Int(-1, 1) && Near(preview.Facilities[0].Crop.Progress, .25f), "Preview captures committed box location and crop progress");
            game.Facilities.Rotate(); Require(game.Facilities.ConfirmMove(new Vector2Int(1, 1)) && Near(plot.GrowthProgress, .25f), "Free rotation and movement keep the crop intact");
            Require(game.Saves.Load() && build.PlanterCount == 1, "Load original growing box"); plot = build.Grills[new Vector2Int(-1, 1)];
            Require(plot.CropStage == CropStage.Growing && Near(plot.GrowthProgress, .25f) && !plot.RepeatPlanting && !plot.IsBeingMoved && plot.CropView.Plants.gameObject.activeSelf, "Crop, repeat setting and visuals restore without stale move state");
            var saved = game.Saves.Capture(); Reject(saved, d => d.Facilities[0].Crop.Stage = (CropStage)99); Reject(saved, d => d.Facilities[0].Crop.Progress = float.NaN);
            Reject(saved, d => d.Facilities[0].Crop.Stage = CropStage.Empty); Reject(saved, d => d.Facilities[0].Crop.Stage = CropStage.Mature);
            Reject(saved, d => d.Facilities[0].Complete = false); Reject(saved, d => d.Facilities[0].HasCrop = false); Reject(saved, d => d.Inventory.Potato = -1); Reject(saved, d => d.Inventory.BakedPotato = -1);
            var bad = Clone(saved); bad.Facilities[0].Crop.Progress = -1; File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(bad));
            Require(!game.Saves.Load() && Near(plot.GrowthProgress, .25f), "Malformed crop rejects before mutating live state");
            Reset(1, new ResourceCost(100, 100, 0, 0, 1, 1)); plot = Complete(new Vector2Int(-1, 0), FacilityKind.Planter, 1); Do(plot, FarmAction.Plant);
            object owner = new object(); var water = farm.TryAcquire(owner, plot, FarmAction.Water); farm.Begin(water, owner, plot.WorkCell); Require(game.Saves.Save(), "Save while irrigation water is reserved");
            farm.Advance(water, owner, plot.WorkCell, 10); Require(game.Saves.Load(), "Restore unfinished watering"); plot = build.Grills[new Vector2Int(-1, 0)];
            Require(plot.CropStage == CropStage.Planted && stock.GetTotal(ResourceType.Water) == 1 && stock.ReservationCount == 0 && stock.GetTotal(ResourceType.Potato) == 0, "Load releases task reservations without refunding consumed seed or duplicating water");
            Complete(new Vector2Int(1, 1), FacilityKind.Grill, 2); Complete(new Vector2Int(1, -1), FacilityKind.Plant, 0);
            var mixed = game.Saves.Capture(); Require(game.Saves.Validate(Clone(mixed), out _) && game.Saves.Save() && game.Saves.Load() && build.PlanterCount == 1 && build.GrillCount == 1 && build.DecorationCount == 1, "Mixed crop and ordinary facilities roundtrip with explicit crop-presence flags");
            Reject(mixed, d => d.Facilities.Find(f => f.Kind == FacilityKind.Grill).HasCrop = true);
            Reset(1, new ResourceCost(20, 10)); var legacy = game.Saves.Capture(); legacy.Version = 5; crew.Primary.Needs.SetEnergy(44); legacy.CrewMembers[0].Energy = 44;
            File.WriteAllText(game.Saves.ManualPath, JsonUtility.ToJson(legacy)); Require(game.Saves.Load() && game.Saves.Capture().Version == 6 && crew.Primary.Needs.Energy == 44 && stock.GetTotal(ResourceType.Potato) == 0, "Version five migrates safely without repeatedly gifting starter seeds");
        }
        private static void TestOverflow()
        {
            Reset(1, new ResourceCost(100, 100, 0, 0, 1, 1)); var plot = Complete(new Vector2Int(-1, 0), FacilityKind.Planter, 1); Do(plot, FarmAction.Plant); Do(plot, FarmAction.Water); farm.Tick(60); farm.SetRepeat(plot, false);
            stock.RestoreInventory(new ResourceCost(0, 0, 0, 0, 0, int.MaxValue)); Require(farm.TryAcquire(new object(), plot, FarmAction.Harvest) == null && plot.CropStage == CropStage.Mature, "Full potato stock keeps mature crops safely in the box");
            stock.RestoreInventory(new ResourceCost(0, 0, 0, 0, 0, int.MaxValue - 3)); object owner = new object(); var harvest = farm.TryAcquire(owner, plot, FarmAction.Harvest); Require(harvest != null && farm.Begin(harvest, owner, plot.WorkCell), "Reserve mature box with output space");
            stock.Add(ResourceType.Potato, 1); Require(!farm.Advance(harvest, owner, plot.WorkCell, 10) && plot.CropStage == CropStage.Mature, "Later output overflow cannot erase the crop"); farm.Cancel(harvest, owner);
            stock.RestoreInventory(default); harvest = farm.TryAcquire(owner, plot, FarmAction.Harvest); farm.Begin(harvest, owner, plot.WorkCell); bool reentered = false, coherent = false;
            Action changed = () => { coherent = plot.CropStage == CropStage.Empty && stock.GetTotal(ResourceType.Potato) == 3 && game.Saves.Validate(game.Saves.Capture(), out _); reentered |= farm.Advance(harvest, owner, plot.WorkCell, 10); };
            stock.Changed += changed; Require(farm.Advance(harvest, owner, plot.WorkCell, 10), "Harvest into available stock"); stock.Changed -= changed;
            Require(coherent && !reentered && stock.GetTotal(ResourceType.Potato) == 3 && !farm.Advance(harvest, owner, plot.WorkCell, 10), "Harvest callbacks observe one coherent reward and cannot credit it twice");
        }
        private static void TestFood()
        {
            Reset(1); var plot = Complete(new Vector2Int(-1, 1), FacilityKind.Planter, 2); var a = Complete(new Vector2Int(1, 1), FacilityKind.Grill, 2); var b = Complete(new Vector2Int(1, -1), FacilityKind.Grill, 3);
            stock.RestoreInventory(new ResourceCost(5, 0, 0, 0, 0, 1)); cooking.SetTarget(2);
            Require(cooking.TryAcquire(new object(), a) == null && farm.SeedReserve == 1, "Potato cooking preserves the final seed for enabled planters"); stock.Add(ResourceType.Potato, 2);
            object oa = new object(), ob = new object(); var sa = cooking.TryAcquire(oa, a); var sb = cooking.TryAcquire(ob, b);
            Require(sa != null && sb != null && sa.Output == ResourceType.BakedPotato && sb.Output == ResourceType.BakedPotato && cooking.Begin(sa, oa, a.WorkCell) && cooking.Begin(sb, ob, b.WorkCell), "Two grills can reserve separate potato meals within one shared food target");
            Require(stock.GetAvailable(ResourceType.Potato) == 1 && a.View.PotatoFood.activeSelf && b.View.PotatoFood.activeSelf, "Potato food models appear and one seed remains unreserved");
            cooking.Advance(sa, oa, a.WorkCell, 100); cooking.Advance(sb, ob, b.WorkCell, 100);
            Require(stock.GetTotal(ResourceType.BakedPotato) == 2 && stock.GetTotal(ResourceType.Potato) == 1 && stock.GetTotal(ResourceType.Wood) == 3 && cooking.FoodCount == 2 && cooking.TryAcquire(oa, a) == null, "Baked outputs, fuel and combined food target settle once");
            var needs = crew.Primary.Needs; needs.SetSatiety(20); var meal = needs.TryBeginMeal(oa);
            Require(meal != null && meal.Food == ResourceType.BakedPotato && !needs.Advance(meal, ob, 100) && needs.Advance(meal, oa, 100) && needs.Satiety == 55 && stock.GetTotal(ResourceType.BakedPotato) == 1, "Baked potato satisfies hunger using the same owned meal lifecycle");
            stock.Add(ResourceType.Fish, 2); var fish = cooking.TryAcquire(oa, a); Require(fish != null && fish.Output == ResourceType.CookedFish && cooking.Begin(fish, oa, a.WorkCell) && cooking.Advance(fish, oa, a.WorkCell, 100) && cooking.FoodCount == 2, "Fish fallback replenishes the shared target without consuming the protected seed"); Invariant();
        }
        private static async Task TestActualFood()
        {
            Reset(1); var plot = Complete(new Vector2Int(-1, 1), FacilityKind.Planter, 2); farm.SetRepeat(plot, false); var grill = Complete(new Vector2Int(1, 1), FacilityKind.Grill, 2);
            stock.RestoreInventory(new ResourceCost(5, 0, 0, 0, 2, 2)); cooking.SetTarget(1); crew.Primary.RestorePosition(grill.WorkCell); crew.Primary.Needs.SetSatiety(20);
            game.enabled = true; game.TimeControl.SetSpeed(4); await Until(() => crew.Activity == CrewActivity.Cooking && crew.WorkProgress > .2f, 10); game.TimeControl.SetPaused(true);
            Require(crew.StatusText.Contains("烤土豆") && grill.View.PotatoFood.activeSelf, "Real crew selects and presents potato recipe"); game.Facilities.Select(grill); await Until(() => ui.IsOpen(UICommon_Facility.Address), 5); Capture("Farming_Baking", 1600, 900);
            game.TimeControl.SetSpeed(4); await Until(() => crew.Activity == CrewActivity.Eating, 8); game.TimeControl.SetPaused(true);
            Require(crew.Worker.GetComponent<CrewView>().MealPotato.activeSelf && stock.GetTotal(ResourceType.BakedPotato) == 1 && stock.GetReserved(ResourceType.BakedPotato) == 1, "Real meal presents baked potato while reserving it until eaten"); Capture("Farming_Meal", 1600, 900);
            game.TimeControl.SetSpeed(4); await Until(() => crew.Primary.Needs.Satiety > 50, 5); game.TimeControl.SetPaused(true); game.enabled = false;
            Require(stock.GetTotal(ResourceType.Potato) == 1 && stock.GetTotal(ResourceType.BakedPotato) == 0 && stock.GetTotal(ResourceType.Wood) == 4, "Actual cooking and eating complete for one potato and one fuel without fish");
            game.Facilities.ClearSelection(); game.Facilities.Select(plot); await Until(() => ui.IsOpen(UICommon_Facility.Address), 5); var panel = (UICommon_Facility)ui.GetView(UICommon_Facility.Address);
            Require(panel.TargetText.text.Contains("关") && panel.TargetPlusButton.GetComponentInChildren<Text>().text == "种", "Planter panel restores its repeat toggle after viewing a grill");
            ui.Close(UICommon_Facility.Address); await ui.OpenAsync(UICommon_Facility.Address); panel.TargetPlusButton.onClick.Invoke(); Require(plot.RepeatPlanting, "Cached planter toggle binds one handler"); panel.TargetPlusButton.onClick.Invoke(); Require(!plot.RepeatPlanting, "Repeat can be disabled again");
        }
        private static void Reset(int count, ResourceCost? inventory = null)
        {
            game.CloseComfortPanel(); game.CloseCrewPanel(); game.Facilities.ClearSelection(); crew.CancelAssignments();
            var decks = new List<DeckSave>(); for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) decks.Add(new DeckSave { X = x, Z = z, Complete = true, Progress = 1 });
            build.CatalogOpen = false; build.SetTool(BuildTool.None); build.RestoreUnlocks(false); build.RestoreLayout(decks, new List<FacilitySave>());
            stock.RestoreInventory(inventory ?? new ResourceCost(200, 100)); var entries = new List<CrewSave>(); for (int i = 0; i < count; i++) entries.Add(new CrewSave { Id = i + 1, Name = i == 0 ? "阿航" : "小澜", X = 0, Z = -i, Satiety = 100, Hydration = 100, Energy = 85 }); crew.RestoreMembers(entries);
            game.Recruitment.Restore(new RescueSave()); UnityEngine.Object.FindObjectOfType<SalvageManager>().RestoreItems(new List<FloatingSave>(), 1000, 41, 0); UnityEngine.Object.FindObjectOfType<FishingManager>().SetAutomaticFishing(false); cooking.SetTarget(0); game.Water.SetTarget(0);
        }
        private static GrillCell Complete(Vector2Int cell, FacilityKind kind, int rotation)
        { Require(build.TryPlaceFacilityBlueprint(cell, rotation, kind, out _) && build.TryBeginGrill(cell) && build.TryCompleteGrill(cell), "Construct fixture " + kind); return build.Grills[cell]; }
        private static void Do(GrillCell cell, FarmAction action)
        { object owner = new object(); var session = farm.TryAcquire(owner, cell, action); Require(session != null && farm.Begin(session, owner, cell.WorkCell) && farm.Advance(session, owner, cell.WorkCell, 100), "Complete farm action " + action); }
        private static void Invariant()
        { for (int index = 0; index < ResourceManager.TypeCount; index++) { var type = (ResourceType)index; Require(stock.GetTotal(type) >= 0 && stock.GetAvailable(type) >= 0 && stock.GetTotal(type) == (long)stock.GetAvailable(type) + stock.GetReserved(type), "Seven-resource inventory invariant for " + type); } }
        private static SaveData Clone(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        private static void Reject(SaveData original, Action<SaveData> mutate) { var data = Clone(original); mutate(data); Require(!game.Saves.Validate(data, out _), "Malformed crop or inventory rejected"); }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
        private static void StepUntil(Func<bool> condition, int limit)
        { for (int i = 0; i < limit && !condition(); i++) { farm.Tick(.05f); crew.Tick(.05f); foreach (var worker in crew.Members) Require(DeckPathfinder.IsWalkable(build.Cells, build.WorldToCell(worker.Worker.position)), "Farmer stays on safe completed access"); } Require(condition(), "Task transition: " + crew.StatusText); }
        private static void Capture(string name, int width, int height)
        { VisualPreviewCapture.Capture(Camera.main, ui.GetComponent<Canvas>(), hud, "Docs/Previews/" + name + ".png", width, height, () => { for (int pass = 0; pass < 2; pass++) { Camera.main.Render(); foreach (var graphic in ui.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty(); Canvas.ForceUpdateCanvases(); } }); }
        private static async Task Until(Func<bool> condition, double seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (!condition()) { Require(EditorApplication.timeSinceStartup < end, "Timed out waiting for farm/UI"); await NextFrame(); } }
        private static async Task RealWait(double seconds) { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) await NextFrame(); }
        private static Task NextFrame() { var t = new TaskCompletionSource<bool>(); EditorApplication.delayCall += () => t.TrySetResult(true); return t.Task; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Farming validation: " + message); checks++; }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Wait; EditorApplication.playModeStateChanged += state =>
            { if (state != PlayModeStateChange.EnteredEditMode) return; AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = 0; AssetDatabase.SaveAssets(); Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); if (Application.isBatchMode) EditorApplication.Exit(code); }; EditorApplication.ExitPlaymode();
        }
    }
}
