using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;

namespace RaftGame.Crew
{
    /// <summary>Single-worker prototype. Explicitly initialized and ticked by GameManager.</summary>
    public sealed class CrewManager : MonoBehaviour
    {
        private CrewConfig configuration;
        private BuildManager building;
        private CrewView view;
        private DeckCell task;
        private List<Vector2Int> route;
        private int waypoint;
        private float retryTime;
        private int observedLayout = -1;
        private string previousStatus;
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
        private NeedsManager needs;
        private GrillCell grillTask;
        private CookingSession cookingSession;
        private MealSession mealSession;
        private int observedProduction = -1;
        private int observedNeeds = -1;
        private WaterManager water;
        private PurifyingSession purifyingSession;
        private DrinkSession drinkSession;
        private int observedWater = -1;

        public bool IsInitialized { get; private set; }
        public CrewActivity Activity { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        public Vector2Int? TaskCell => task?.Coordinate ?? grillTask?.Coordinate ?? cookingSession?.Grill.Coordinate ?? purifyingSession?.Purifier.Coordinate;
        public float WorkProgress => drinkSession != null ? drinkSession.Progress : purifyingSession != null ? purifyingSession.Progress : mealSession != null ? mealSession.Progress : cookingSession != null ? cookingSession.Progress :
            fishingSession != null ? fishingSession.Progress : salvageTask != null ? gatherProgress :
            grillTask != null ? grillTask.ConstructionProgress : task != null ? task.ConstructionProgress : 0f;
        public Transform Worker => view != null ? view.transform : null;
        public event Action Changed;

        public string StatusText => Activity == CrewActivity.Constructing ? $"船员：正在施工 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Gathering ? $"船员：正在打捞 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Fishing ? $"船员：垂钓中 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Cooking ? $"船员：烤鱼中 {Mathf.FloorToInt(WorkProgress * 100f)}%" :
            Activity == CrewActivity.Eating ? "船员：享用烤鱼" :
            Activity == CrewActivity.Drinking ? "船员：饮用淡水" :
            Activity == CrewActivity.Purifying ? $"船员：净水中 {Mathf.FloorToInt(WorkProgress * 100)}%" :
            Activity == CrewActivity.Walking ? (purifyingSession != null ? "船员：前往净水器" : cookingSession != null ? "船员：前往烤架" : fishingSession != null ? "船员：前往钓鱼点" : salvageTask != null ? "船员：前往打捞点" : task != null || grillTask != null ? "船员：前往施工点" : "船员：返回甲板") :
            Activity == CrewActivity.WaitingForMaterials ? "船员：等待材料" :
            Activity == CrewActivity.WaitingForSalvage ? "船员：等待可打捞物资" :
            Activity == CrewActivity.WaitingForAccess ? "船员：等待通路" : "船员：待命";

        public bool Initialize(CrewConfig config, BuildManager buildManager, Camera camera, SalvageManager salvageManager = null,
            FishingManager fishingManager = null, ProductionManager productionManager = null, NeedsManager needsManager = null, WaterManager waterManager = null)
        {
            if (IsInitialized) return true;
            if (config == null || config.CrewPrefab == null || config.CrewPrefab.GetComponent<CrewView>() == null ||
                buildManager == null || !buildManager.IsInitialized)
            {
                Debug.LogError("CrewManager requires CrewConfig, a CrewView prefab and an initialized BuildManager.", this);
                return false;
            }
            configuration = config;
            building = buildManager;
            salvage = salvageManager;
            fishing = fishingManager;
            production = productionManager;
            needs = needsManager;
            water = waterManager;
            CurrentCell = config.SpawnCell;
            if (!DeckPathfinder.IsWalkable(building.Cells, CurrentCell))
            {
                bool found = false;
                foreach (DeckCell cell in building.Cells.Values)
                    if (DeckPathfinder.IsWalkable(building.Cells, cell.Coordinate) && (!found || Earlier(cell.Coordinate, CurrentCell)))
                    { found = true; CurrentCell = cell.Coordinate; }
                if (!found) return false;
            }
            view = Instantiate(config.CrewPrefab, transform).GetComponent<CrewView>();
            view.name = "Sailor";
            view.transform.position = FootPosition(CurrentCell);
            view.Initialize(camera);
            building.IsCellOccupied = coordinate => view != null &&
                (CurrentCell == coordinate || building.WorldToCell(view.transform.position) == coordinate);
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
            if (dt > 0f)
            {
                if (mealSession == null && drinkSession == null && needs != null && (needs.ShouldEat || needs.ShouldDrink) && HasAssignment()) ReleaseTask();
                if (task != null && (!building.Cells.TryGetValue(task.Coordinate, out DeckCell current) ||
                    !ReferenceEquals(task, current) || current.State != (ownsConstruction ? DeckState.Constructing : DeckState.Planned)))
                    ReleaseTask();
                if (grillTask != null && (!building.Grills.TryGetValue(grillTask.Coordinate, out GrillCell currentGrill) ||
                    !ReferenceEquals(grillTask, currentGrill) || currentGrill.State != (ownsConstruction ? DeckState.Constructing : DeckState.Planned))) ReleaseTask();
                if (!ReferenceEquals(salvageTask, null) && !salvage.IsClaimValid(salvageTask, this)) ReleaseTask();
                if (cookingSession != null && !production.IsValid(cookingSession, this)) ReleaseTask();
                if (purifyingSession != null && !water.IsValid(purifyingSession, this)) ReleaseTask();
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
                else
                {
                    retryTime -= dt;
                    if (retryTime <= 0f || WorkChanged() || (fishing != null && observedFishing != fishing.Version) ||
                        (needs != null && observedNeeds != needs.Version)) SelectTask();
                }
            }
            Vector3 workPosition = drinkSession != null || mealSession != null ? view.transform.position + Vector3.up :
                purifyingSession != null ? building.CellToWorld(purifyingSession.Purifier.Coordinate) + Vector3.up * 0.7f :
                cookingSession != null ? building.CellToWorld(cookingSession.Grill.Coordinate) + Vector3.up * 0.7f :
                fishingSession != null ? fishing.GetWaterPoint(fishingSession.Spot) :
                salvageTask != null ? salvageTask.transform.position : grillTask != null ? building.CellToWorld(grillTask.Coordinate) :
                task != null ? building.CellToWorld(task.Coordinate) : Vector3.zero;
            view.Present(Activity, WorkProgress, dt, workPosition);
            PublishStatus();
        }

        private Vector3 FootPosition(Vector2Int cell) => building.CellToWorld(cell) + Vector3.up * configuration.FootOffset;
        private static bool Earlier(Vector2Int a, Vector2Int b) => a.x < b.x || (a.x == b.x && a.y < b.y);

        private bool HasAssignment() => task != null || grillTask != null || salvageTask != null || fishingSession != null || cookingSession != null || purifyingSession != null;
        private bool WorkChanged() => observedLayout != building.LayoutVersion || observedInventory != building.InventoryVersion ||
            (salvage != null && observedSalvage != salvage.Version) || (production != null && observedProduction != production.Version) || (water != null && observedWater != water.Version);
        private void Observe()
        {
            observedLayout = building.LayoutVersion; observedInventory = building.InventoryVersion;
            observedSalvage = salvage != null ? salvage.Version : 0;
            observedFishing = fishing != null ? fishing.Version : 0;
            observedProduction = production != null ? production.Version : 0;
            observedNeeds = needs != null ? needs.Version : 0;
            observedWater = water != null ? water.Version : 0;
        }

        private void SelectTask()
        {
            retryTime = Mathf.Max(0.1f, configuration.RetryInterval);
            Observe();
            if (needs != null && needs.ShouldDrink && needs.Hydration <= needs.Satiety && (drinkSession = needs.TryBeginDrink(this)) != null)
            { Activity = CrewActivity.Drinking; return; }
            if (needs != null && (mealSession = needs.TryBeginMeal(this)) != null)
            { Activity = CrewActivity.Eating; return; }
            if (needs != null && (drinkSession = needs.TryBeginDrink(this)) != null)
            { Activity = CrewActivity.Drinking; return; }
            DeckPathfinder.FindReachable(building.Cells, CurrentCell, out var parents, out var distances);
            if (salvage != null && salvage.TryFindTask(distances, this, out FloatingResource item, out Vector2Int salvageApproach) && salvage.TryClaim(item, this))
            {
                salvageTask = item;
                gatherProgress = 0f;
                route = DeckPathfinder.Reconstruct(parents, salvageApproach);
                waypoint = 1;
                Activity = CrewActivity.Walking;
                if (waypoint >= route.Count) StartWork();
                return;
            }
            DeckCell chosen = null;
            Vector2Int destination = CurrentCell;
            int bestDistance = int.MaxValue;
            foreach (DeckCell candidate in building.Cells.Values)
            {
                if (candidate.State != DeckState.Planned || !building.CanAffordConstruction()) continue;
                foreach (Vector2Int direction in DeckPathfinder.Directions)
                {
                    Vector2Int approach = candidate.Coordinate + direction;
                    if (!distances.TryGetValue(approach, out int distance)) continue;
                    if (distance < bestDistance || (distance == bestDistance && (chosen == null || Earlier(candidate.Coordinate, chosen.Coordinate))))
                    { chosen = candidate; destination = approach; bestDistance = distance; }
                }
            }
            GrillCell chosenGrill = null;
            foreach (GrillCell candidate in building.Grills.Values)
                if (candidate.State == DeckState.Planned && building.CanAffordFacility(candidate.Kind) && distances.TryGetValue(candidate.WorkCell, out int distance) &&
                    (distance < bestDistance || (distance == bestDistance && chosen == null && (chosenGrill == null || Earlier(candidate.Coordinate, chosenGrill.Coordinate)))))
                { chosenGrill = candidate; chosen = null; destination = candidate.WorkCell; bestDistance = distance; }
            if (chosen == null && chosenGrill == null)
            {
                if (water != null && water.TryFindTask(distances, out GrillCell purifier))
                {
                    purifyingSession = water.TryAcquire(this, purifier);
                    if (purifyingSession != null) { BeginRoute(parents, purifier.WorkCell); return; }
                }
                if (production != null && production.TryFindTask(distances, out GrillCell grill))
                {
                    cookingSession = production.TryAcquire(this, grill);
                    if (cookingSession != null) { BeginRoute(parents, grill.WorkCell); return; }
                }
                if (fishing != null && fishing.TryFindSpot(distances, out FishingSpot spot))
                {
                    fishingSession = fishing.TryAcquire(this, spot);
                    if (fishingSession != null)
                    {
                        route = DeckPathfinder.Reconstruct(parents, spot.Cell);
                        waypoint = 1;
                        Activity = CrewActivity.Walking;
                        if (waypoint >= route.Count) StartWork();
                        return;
                    }
                }
                Activity = building.BlueprintCount > 0 ? (HasAffordableBlueprint() ? CrewActivity.WaitingForAccess : CrewActivity.WaitingForMaterials) :
                    salvage != null && salvage.MarkedCount > 0 ? CrewActivity.WaitingForSalvage : CrewActivity.Idle;
                return;
            }
            task = chosen;
            grillTask = chosenGrill;
            BeginRoute(parents, destination);
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

        private bool HasPriorityWork()
        {
            DeckPathfinder.FindReachable(building.Cells, CurrentCell, out _, out var distances);
            if (salvage != null && salvage.TryFindTask(distances, this, out _, out _)) return true;
            bool affordDeck = CanYieldFor(building.DeckCost) || building.CanAffordConstruction();
            if (affordDeck) foreach (DeckCell cell in building.Cells.Values)
                if (cell.State == DeckState.Planned)
                    foreach (Vector2Int direction in DeckPathfinder.Directions)
                        if (distances.ContainsKey(cell.Coordinate + direction)) return true;
            foreach (GrillCell grill in building.Grills.Values)
                if (grill.State == DeckState.Planned && distances.ContainsKey(grill.WorkCell) &&
                    (building.CanAffordFacility(grill.Kind) || CanYieldFor(building.FacilityCost(grill.Kind)))) return true;
            if (purifyingSession == null && water != null && water.TryFindTask(distances, out _)) return true;
            if (purifyingSession == null && cookingSession == null && production != null && production.TryFindTask(distances, out _)) return true;
            return false;
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

        private void ReleaseTask()
        {
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

        private void PublishStatus()
        {
            string status = StatusText;
            if (status == previousStatus) return;
            previousStatus = status;
            Changed?.Invoke();
        }
        public void CancelAssignments() { ReleaseTask(); route = null; Activity = CrewActivity.Idle; }
        public void RestorePosition(Vector2Int coordinate)
        {
            CurrentCell = coordinate; view.transform.position = FootPosition(coordinate); retryTime = 0; Observe(); PublishStatus();
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            if (ownsConstruction && task != null) building.TrySuspendConstruction(task.Coordinate);
            if (ownsConstruction && grillTask != null) building.TrySuspendGrill(grillTask.Coordinate);
            if (salvageTask != null && salvage != null) salvage.ReleaseClaim(salvageTask, this);
            if (fishingSession != null && fishing != null) fishing.Cancel(fishingSession, this);
            if (cookingSession != null) production.Cancel(cookingSession, this);
            if (mealSession != null) needs.Cancel(mealSession, this);
            if (purifyingSession != null) water.Cancel(purifyingSession, this);
            if (drinkSession != null) needs.CancelDrink(drinkSession, this);
            building.IsCellOccupied = null;
            if (view != null) Destroy(view.gameObject);
            task = null;
            salvageTask = null;
            fishingSession = null;
            cookingSession = null; mealSession = null; grillTask = null;
            purifyingSession = null; drinkSession = null;
            view = null;
            route = null;
            IsInitialized = false;
            Changed = null;
        }
    }
}
