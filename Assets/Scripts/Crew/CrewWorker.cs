using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;
using RaftGame.Farming;
using RaftGame.Resources;

namespace RaftGame.Crew
{
    /// <summary>Per-person task execution, initialized and ticked by CrewManager.</summary>
    public sealed class CrewWorker
    {
        private enum WorkKind { None, Rescue, Gathering, Construction, Water, Cooking, Fishing, Farming }
        private CrewManager roster;
        private WorkKind workKind;
        private RescueSession rescueSession;
        private RestSession restSession;
        private FarmSession farmSession;
        private int observedFarming = -1;
        private int observedRoster = -1, observedRescue = -1;
        public int Id { get; }
        public string DisplayName { get; private set; }
        public WorkPreference Preference { get; private set; }
        public CrewNeeds Needs => needs;
        public CrewWorker(int id, string name, WorkPreference preference) { Id = id; DisplayName = name; Preference = preference; }
        public void RestoreIdentity(string name, WorkPreference preference) { DisplayName = name; Preference = preference; }
        public void SetPreference(WorkPreference preference)
        {
            if (Preference == preference) return;
            Preference = preference;
            if (mealSession == null && drinkSession == null && restSession == null) ReleaseTask();
            Changed?.Invoke();
        }
        private CrewConfig configuration;
        private BuildManager building;
        private CrewView view;
        private DeckCell task;
        private List<Vector2Int> route;
        private int waypoint;
        private float retryTime;
        private int observedLayout = -1;
        private string previousStatus;
        private Vector2Int previousCell;
        private bool ownsConstruction;
        private SalvageManager salvage;
        private FloatingResource salvageTask;
        private float gatherProgress;
        private int observedInventory = -1;
        private int observedSalvage = -1;
        private FishingManager fishing;
        private FishingSession fishingSession;
        private int observedFishing = -1;
        private ProductionManager production;
        private CrewNeeds needs;
        private GrillCell grillTask;
        private CookingSession cookingSession;
        private MealSession mealSession;
        private int observedProduction = -1;
        private int observedNeeds = -1;
        private WaterManager water;
        private PurifyingSession purifyingSession;
        private DrinkSession drinkSession;
        private int observedWater = -1;
        private CollectionNetManager nets;
        private NetCollectionSession netSession;
        private int observedNets = -1;

        public bool IsInitialized { get; private set; }
        public CrewActivity Activity { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        public Vector2Int? TaskCell => farmSession?.Planter.Coordinate ?? restSession?.Bed.Coordinate ?? task?.Coordinate ?? grillTask?.Coordinate ?? cookingSession?.Grill.Coordinate ?? purifyingSession?.Purifier.Coordinate ?? netSession?.Net.Coordinate;
        public float WorkProgress => farmSession != null ? farmSession.Progress : restSession != null ? restSession.Progress : rescueSession != null ? rescueSession.Progress : netSession != null ? netSession.Progress : drinkSession != null ? drinkSession.Progress : purifyingSession != null ? purifyingSession.Progress : mealSession != null ? mealSession.Progress : cookingSession != null ? cookingSession.Progress :
            fishingSession != null ? fishingSession.Progress : salvageTask != null ? gatherProgress :
            grillTask != null ? grillTask.ConstructionProgress : task != null ? task.ConstructionProgress : 0f;
        public Transform Worker => view != null ? view.transform : null;
        public event Action Changed;

        public string StatusText => Activity == CrewActivity.Constructing ? $"船员：正在施工 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Rescuing ? $"船员：救援中 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.Resting ? $"船员：床上休息 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.Gathering ? $"船员：正在打捞 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Fishing ? $"船员：垂钓中 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Planting ? $"船员：播种中 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.Watering ? $"船员：浇水中 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.Harvesting ? $"船员：收获土豆 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.Cooking ? $"船员：{(cookingSession != null && cookingSession.Output == ResourceType.BakedPotato ? "烤土豆" : "烤鱼")}中 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Eating ? (mealSession != null && mealSession.Food == ResourceType.BakedPotato ? "船员：享用烤土豆" : "船员：享用烤鱼") :
            Activity == CrewActivity.Drinking ? "船员：饮用淡水" :
            Activity == CrewActivity.Purifying ? $"船员：净水中 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.CollectingNet ? $"船员：收网中 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.Walking ? (farmSession != null ? "船员：前往种植箱" : restSession != null ? "船员：前往床铺" : rescueSession != null ? "船员：前往救援点" : netSession != null ? "船员：前往收集网" : purifyingSession != null ? "船员：前往净水器" : cookingSession != null ? "船员：前往烤架" : fishingSession != null ? "船员：前往钓鱼点" : salvageTask != null ? "船员：前往打捞点" : task != null || grillTask != null ? "船员：前往施工点" : "船员：返回甲板") :
            Activity == CrewActivity.WaitingForMaterials ? "船员：等待材料" :
            Activity == CrewActivity.WaitingForSalvage ? "船员：等待可打捞物资" :
            Activity == CrewActivity.WaitingForAccess ? "船员：等待通路" : "船员：待命";

        internal bool Initialize(CrewManager coordinator, CrewConfig config, BuildManager buildManager, Camera camera, SalvageManager salvageManager = null,
            FishingManager fishingManager = null, ProductionManager productionManager = null, CrewNeeds needsManager = null, WaterManager waterManager = null, CollectionNetManager netManager = null)
        {
            if (IsInitialized) return true;
            if (config == null || config.CrewPrefab == null || config.CrewPrefab.GetComponent<CrewView>() == null ||
                buildManager == null || !buildManager.IsInitialized)
            {
                Debug.LogError("CrewManager requires CrewConfig, a CrewView prefab and an initialized BuildManager.", coordinator);
                return false;
            }
            roster = coordinator;
            configuration = config;
            building = buildManager;
            salvage = salvageManager;
            fishing = fishingManager;
            production = productionManager;
            needs = needsManager;
            water = waterManager;
            nets = netManager;
            CurrentCell = config.SpawnCell;
            if (!DeckPathfinder.IsWalkable(building.Cells, CurrentCell))
            {
                bool found = false;
                foreach (DeckCell cell in building.Cells.Values)
                    if (DeckPathfinder.IsWalkable(building.Cells, cell.Coordinate) && (!found || Earlier(cell.Coordinate, CurrentCell)))
                    { found = true; CurrentCell = cell.Coordinate; }
                if (!found) return false;
            }
            view = UnityEngine.Object.Instantiate(config.CrewPrefab, roster.transform).GetComponent<CrewView>();
            view.name = "Sailor";
            view.transform.position = FootPosition(CurrentCell);
            view.Initialize(camera);
            view.ApplyCrewColor(Id);
            building.FacilityChanging += InterruptFacility;
            Activity = CrewActivity.Idle;
            IsInitialized = true;
            PublishStatus();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsInitialized) return;
            float dt = Mathf.Max(0f, deltaTime);
            // Turning fishing off or planning over the casting water cancels gear even while paused.
            if (fishingSession != null && !fishing.IsSessionValid(fishingSession, this)) ReleaseTask();
            if (rescueSession != null && !roster.Recruitment.IsValid(rescueSession, this)) ReleaseTask();
            if (restSession != null && !roster.Rest.IsValid(restSession, this)) ReleaseTask();
            if (farmSession != null && !roster.Farming.IsValid(farmSession, this)) ReleaseTask();
            if (dt > 0f)
            {
                if (Activity != CrewActivity.Resting) needs?.TickEnergy(dt, HasAssignment() || Activity == CrewActivity.Walking);
                if (mealSession == null && drinkSession == null && needs != null && (needs.ShouldEat || needs.ShouldDrink) && HasAssignment()) ReleaseTask();
                if (restSession == null && mealSession == null && drinkSession == null && roster.Rest != null && roster.Rest.NeedsRest(needs) && HasAssignment())
                {
                    FindWork(out _, out var restingDistances);
                    if (roster.Rest.TryFindBed(restingDistances, needs, out _)) ReleaseTask();
                }
                if (task != null && (!building.Cells.TryGetValue(task.Coordinate, out DeckCell current) ||
                    !ReferenceEquals(task, current) || current.State != (ownsConstruction ? DeckState.Constructing : DeckState.Planned)))
                    ReleaseTask();
                if (grillTask != null && (!building.Grills.TryGetValue(grillTask.Coordinate, out GrillCell currentGrill) ||
                    !ReferenceEquals(grillTask, currentGrill) || currentGrill.State != (ownsConstruction ? DeckState.Constructing : DeckState.Planned))) ReleaseTask();
                if (!ReferenceEquals(salvageTask, null) && !salvage.IsClaimValid(salvageTask, this)) ReleaseTask();
                if (cookingSession != null && !production.IsValid(cookingSession, this)) ReleaseTask();
                if (purifyingSession != null && !water.IsValid(purifyingSession, this)) ReleaseTask();
                if (netSession != null && !nets.IsValid(netSession, this)) ReleaseTask();
                if ((fishingSession != null || cookingSession != null || purifyingSession != null) && WorkChanged())
                {
                    Observe();
                    if (HasPriorityWork()) ReleaseTask();
                }

                float workTime = dt * (needs != null ? needs.Efficiency : 1f);
                if (Activity == CrewActivity.Walking) MoveAlongRoute(workTime);
                else if (Activity == CrewActivity.Constructing) Construct(workTime);
                else if (Activity == CrewActivity.Gathering) Gather(workTime);
                else if (Activity == CrewActivity.Fishing) Fish(workTime);
                else if (Activity == CrewActivity.Cooking) Cook(workTime);
                else if (Activity == CrewActivity.Eating) Eat(dt);
                else if (Activity == CrewActivity.Drinking) Drink(dt);
                else if (Activity == CrewActivity.Purifying) Purify(workTime);
                else if (Activity == CrewActivity.CollectingNet) CollectNet(workTime);
                else if (Activity == CrewActivity.Rescuing) Rescue(workTime);
                else if (Activity == CrewActivity.Resting) Rest(dt);
                else if (Activity == CrewActivity.Planting || Activity == CrewActivity.Watering || Activity == CrewActivity.Harvesting) Farm(workTime);
                else
                {
                    retryTime -= dt;
                    if (retryTime <= 0f || WorkChanged() || (fishing != null && observedFishing != fishing.Version) ||
                        (needs != null && observedNeeds != needs.Version)) SelectTask();
                }
            }
            if (!HasAssignment() && Activity != CrewActivity.Walking) { roster.ReleaseClaims(this); workKind = WorkKind.None; }
            Vector3 workPosition = farmSession != null ? building.CellToWorld(farmSession.Planter.Coordinate) + Vector3.up * .25f : rescueSession != null ? roster.Recruitment.Position : netSession != null ? building.CellToWorld(netSession.Net.MouthCell) : drinkSession != null || mealSession != null ? view.transform.position + Vector3.up :
                purifyingSession != null ? building.CellToWorld(purifyingSession.Purifier.Coordinate) + Vector3.up * 0.7f :
                cookingSession != null ? building.CellToWorld(cookingSession.Grill.Coordinate) + Vector3.up * 0.7f :
                fishingSession != null ? fishing.GetWaterPoint(fishingSession.Spot) :
                salvageTask != null ? salvageTask.transform.position : grillTask != null ? building.CellToWorld(grillTask.Coordinate) :
                task != null ? building.CellToWorld(task.Coordinate) : Vector3.zero;
            view.Present(Activity, WorkProgress, dt, workPosition, mealSession != null ? mealSession.Food : ResourceType.CookedFish);
            if (Activity == CrewActivity.Resting && restSession != null) view.PresentRest(restSession.Bed.BedView);
            PublishStatus();
        }

        private Vector3 FootPosition(Vector2Int cell) => building.CellToWorld(cell) + Vector3.up * configuration.FootOffset + (Id == 1 ? Vector3.zero : new Vector3(.36f, 0, -.28f));
        private static bool Earlier(Vector2Int a, Vector2Int b) => a.x < b.x || (a.x == b.x && a.y < b.y);

        private bool HasAssignment() => farmSession != null || restSession != null || rescueSession != null || task != null || grillTask != null || salvageTask != null || fishingSession != null || cookingSession != null || purifyingSession != null || netSession != null;
        private bool WorkChanged() => observedLayout != building.LayoutVersion || observedInventory != building.InventoryVersion ||
            (salvage != null && observedSalvage != salvage.Version) || (production != null && observedProduction != production.Version) || (water != null && observedWater != water.Version) || (nets != null && observedNets != nets.Version) || observedRoster != roster.Version ||
            (roster.Recruitment != null && observedRescue != roster.Recruitment.Version) || (roster.Farming != null && observedFarming != roster.Farming.Version);
        private void Observe()
        {
            observedLayout = building.LayoutVersion; observedInventory = building.InventoryVersion;
            observedSalvage = salvage != null ? salvage.Version : 0;
            observedFishing = fishing != null ? fishing.Version : 0;
            observedProduction = production != null ? production.Version : 0;
            observedNeeds = needs != null ? needs.Version : 0;
            observedWater = water != null ? water.Version : 0;
            observedNets = nets != null ? nets.Version : 0;
            observedRoster = roster.Version; observedRescue = roster.Recruitment != null ? roster.Recruitment.Version : 0;
            observedFarming = roster.Farming != null ? roster.Farming.Version : 0;
        }

        private void SelectTask()
        {
            retryTime = Mathf.Max(.1f, configuration.RetryInterval); Observe();
            if (needs != null && needs.ShouldDrink && needs.Hydration <= needs.Satiety && (drinkSession = needs.TryBeginDrink(this)) != null)
            { Activity = CrewActivity.Drinking; return; }
            if (needs != null && (mealSession = needs.TryBeginMeal(this)) != null) { Activity = CrewActivity.Eating; return; }
            if (needs != null && (drinkSession = needs.TryBeginDrink(this)) != null) { Activity = CrewActivity.Drinking; return; }
            FindWork(out var parents, out var distances);
            if (TryRest(parents, distances)) return;
            if (TryRescue(parents, distances)) return;
            if (Preference == WorkPreference.BuildingProductionFirst)
            {
                if (TryConstruction(parents, distances) || TryProduction(parents, distances) || TryGathering(parents, distances) || TryFishing(parents, distances)) return;
            }
            else if (Preference == WorkPreference.GatheringFirst)
            {
                if (TryGathering(parents, distances) || TryFishing(parents, distances) || TryConstruction(parents, distances) || TryProduction(parents, distances)) return;
            }
            else if (TryGathering(parents, distances) || TryConstruction(parents, distances) || TryProduction(parents, distances) || TryFishing(parents, distances)) return;
            Activity = building.BlueprintCount > 0 ? (HasAffordableBlueprint() ? CrewActivity.WaitingForAccess : CrewActivity.WaitingForMaterials) :
                salvage != null && salvage.MarkedCount > 0 ? CrewActivity.WaitingForSalvage : CrewActivity.Idle;
        }
        private void FindWork(out Dictionary<Vector2Int, Vector2Int> parents, out Dictionary<Vector2Int, int> distances)
        {
            DeckPathfinder.FindReachable(building.Cells, CurrentCell, out parents, out var reachable);
            distances = new Dictionary<Vector2Int, int>();
            foreach (var entry in reachable) if (roster.StationAvailable(this, entry.Key)) distances.Add(entry.Key, entry.Value);
        }
        private bool TryRest(Dictionary<Vector2Int, Vector2Int> parents, Dictionary<Vector2Int, int> distances)
        {
            var rest = roster.Rest;
            if (rest == null || !rest.TryFindBed(distances, needs, out var bed) || !roster.TryClaim(this, bed, bed.WorkCell)) return false;
            restSession = rest.TryAcquire(this, bed, needs);
            if (restSession == null) { roster.ReleaseClaims(this); return false; }
            BeginRoute(parents, bed.WorkCell); return true;
        }
        private void Rest(float deltaTime)
        {
            var session = restSession;
            if (!roster.Rest.Advance(session, this, CurrentCell, deltaTime)) { ReleaseTask(); return; }
            if (session.Progress < 1) return;
            restSession = null; Activity = CrewActivity.Idle; retryTime = 0;
        }
        private bool TryRescue(Dictionary<Vector2Int, Vector2Int> parents, Dictionary<Vector2Int, int> distances)
        {
            var rescue = roster.Recruitment;
            if (rescue == null || !rescue.TryFindTask(distances, out var approach) || !roster.TryClaim(this, rescue, approach)) return false;
            rescueSession = rescue.TryAcquire(this, approach);
            if (rescueSession == null) { roster.ReleaseClaims(this); return false; }
            workKind = WorkKind.Rescue; BeginRoute(parents, approach); return true;
        }
        private bool TryGathering(Dictionary<Vector2Int, Vector2Int> parents, Dictionary<Vector2Int, int> distances)
        {
            if (salvage != null && salvage.TryFindTask(distances, this, out var item, out var approach) && salvage.TryClaim(item, this))
            {
                if (roster.TryClaim(this, item, approach))
                { salvageTask = item; gatherProgress = 0; workKind = WorkKind.Gathering; BeginRoute(parents, approach); return true; }
                salvage.ReleaseClaim(item, this);
            }
            if (nets != null && nets.TryFindTask(distances, out var net) && roster.TryClaim(this, net, net.WorkCell))
            {
                netSession = nets.TryAcquire(this, net);
                if (netSession != null) { workKind = WorkKind.Gathering; BeginRoute(parents, net.WorkCell); return true; }
                roster.ReleaseClaims(this);
            }
            return TryFarming(parents, distances, true);
        }
        private bool TryFarming(Dictionary<Vector2Int, Vector2Int> parents, Dictionary<Vector2Int, int> distances, bool harvestOnly = false)
        {
            var farming = roster.Farming;
            if (farming == null || !farming.TryFindTask(distances, out var planter, out var action, harvestOnly) || !roster.TryClaim(this, planter, planter.WorkCell)) return false;
            farmSession = farming.TryAcquire(this, planter, action);
            if (farmSession == null) { roster.ReleaseClaims(this); return false; }
            workKind = action == FarmAction.Harvest ? WorkKind.Gathering : WorkKind.Farming; BeginRoute(parents, planter.WorkCell); return true;
        }
        private void Farm(float deltaTime)
        {
            var session = farmSession;
            if (!roster.Farming.Advance(session, this, CurrentCell, deltaTime)) { ReleaseTask(); return; }
            if (session.Progress < 1) return;
            farmSession = null; Activity = CrewActivity.Idle; retryTime = 0;
        }
        private bool TryConstruction(Dictionary<Vector2Int, Vector2Int> parents, Dictionary<Vector2Int, int> distances)
        {
            DeckCell chosen = null; GrillCell chosenGrill = null; Vector2Int destination = CurrentCell; int best = int.MaxValue;
            foreach (var candidate in building.Cells.Values)
                if (candidate.State == DeckState.Planned && building.CanAffordConstruction())
                    foreach (var direction in DeckPathfinder.Directions)
                    {
                        var approach = candidate.Coordinate + direction;
                        if (distances.TryGetValue(approach, out int distance) && roster.CanClaim(this, candidate, approach) &&
                            (distance < best || (distance == best && (chosen == null || Earlier(candidate.Coordinate, chosen.Coordinate)))))
                        { chosen = candidate; destination = approach; best = distance; }
                    }
            foreach (var candidate in building.Grills.Values)
                if (candidate.State == DeckState.Planned && building.CanAffordFacility(candidate.Kind) && distances.TryGetValue(candidate.WorkCell, out int distance) &&
                    roster.CanClaim(this, candidate, candidate.WorkCell) && (distance < best || (distance == best && chosen == null && (chosenGrill == null || Earlier(candidate.Coordinate, chosenGrill.Coordinate)))))
                { chosenGrill = candidate; chosen = null; destination = candidate.WorkCell; best = distance; }
            object key = chosen != null ? (object)chosen : chosenGrill;
            if (key == null || !roster.TryClaim(this, key, destination)) return false;
            task = chosen; grillTask = chosenGrill; workKind = WorkKind.Construction; BeginRoute(parents, destination); return true;
        }
        private bool TryProduction(Dictionary<Vector2Int, Vector2Int> parents, Dictionary<Vector2Int, int> distances)
        {
            if (TryFarming(parents, distances)) return true;
            if (water != null && water.TryFindTask(distances, out var purifier) && roster.TryClaim(this, purifier, purifier.WorkCell))
            {
                purifyingSession = water.TryAcquire(this, purifier);
                if (purifyingSession != null) { workKind = WorkKind.Water; BeginRoute(parents, purifier.WorkCell); return true; }
                roster.ReleaseClaims(this);
            }
            if (production != null && production.TryFindTask(distances, out var grill) && roster.TryClaim(this, grill, grill.WorkCell))
            {
                cookingSession = production.TryAcquire(this, grill);
                if (cookingSession != null) { workKind = WorkKind.Cooking; BeginRoute(parents, grill.WorkCell); return true; }
                roster.ReleaseClaims(this);
            }
            return false;
        }
        private bool TryFishing(Dictionary<Vector2Int, Vector2Int> parents, Dictionary<Vector2Int, int> distances)
        {
            if (fishing == null || !fishing.TryFindSpot(distances, out var spot) || !roster.TryClaim(this, fishing, spot.Cell)) return false;
            fishingSession = fishing.TryAcquire(this, spot);
            if (fishingSession == null) { roster.ReleaseClaims(this); return false; }
            workKind = WorkKind.Fishing; BeginRoute(parents, spot.Cell); return true;
        }
        private void BeginRoute(Dictionary<Vector2Int, Vector2Int> parents, Vector2Int destination)
        {
            route = DeckPathfinder.Reconstruct(parents, destination);
            waypoint = 1; // The first point is the cell already occupied by the worker.
            Activity = CrewActivity.Walking;
            if (waypoint >= route.Count) StartWork();
        }

        private bool HasAffordableBlueprint()
        {
            foreach (DeckCell cell in building.Cells.Values)
                if (cell.State == DeckState.Planned && building.CanAffordConstruction()) return true;
            foreach (GrillCell grill in building.Grills.Values)
                if (grill.State == DeckState.Planned && building.CanAffordFacility(grill.Kind)) return true;
            return false;
        }

        private void MoveAlongRoute(float deltaTime)
        {
            float remaining = Mathf.Max(0.1f, configuration.WalkSpeed) * deltaTime;
            while (route != null && waypoint < route.Count)
            {
                Vector2Int next = route[waypoint];
                if (!DeckPathfinder.IsWalkable(building.Cells, next)) { ReleaseTask(); return; }
                Vector3 goal = FootPosition(next);
                Vector3 offset = goal - view.transform.position;
                float distance = offset.magnitude;
                Face(offset, deltaTime);
                if (distance > remaining && distance > 0.001f)
                {
                    view.transform.position += offset / distance * remaining;
                    return;
                }
                view.transform.position = goal;
                CurrentCell = next;
                waypoint++;
                remaining -= distance;
            }
            if (HasAssignment()) StartWork();
            else { Activity = CrewActivity.Idle; retryTime = 0f; }
        }

        private void StartWork()
        {
            route = null;
            if (farmSession != null)
            {
                if (!roster.Farming.Begin(farmSession, this, CurrentCell)) { ReleaseTask(); return; }
                Activity = farmSession.Action == FarmAction.Plant ? CrewActivity.Planting : farmSession.Action == FarmAction.Water ? CrewActivity.Watering : CrewActivity.Harvesting;
                Face(building.CellToWorld(farmSession.Planter.Coordinate) - view.transform.position, 1); return;
            }
            if (restSession != null)
            {
                if (!roster.Rest.Begin(restSession, this, CurrentCell)) { ReleaseTask(); return; }
                Activity = CrewActivity.Resting; return;
            }
            if (rescueSession != null)
            {
                if (!roster.Recruitment.IsValid(rescueSession, this) || CurrentCell != rescueSession.Approach) { ReleaseTask(); return; }
                Activity = CrewActivity.Rescuing; Face(roster.Recruitment.Position - view.transform.position, 1); return;
            }
            if (netSession != null)
            {
                if (!nets.IsValid(netSession, this) || CurrentCell != netSession.Net.WorkCell) { ReleaseTask(); return; }
                Activity = CrewActivity.CollectingNet;
                Face(building.CellToWorld(netSession.Net.MouthCell) - view.transform.position, 1); return;
            }
            if (purifyingSession != null)
            {
                if (!water.Begin(purifyingSession, this, CurrentCell)) { ReleaseTask(); return; }
                Activity = CrewActivity.Purifying;
                Face(building.CellToWorld(purifyingSession.Purifier.Coordinate) - view.transform.position, 1f); return;
            }
            if (cookingSession != null)
            {
                if (!production.Begin(cookingSession, this, CurrentCell)) { ReleaseTask(); return; }
                Activity = CrewActivity.Cooking;
                Face(building.CellToWorld(cookingSession.Grill.Coordinate) - view.transform.position, 1f); return;
            }
            if (fishingSession != null)
            {
                if (!fishing.IsSessionValid(fishingSession, this) || CurrentCell != fishingSession.Spot.Cell) { ReleaseTask(); return; }
                Activity = CrewActivity.Fishing;
                Face(fishing.GetWaterPoint(fishingSession.Spot) - view.transform.position, 1f);
                return;
            }
            if (salvageTask != null)
            {
                if (!salvage.IsClaimValid(salvageTask, this) || !salvage.IsInReach(CurrentCell, salvageTask)) { ReleaseTask(); return; }
                Activity = CrewActivity.Gathering;
                Face(salvageTask.transform.position - view.transform.position, 1f);
                return;
            }
            if ((task != null && building.TryBeginConstruction(task.Coordinate)) || (grillTask != null && building.TryBeginGrill(grillTask.Coordinate)))
            {
                ownsConstruction = true;
                Activity = CrewActivity.Constructing;
                Face(building.CellToWorld(task != null ? task.Coordinate : grillTask.Coordinate) - view.transform.position, 1f);
            }
            else ReleaseTask();
        }

        private void Construct(float deltaTime)
        {
            if (grillTask != null)
            {
                float grillProgress = grillTask.ConstructionProgress + deltaTime / Mathf.Max(0.1f, configuration.ConstructionDuration);
                building.SetGrillProgress(grillTask.Coordinate, grillProgress);
                if (grillProgress < 1f) return;
                if (!building.TryCompleteGrill(grillTask.Coordinate)) { ReleaseTask(); return; }
                grillTask = null; ownsConstruction = false; Activity = CrewActivity.Idle; retryTime = 0f; return;
            }
            if (task == null) { ReleaseTask(); return; }
            float progress = task.ConstructionProgress + deltaTime / Mathf.Max(0.1f, configuration.ConstructionDuration);
            building.SetConstructionProgress(task.Coordinate, progress);
            if (progress < 1f) return;
            if (!building.TryCompleteConstruction(task.Coordinate)) { ReleaseTask(); return; }
            task = null;
            ownsConstruction = false;
            Activity = CrewActivity.Idle;
            retryTime = 0f;
        }

        private void Gather(float deltaTime)
        {
            if (!salvage.IsClaimValid(salvageTask, this) || !salvage.IsInReach(CurrentCell, salvageTask)) { ReleaseTask(); return; }
            Face(salvageTask.transform.position - view.transform.position, deltaTime);
            gatherProgress = Mathf.Clamp01(gatherProgress + deltaTime / Mathf.Max(0.1f, salvage.GatherDuration));
            if (gatherProgress < 1f) return;
            if (!salvage.TryCollect(salvageTask, this, CurrentCell)) { ReleaseTask(); return; }
            salvageTask = null;
            gatherProgress = 0f;
            Activity = CrewActivity.Idle;
            retryTime = 0f;
        }

        private void Fish(float deltaTime)
        {
            if (!fishing.Advance(fishingSession, this, CurrentCell, deltaTime, out _)) { ReleaseTask(); return; }
            if (fishingSession.Progress < 1f) return;
            fishingSession = null;
            Activity = CrewActivity.Idle;
            retryTime = 0f;
        }

        private int Rank(WorkKind kind)
        {
            if (kind == WorkKind.Rescue) return -1;
            if (Preference == WorkPreference.GatheringFirst)
                return kind == WorkKind.Gathering ? 0 : kind == WorkKind.Fishing ? 1 : kind == WorkKind.Construction ? 2 : kind == WorkKind.Farming ? 3 : kind == WorkKind.Water ? 4 : 5;
            if (Preference == WorkPreference.BuildingProductionFirst)
                return kind == WorkKind.Construction ? 0 : kind == WorkKind.Farming ? 1 : kind == WorkKind.Water ? 2 : kind == WorkKind.Cooking ? 3 : kind == WorkKind.Gathering ? 4 : 5;
            return kind == WorkKind.Gathering ? 0 : kind == WorkKind.Construction ? 1 : kind == WorkKind.Farming ? 2 : kind == WorkKind.Water ? 3 : kind == WorkKind.Cooking ? 4 : 5;
        }
        private bool HasPriorityWork()
        {
            FindWork(out _, out var distances);
            if (roster.Recruitment != null && roster.Recruitment.TryFindTask(distances, out _)) return true;
            int current = Rank(workKind);
            if (Rank(WorkKind.Gathering) < current)
            {
                if (salvage != null && salvage.TryFindTask(distances, this, out var item, out var approach) && roster.CanClaim(this, item, approach)) return true;
                if (nets != null && nets.TryFindTask(distances, out var net) && roster.CanClaim(this, net, net.WorkCell)) return true;
                if (roster.Farming != null && roster.Farming.TryFindTask(distances, out var crop, out _, true) && roster.CanClaim(this, crop, crop.WorkCell)) return true;
            }
            if (Rank(WorkKind.Construction) < current)
            {
                if (CanYieldFor(building.DeckCost) || building.CanAffordConstruction())
                    foreach (var candidate in building.Cells.Values) if (candidate.State == DeckState.Planned)
                        foreach (var direction in DeckPathfinder.Directions)
                        { var approach = candidate.Coordinate + direction; if (distances.ContainsKey(approach) && roster.CanClaim(this, candidate, approach)) return true; }
                foreach (var cell in building.Grills.Values)
                    if (cell.State == DeckState.Planned && distances.ContainsKey(cell.WorkCell) && roster.CanClaim(this, cell, cell.WorkCell) &&
                        (building.CanAffordFacility(cell.Kind) || CanYieldFor(building.FacilityCost(cell.Kind)))) return true;
            }
            if (Rank(WorkKind.Farming) < current && roster.Farming != null && roster.Farming.TryFindTask(distances, out var planter, out _) && roster.CanClaim(this, planter, planter.WorkCell)) return true;
            if (Rank(WorkKind.Water) < current && water != null && water.TryFindTask(distances, out var purifier) && roster.CanClaim(this, purifier, purifier.WorkCell)) return true;
            if (Rank(WorkKind.Cooking) < current && production != null && production.TryFindTask(distances, out var grill) && roster.CanClaim(this, grill, grill.WorkCell)) return true;
            if (Rank(WorkKind.Fishing) < current && fishing != null && fishing.TryFindSpot(distances, out var spot) && roster.CanClaim(this, fishing, spot.Cell)) return true;
            return false;
        }
        private void Rescue(float dt)
        {
            var session = rescueSession;
            if (!roster.Recruitment.Advance(session, this, CurrentCell, dt)) { ReleaseTask(); return; }
            if (!ReferenceEquals(session, rescueSession) || session.Progress < 1) return;
            rescueSession = null; Activity = CrewActivity.Idle; retryTime = 0;
        }
        private bool CanYieldFor(RaftGame.Resources.ResourceCost cost) =>
            (cookingSession != null && production.CanYieldFor(cookingSession, cost)) || (purifyingSession != null && water.CanYieldFor(purifyingSession, cost));
        private void Purify(float deltaTime)
        {
            if (!water.Advance(purifyingSession, this, CurrentCell, deltaTime)) { ReleaseTask(); return; }
            if (purifyingSession.Progress < 1) return;
            purifyingSession = null; Activity = CrewActivity.Idle; retryTime = 0;
        }
        private void Drink(float deltaTime)
        {
            if (!needs.AdvanceDrink(drinkSession, this, deltaTime)) { ReleaseTask(); return; }
            if (drinkSession.Progress < 1) return;
            drinkSession = null; Activity = CrewActivity.Idle; retryTime = 0;
        }

        private void Cook(float deltaTime)
        {
            if (!production.Advance(cookingSession, this, CurrentCell, deltaTime)) { ReleaseTask(); return; }
            if (cookingSession.Progress < 1f) return;
            cookingSession = null; Activity = CrewActivity.Idle; retryTime = 0f;
        }

        private void Eat(float deltaTime)
        {
            if (!needs.Advance(mealSession, this, deltaTime)) { ReleaseTask(); return; }
            if (mealSession.Progress < 1f) return;
            mealSession = null; Activity = CrewActivity.Idle; retryTime = 0f;
        }

        private void Face(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            view.transform.rotation = Quaternion.RotateTowards(view.transform.rotation, Quaternion.LookRotation(direction),
                Mathf.Max(1f, configuration.TurnSpeed) * deltaTime);
        }

        private void CollectNet(float deltaTime)
        {
            var session = netSession;
            if (!nets.Advance(session, this, CurrentCell, deltaTime)) { ReleaseTask(); return; }
            if (!ReferenceEquals(session, netSession) || session.Progress < 1) return;
            netSession = null; Activity = CrewActivity.Idle; retryTime = 0;
        }

        private void ReleaseTask()
        {
            if (farmSession != null) roster.Farming.Cancel(farmSession, this);
            farmSession = null;
            if (restSession != null) { roster.Rest.Cancel(restSession, this); view?.Wake(); }
            restSession = null;
            if (rescueSession != null) roster.Recruitment.Cancel(rescueSession, this);
            rescueSession = null; roster.ReleaseClaims(this); workKind = WorkKind.None;
            if (ownsConstruction && task != null && building.Cells.TryGetValue(task.Coordinate, out DeckCell current) && ReferenceEquals(task, current))
                building.TrySuspendConstruction(task.Coordinate);
            task = null;
            if (ownsConstruction && grillTask != null && building.Grills.TryGetValue(grillTask.Coordinate, out GrillCell grill) && ReferenceEquals(grillTask, grill))
                building.TrySuspendGrill(grillTask.Coordinate);
            grillTask = null;
            if (salvageTask != null && salvage != null) salvage.ReleaseClaim(salvageTask, this);
            salvageTask = null;
            gatherProgress = 0f;
            if (fishingSession != null && fishing != null) fishing.Cancel(fishingSession, this);
            fishingSession = null;
            if (cookingSession != null) production.Cancel(cookingSession, this);
            cookingSession = null;
            if (mealSession != null) needs.Cancel(mealSession, this);
            mealSession = null;
            if (purifyingSession != null) water.Cancel(purifyingSession, this);
            purifyingSession = null;
            if (drinkSession != null) needs.CancelDrink(drinkSession, this);
            drinkSession = null;
            if (netSession != null) nets.Cancel(netSession, this);
            netSession = null;
            ownsConstruction = false;
            retryTime = 0f;
            // If cancelled mid-step, return along that same safe segment before selecting a new route.
            if (view != null && Vector3.Distance(view.transform.position, FootPosition(CurrentCell)) > 0.001f)
            {
                route = new List<Vector2Int> { CurrentCell };
                waypoint = 0;
                Activity = CrewActivity.Walking;
            }
            else { route = null; Activity = CrewActivity.Idle; }
        }

        private void InterruptFacility(GrillCell cell)
        {
            if (ReferenceEquals(grillTask, cell) || (cookingSession != null && ReferenceEquals(cookingSession.Grill, cell)) ||
                (purifyingSession != null && ReferenceEquals(purifyingSession.Purifier, cell)) || (netSession != null && ReferenceEquals(netSession.Net, cell)) || (restSession != null && ReferenceEquals(restSession.Bed, cell)) || (farmSession != null && ReferenceEquals(farmSession.Planter, cell)))
            { ReleaseTask(); view.Present(Activity, WorkProgress, 0, Vector3.zero); PublishStatus(); }
        }

        private void PublishStatus()
        {
            string status = StatusText;
            if (status == previousStatus && CurrentCell == previousCell) return;
            previousStatus = status; previousCell = CurrentCell;
            Changed?.Invoke();
        }
        public void CancelAssignments() { ReleaseTask(); route = null; Activity = CrewActivity.Idle; view?.Present(Activity, 0, 0, Vector3.zero); }
        public void RestorePosition(Vector2Int coordinate)
        {
            CurrentCell = coordinate; view.transform.position = FootPosition(coordinate); view.Wake(); retryTime = 0; Observe(); PublishStatus();
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            if (farmSession != null) roster.Farming.Cancel(farmSession, this);
            farmSession = null;
            if (restSession != null) roster.Rest.Cancel(restSession, this);
            restSession = null;
            if (rescueSession != null) roster.Recruitment.Cancel(rescueSession, this);
            rescueSession = null; roster.ReleaseClaims(this);
            if (ownsConstruction && task != null) building.TrySuspendConstruction(task.Coordinate);
            if (ownsConstruction && grillTask != null) building.TrySuspendGrill(grillTask.Coordinate);
            if (salvageTask != null && salvage != null) salvage.ReleaseClaim(salvageTask, this);
            if (fishingSession != null && fishing != null) fishing.Cancel(fishingSession, this);
            if (cookingSession != null) production.Cancel(cookingSession, this);
            if (mealSession != null) needs.Cancel(mealSession, this);
            if (purifyingSession != null) water.Cancel(purifyingSession, this);
            if (drinkSession != null) needs.CancelDrink(drinkSession, this);
            if (netSession != null) nets.Cancel(netSession, this);
            building.FacilityChanging -= InterruptFacility;
            if (view != null) UnityEngine.Object.Destroy(view.gameObject);
            task = null;
            salvageTask = null;
            fishingSession = null;
            cookingSession = null; mealSession = null; grillTask = null;
            purifyingSession = null; drinkSession = null;
            netSession = null;
            view = null;
            route = null;
            IsInitialized = false;
            Changed = null;
        }
    }
}
