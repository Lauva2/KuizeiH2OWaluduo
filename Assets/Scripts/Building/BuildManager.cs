using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using RaftGame.Resources;
using RaftGame.Core;

namespace RaftGame.Building
{
    /// <summary>Grid ownership, connected blueprints and construction hand-off. Driven by GameManager.</summary>
    public sealed class BuildManager : MonoBehaviour
    {
        [SerializeField] private Transform raftRoot;
        private readonly Dictionary<Vector2Int, DeckCell> cells = new Dictionary<Vector2Int, DeckCell>();
        private readonly Dictionary<Vector2Int, GrillCell> grills = new Dictionary<Vector2Int, GrillCell>();
        private static readonly Vector2Int[] Neighbors = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        private BuildConfig configuration;
        private Camera sceneCamera;
        private GameObject preview;
        private GameObject grillPreview;
        private GameObject purifierPreview;
        private MaterialPropertyBlock tint;
        private bool painting;
        private Vector2Int? paintedCell;
        private Vector2Int? hoveredCell;
        private ResourceManager resources;

        public bool IsInitialized { get; private set; }
        public BuildTool Tool { get; private set; }
        public string Message { get; private set; } = "沿木筏边缘规划新的甲板";
        public IReadOnlyDictionary<Vector2Int, DeckCell> Cells => cells;
        public IReadOnlyDictionary<Vector2Int, GrillCell> Grills => grills;
        public Func<Vector2Int, bool> IsCellOccupied { private get; set; }
        public int PlacementRotation { get; private set; }
        public int GrillCount { get; private set; }
        public int PurifierCount { get; private set; }
        public bool PurifierUnlocked { get; private set; }
        public bool CatalogOpen { get; set; }
        public void UnlockPurifier() { if (PurifierUnlocked) return; PurifierUnlocked = true; Feedback("发现净水器图纸 · 按 B 打开建造图册"); }
        public void RestoreUnlocks(bool purifier) { PurifierUnlocked = purifier; CatalogOpen = false; if (!purifier && Tool == BuildTool.PurifierBlueprint) SetTool(BuildTool.None); Changed?.Invoke(); }
        public int CompletedCount { get; private set; }
        public int BlueprintCount => cells.Count - CompletedCount + grills.Count - GrillCount - PurifierCount;
        public int LayoutVersion { get; private set; }
        public float TileSize => configuration.TileSize;
        public ResourceCost DeckCost => configuration.DeckCost;
        public ResourceCost GrillCost => configuration.GrillCost;
        public ResourceCost PurifierCost => configuration.PurifierCost;
        public int MaximumCoordinate => configuration.MaximumCoordinate;
        public ResourceCost FacilityCost(FacilityKind kind) => kind == FacilityKind.Purifier ? configuration.PurifierCost : configuration.GrillCost;
        public bool HasFacilityAssets(FacilityKind kind) => FacilityPrefab(kind, false) != null && FacilityPrefab(kind, true) != null;
        private GameObject FacilityPrefab(FacilityKind kind, bool blueprint) => kind == FacilityKind.Purifier ?
            (blueprint ? configuration.PurifierBlueprintPrefab : configuration.PurifierPrefab) :
            (blueprint ? configuration.GrillBlueprintPrefab : configuration.GrillPrefab);
        public int InventoryVersion => resources != null ? resources.Version : 0;
        public event Action Changed;

        public bool Initialize(BuildConfig config, Camera camera, ResourceManager inventory = null)
        {
            if (config == null || camera == null || raftRoot == null || config.DeckPrefab == null || config.BlueprintPrefab == null ||
                (inventory != null && !inventory.IsInitialized))
            {
                Debug.LogError("BuildManager requires configuration, camera, raft root and both prefabs.", this);
                return false;
            }
            if (IsInitialized) return true;
            configuration = config;
            resources = inventory;
            sceneCamera = camera;
            tint = new MaterialPropertyBlock();
            cells.Clear();
            grills.Clear();
            GrillCount = 0;
            PurifierCount = 0;
            PurifierUnlocked = false;
            CompletedCount = 0;
            foreach (DeckTile tile in raftRoot.GetComponentsInChildren<DeckTile>())
            {
                if (cells.ContainsKey(tile.Coordinate))
                {
                    Debug.LogError("Duplicate starting raft coordinate: " + tile.Coordinate, this);
                    return false;
                }
                cells.Add(tile.Coordinate, new DeckCell(tile.Coordinate, DeckState.Complete, tile.gameObject));
                CompletedCount++;
            }
            if (CompletedCount == 0)
            {
                Debug.LogError("The raft needs at least one authored starting tile.", this);
                return false;
            }
            preview = Instantiate(config.BlueprintPrefab, raftRoot);
            preview.name = "Placement Preview";
            preview.SetActive(false);
            if (config.GrillBlueprintPrefab != null)
            {
                grillPreview = Instantiate(config.GrillBlueprintPrefab, raftRoot);
                grillPreview.name = "Grill Placement Preview";
                grillPreview.SetActive(false);
            }
            IsInitialized = true;
            if (config.PurifierBlueprintPrefab != null)
            { purifierPreview = Instantiate(config.PurifierBlueprintPrefab, raftRoot); purifierPreview.name = "Purifier Placement Preview"; purifierPreview.SetActive(false); }
            LayoutVersion++;
            return true;
        }

        public void SetTool(BuildTool tool)
        {
            if (!IsInitialized) return;
            if (tool == BuildTool.PurifierBlueprint && !PurifierUnlocked) { Feedback("尚未发现净水器图纸 · 试着打捞漂流补给箱"); return; }
            Tool = tool;
            painting = false;
            paintedCell = null;
            hoveredCell = null;
            preview.SetActive(false);
            if (grillPreview != null) grillPreview.SetActive(false);
            if (purifierPreview != null) purifierPreview.SetActive(false);
            Message = tool == BuildTool.DeckBlueprint ? "点击或拖动放置蓝图；右键退出" :
                tool == BuildTool.GrillBlueprint || tool == BuildTool.PurifierBlueprint ? "选择甲板放设施；R 旋转，箭头处保留操作位" :
                tool == BuildTool.CancelBlueprint ? "点击撤销外围蓝图；右键退出" : "沿木筏边缘规划新的甲板";
            Changed?.Invoke();
        }

        public void Tick()
        {
            if (!IsInitialized) return;
            if (CatalogOpen) return;
            if (Input.GetKeyDown(KeyCode.X)) SetTool(Tool == BuildTool.CancelBlueprint ? BuildTool.None : BuildTool.CancelBlueprint);
            if (Input.GetKeyDown(KeyCode.G)) SetTool(Tool == BuildTool.GrillBlueprint ? BuildTool.None : BuildTool.GrillBlueprint);
            if (Input.GetKeyDown(KeyCode.P)) SetTool(Tool == BuildTool.PurifierBlueprint ? BuildTool.None : BuildTool.PurifierBlueprint);
            if ((Tool == BuildTool.GrillBlueprint || Tool == BuildTool.PurifierBlueprint) && Input.GetKeyDown(KeyCode.R))
            { PlacementRotation = (PlacementRotation + 1) % 4; hoveredCell = null; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) SetTool(BuildTool.None);
            bool blocked = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || Input.GetMouseButton(2);
            if (Input.GetMouseButtonUp(0)) { painting = false; paintedCell = null; }
            if (Input.GetMouseButtonDown(0)) painting = !blocked && Tool != BuildTool.None;
            if (Tool == BuildTool.None || blocked || !TryPointerCell(out Vector2Int coordinate))
            {
                preview.SetActive(false);
                if (grillPreview != null) grillPreview.SetActive(false);
                if (purifierPreview != null) purifierPreview.SetActive(false);
                return;
            }

            bool grillTool = Tool == BuildTool.GrillBlueprint || Tool == BuildTool.PurifierBlueprint;
            FacilityKind kind = Tool == BuildTool.PurifierBlueprint ? FacilityKind.Purifier : FacilityKind.Grill;
            string reason;
            bool valid = grillTool ? CanPlaceFacility(coordinate, PlacementRotation, kind, out reason) :
                Tool == BuildTool.DeckBlueprint ? CanPlace(coordinate, out reason) : CanCancel(coordinate, out reason);
            GameObject facilityPreview = kind == FacilityKind.Purifier ? purifierPreview : grillPreview;
            GameObject activePreview = grillTool && facilityPreview != null ? facilityPreview : preview;
            preview.SetActive(activePreview == preview);
            if (grillPreview != null) grillPreview.SetActive(activePreview == grillPreview);
            if (purifierPreview != null) purifierPreview.SetActive(activePreview == purifierPreview);
            activePreview.transform.position = CellToWorld(coordinate) + Vector3.up * 0.035f;
            activePreview.transform.rotation = raftRoot.rotation * Quaternion.Euler(0, grillTool ? PlacementRotation * 90 : 0, 0);
            Tint(activePreview, valid ? configuration.ValidColor : configuration.InvalidColor);
            if (hoveredCell != coordinate)
            {
                hoveredCell = coordinate;
                Message = grillTool ? (valid ? "点击放置设施；箭头处为操作位，R 旋转" : reason) : Tool == BuildTool.DeckBlueprint ?
                    (valid ? "点击放置甲板蓝图" : PlacementReason(coordinate)) :
                    (valid ? "点击撤销这块蓝图" : CancellationReason(coordinate));
                Changed?.Invoke();
            }
            if (painting && Input.GetMouseButton(0) && paintedCell != coordinate)
            {
                paintedCell = coordinate;
                if (Tool == BuildTool.DeckBlueprint) TryPlaceBlueprint(coordinate, out _);
                else if (grillTool) { TryPlaceFacilityBlueprint(coordinate, PlacementRotation, kind, out _); painting = false; }
                else TryCancelBlueprint(coordinate, out _);
            }
        }

        public Vector2Int WorldToCell(Vector3 position)
        {
            Vector3 local = raftRoot.InverseTransformPoint(position);
            return new Vector2Int(Mathf.FloorToInt(local.x / configuration.TileSize + 0.5f),
                Mathf.FloorToInt(local.z / configuration.TileSize + 0.5f));
        }

        public Vector3 CellToWorld(Vector2Int coordinate)
        {
            return raftRoot.TransformPoint(new Vector3(coordinate.x * configuration.TileSize,
                configuration.DeckHeight, coordinate.y * configuration.TileSize));
        }

        private bool TryPointerCell(out Vector2Int coordinate)
        {
            var plane = new Plane(raftRoot.up, raftRoot.TransformPoint(Vector3.up * configuration.DeckHeight));
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (plane.Raycast(ray, out float distance))
            {
                coordinate = WorldToCell(ray.GetPoint(distance));
                return true;
            }
            coordinate = default;
            return false;
        }

        public bool CanPlace(Vector2Int coordinate, out string reason)
        {
            if (!IsInitialized) { reason = "建造系统尚未准备好"; return false; }
            if (Math.Abs((long)coordinate.x) > configuration.MaximumCoordinate ||
                Math.Abs((long)coordinate.y) > configuration.MaximumCoordinate)
            { reason = "超出本片木筏的规划范围"; return false; }
            if (cells.ContainsKey(coordinate)) { reason = "这个格子已经被占用"; return false; }
            foreach (Vector2Int direction in Neighbors)
                if (cells.ContainsKey(coordinate + direction) && !grills.ContainsKey(coordinate + direction)) { reason = string.Empty; return true; }
            reason = "请贴着已有甲板或蓝图放置";
            return false;
        }

        public bool TryPlaceBlueprint(Vector2Int coordinate, out string reason)
        {
            if (!CanPlace(coordinate, out reason)) { Feedback(reason); return false; }
            GameObject visual = Instantiate(configuration.BlueprintPrefab, raftRoot);
            visual.name = $"Deck Blueprint [{coordinate.x}, {coordinate.y}]";
            visual.transform.position = CellToWorld(coordinate);
            Tint(visual, configuration.PlannedColor);
            cells.Add(coordinate, new DeckCell(coordinate, DeckState.Planned, visual));
            LayoutVersion++;
            Feedback("蓝图已放置，等待船员施工");
            return true;
        }

        public bool CanCancel(Vector2Int coordinate, out string reason)
        {
            if (grills.TryGetValue(coordinate, out GrillCell grill))
            { reason = grill.State == DeckState.Complete ? "烤架已经建好" : string.Empty; return grill.State != DeckState.Complete; }
            if (!cells.TryGetValue(coordinate, out DeckCell cell) || cell.State == DeckState.Complete)
            { reason = "只能撤销尚未完工的蓝图"; return false; }
            // Every remaining blueprint must still connect to a finished tile after removal.
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            foreach (DeckCell entry in cells.Values)
                if (entry.State == DeckState.Complete && !grills.ContainsKey(entry.Coordinate))
                { visited.Add(entry.Coordinate); queue.Enqueue(entry.Coordinate); }
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in Neighbors)
                {
                    Vector2Int next = current + direction;
                    if (next != coordinate && cells.ContainsKey(next) && !grills.ContainsKey(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            if (visited.Count != cells.Count - 1 - grills.Count)
            { reason = "请先撤销外围蓝图，保持木筏连接"; return false; }
            reason = string.Empty;
            return true;
        }

        public bool TryCancelBlueprint(Vector2Int coordinate, out string reason)
        {
            if (!CanCancel(coordinate, out reason)) { Feedback(reason); return false; }
            if (grills.TryGetValue(coordinate, out GrillCell grill))
            {
                if (resources != null) resources.Release(grill);
                Destroy(grill.Visual); grills.Remove(coordinate); LayoutVersion++;
                Feedback("已撤销烤架蓝图，返还预留材料"); return true;
            }
            Destroy(cells[coordinate].Visual);
            if (resources != null) resources.Release(cells[coordinate]);
            cells.Remove(coordinate);
            LayoutVersion++;
            Feedback("已撤销甲板蓝图");
            return true;
        }

        public bool TryBeginConstruction(Vector2Int coordinate)
        {
            if (!cells.TryGetValue(coordinate, out DeckCell cell) || cell.State != DeckState.Planned) return false;
            foreach (Vector2Int direction in Neighbors)
                if (cells.TryGetValue(coordinate + direction, out DeckCell neighbor) && neighbor.State == DeckState.Complete && !neighbor.IsBlocked)
                {
                    if (resources != null && !resources.TryReserve(cell, configuration.DeckCost)) return false;
                    cell.State = DeckState.Constructing; LayoutVersion++; Changed?.Invoke(); return true;
                }
            return false;
        }

        public bool CanAffordConstruction() => resources == null || resources.CanAfford(configuration.DeckCost);
        public bool CanAffordGrill() => resources == null || resources.CanAfford(configuration.GrillCost);
        public bool CanAffordFacility(FacilityKind kind) => resources == null || resources.CanAfford(FacilityCost(kind));

        public bool CanPlaceGrill(Vector2Int coordinate, int rotation, out string reason) => CanPlaceFacility(coordinate, rotation, FacilityKind.Grill, out reason);
        public bool CanPlaceFacility(Vector2Int coordinate, int rotation, FacilityKind kind, out string reason)
        {
            if (kind == FacilityKind.Purifier && !PurifierUnlocked) { reason = "尚未发现净水器图纸 · 从漂流补给箱中寻找"; return false; }
            if (!IsInitialized || FacilityPrefab(kind, false) == null || FacilityPrefab(kind, true) == null)
            { reason = "设施配置尚未准备好"; return false; }
            if (!cells.TryGetValue(coordinate, out DeckCell deck) || deck.State != DeckState.Complete || deck.IsBlocked || grills.ContainsKey(coordinate))
            { reason = "设施需要一格空闲的已建甲板"; return false; }
            Vector2Int work = coordinate + GrillCell.WorkDirection(rotation);
            if (!cells.TryGetValue(work, out DeckCell approach) || approach.State != DeckState.Complete || approach.IsBlocked || grills.ContainsKey(work))
            { reason = "箭头处需要一格空闲甲板作为操作位"; return false; }
            foreach (GrillCell other in grills.Values)
                if (other.WorkCell == coordinate) { reason = "这里是其他设施的操作位"; return false; }
            // Reserve future footprints now, so several blueprints cannot jointly sever a route.
            var excluded = new HashSet<Vector2Int>(grills.Keys) { coordinate };
            if (!ConnectedWithout(excluded, true) || !ConnectedWithout(excluded, false))
            { reason = "这里会堵住通路，请保留连通的甲板"; return false; }
            reason = string.Empty; return true;
        }

        private bool ConnectedWithout(HashSet<Vector2Int> excluded, bool completeOnly)
        {
            var remaining = new HashSet<Vector2Int>();
            foreach (DeckCell cell in cells.Values)
                if (!excluded.Contains(cell.Coordinate) && (!completeOnly || cell.State == DeckState.Complete)) remaining.Add(cell.Coordinate);
            if (remaining.Count == 0) return false;
            var queue = new Queue<Vector2Int>();
            foreach (Vector2Int first in remaining) { queue.Enqueue(first); remaining.Remove(first); break; }
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in Neighbors)
                    if (remaining.Remove(current + direction)) queue.Enqueue(current + direction);
            }
            return remaining.Count == 0;
        }

        public bool TryPlaceGrillBlueprint(Vector2Int coordinate, int rotation, out string reason) => TryPlaceFacilityBlueprint(coordinate, rotation, FacilityKind.Grill, out reason);
        public bool TryPlaceFacilityBlueprint(Vector2Int coordinate, int rotation, FacilityKind kind, out string reason)
        {
            if (!CanPlaceFacility(coordinate, rotation, kind, out reason)) { Feedback(reason); return false; }
            var visual = Instantiate(FacilityPrefab(kind, true), raftRoot);
            var cell = new GrillCell(coordinate, rotation, visual, kind);
            visual.name = $"{kind} Blueprint [{coordinate.x}, {coordinate.y}]";
            visual.transform.position = CellToWorld(coordinate);
            visual.transform.rotation = raftRoot.rotation * Quaternion.Euler(0, cell.Rotation * 90, 0);
            Tint(visual, configuration.PlannedColor);
            grills.Add(coordinate, cell); LayoutVersion++;
            Feedback("设施蓝图已放置，等待船员施工"); return true;
        }

        public bool TryBeginGrill(Vector2Int coordinate)
        {
            if (!grills.TryGetValue(coordinate, out GrillCell cell) || cell.State != DeckState.Planned ||
                (resources != null && !resources.TryReserve(cell, FacilityCost(cell.Kind)))) return false;
            cell.State = DeckState.Constructing; LayoutVersion++; Changed?.Invoke(); return true;
        }

        public bool SetGrillProgress(Vector2Int coordinate, float progress)
        {
            if (!grills.TryGetValue(coordinate, out GrillCell cell) || cell.State != DeckState.Constructing) return false;
            cell.ConstructionProgress = Mathf.Clamp01(progress); return true;
        }

        public bool TryCompleteGrill(Vector2Int coordinate)
        {
            if (!grills.TryGetValue(coordinate, out GrillCell cell) || cell.State != DeckState.Constructing ||
                (IsCellOccupied != null && IsCellOccupied(coordinate))) return false;
            GameObject visual = Instantiate(FacilityPrefab(cell.Kind, false), raftRoot);
            if (resources != null && !resources.Commit(cell)) { Destroy(visual); return false; }
            visual.transform.position = CellToWorld(coordinate);
            visual.transform.rotation = raftRoot.rotation * Quaternion.Euler(0, cell.Rotation * 90, 0);
            Destroy(cell.Visual); cell.Visual = visual; cell.View = visual.GetComponent<GrillView>();
            cell.WaterView = visual.GetComponent<PurifierView>();
            cell.State = DeckState.Complete; cell.ConstructionProgress = 1f;
            if (cell.View != null) cell.View.Present(false, 0f);
            cells[coordinate].IsBlocked = true;
            if (cell.Kind == FacilityKind.Grill) GrillCount++; else PurifierCount++;
            LayoutVersion++; Feedback(cell.Kind == FacilityKind.Grill ? "烤架建好，船员会自动储备熟鱼" : "净水器建好，船员会自动储备淡水"); return true;
        }

        public bool TrySuspendGrill(Vector2Int coordinate)
        {
            if (!grills.TryGetValue(coordinate, out GrillCell cell) || cell.State != DeckState.Constructing) return false;
            if (resources != null) resources.Release(cell);
            cell.State = DeckState.Planned; LayoutVersion++; Changed?.Invoke(); return true;
        }

        public bool TryCompleteConstruction(Vector2Int coordinate)
        {
            if (!cells.TryGetValue(coordinate, out DeckCell cell) || cell.State != DeckState.Constructing) return false;
            GameObject visual = Instantiate(configuration.DeckPrefab, raftRoot);
            if (resources != null && !resources.Commit(cell)) { Destroy(visual); return false; }
            visual.transform.position = CellToWorld(coordinate);
            visual.GetComponent<DeckTile>().Coordinate = coordinate;
            Destroy(cell.Visual);
            cell.Visual = visual;
            cell.State = DeckState.Complete;
            cell.ConstructionProgress = 1f;
            CompletedCount++;
            LayoutVersion++;
            Feedback("新的甲板已经建好");
            return true;
        }

        public bool SetConstructionProgress(Vector2Int coordinate, float progress)
        {
            if (!cells.TryGetValue(coordinate, out DeckCell cell) || cell.State != DeckState.Constructing) return false;
            cell.ConstructionProgress = Mathf.Clamp01(progress);
            return true;
        }

        public bool TrySuspendConstruction(Vector2Int coordinate)
        {
            if (!cells.TryGetValue(coordinate, out DeckCell cell) || cell.State != DeckState.Constructing) return false;
            if (resources != null) resources.Release(cell);
            cell.State = DeckState.Planned;
            LayoutVersion++;
            Changed?.Invoke();
            return true;
        }

        private string PlacementReason(Vector2Int coordinate) { CanPlace(coordinate, out string reason); return reason; }
        private string CancellationReason(Vector2Int coordinate) { CanCancel(coordinate, out string reason); return reason; }
        private void Feedback(string message) { Message = message; Changed?.Invoke(); }

        private void Tint(GameObject target, Color color)
        {
            tint.SetColor("_Color", color);
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(tint);
        }
        public void RestoreLayout(IReadOnlyList<DeckSave> decks, IReadOnlyList<FacilitySave> facilities)
        {
            SetTool(BuildTool.None);
            foreach (var cell in cells.Values) { cell.Visual.SetActive(false); Destroy(cell.Visual); }
            foreach (var cell in grills.Values) { cell.Visual.SetActive(false); Destroy(cell.Visual); }
            cells.Clear(); grills.Clear(); CompletedCount = 0; GrillCount = 0; PurifierCount = 0;
            foreach (DeckSave entry in decks)
            {
                var visual = Instantiate(entry.Complete ? configuration.DeckPrefab : configuration.BlueprintPrefab, raftRoot);
                visual.transform.position = CellToWorld(entry.Coordinate);
                if (entry.Complete) { visual.GetComponent<DeckTile>().Coordinate = entry.Coordinate; CompletedCount++; }
                else Tint(visual, configuration.PlannedColor);
                cells.Add(entry.Coordinate, new DeckCell(entry.Coordinate, entry.Complete ? DeckState.Complete : DeckState.Planned, visual) { ConstructionProgress = entry.Progress });
            }
            foreach (FacilitySave entry in facilities)
            {
                var visual = Instantiate(FacilityPrefab(entry.Kind, !entry.Complete), raftRoot);
                visual.transform.position = CellToWorld(entry.Coordinate); visual.transform.rotation = raftRoot.rotation * Quaternion.Euler(0, entry.Rotation * 90, 0);
                var cell = new GrillCell(entry.Coordinate, entry.Rotation, visual, entry.Kind) { State = entry.Complete ? DeckState.Complete : DeckState.Planned, ConstructionProgress = entry.Progress };
                if (entry.Complete)
                {
                    cell.View = visual.GetComponent<GrillView>(); cell.WaterView = visual.GetComponent<PurifierView>();
                    cell.View?.Present(false, 0); cell.WaterView?.Present(false, 0);
                    cells[entry.Coordinate].IsBlocked = true;
                    if (entry.Kind == FacilityKind.Grill) GrillCount++; else PurifierCount++;
                }
                else Tint(visual, configuration.PlannedColor);
                grills.Add(entry.Coordinate, cell);
            }
            LayoutVersion++; Feedback("存档中的木筏已恢复");
        }

        public void Shutdown()
        {
            if (resources != null) foreach (DeckCell cell in cells.Values) resources.Release(cell);
            if (resources != null) foreach (GrillCell cell in grills.Values) resources.Release(cell);
            if (preview != null) Destroy(preview);
            if (grillPreview != null) Destroy(grillPreview);
            if (purifierPreview != null) Destroy(purifierPreview);
            IsCellOccupied = null;
            IsInitialized = false;
            Changed = null;
        }
    }
}
