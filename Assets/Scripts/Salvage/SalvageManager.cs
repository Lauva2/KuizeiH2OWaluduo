using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Ocean;
using RaftGame.Resources;

namespace RaftGame.Salvage
{
    /// <summary>World supply, player marks and ownership-checked collection. Never sends crew into water.</summary>
    public sealed class SalvageManager : MonoBehaviour
    {
        private readonly List<FloatingResource> items = new List<FloatingResource>();
        private SalvageConfig configuration;
        private BuildManager building;
        private OceanManager ocean;
        private ResourceManager resources;
        private Camera sceneCamera;
        private Vector2 flow;
        private Vector2 side;
        private int sequence;
        private float spawnTimer;
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public IReadOnlyList<FloatingResource> Items => items;
        public int MarkedCount { get { int count = 0; foreach (FloatingResource item in items) if (item != null && item.IsMarked) count++; return count; } }
        public float GatherDuration => configuration.GatherDuration;
        public string Message { get; private set; } = "点击漂浮物标记打捞 · 再次点击取消";
        public event Action Changed;

        public bool Initialize(SalvageConfig config, BuildManager build, OceanManager sea, ResourceManager inventory, Camera camera)
        {
            if (IsInitialized) return true;
            if (config == null || config.WoodPrefab == null || config.ScrapPrefab == null ||
                config.WoodPrefab.GetComponent<FloatingResource>() == null || config.ScrapPrefab.GetComponent<FloatingResource>() == null || build == null || !build.IsInitialized ||
                sea == null || !sea.IsInitialized || inventory == null || !inventory.IsInitialized || camera == null) return false;
            configuration = config;
            building = build;
            ocean = sea;
            resources = inventory;
            sceneCamera = camera;
            flow = config.CurrentDirection.sqrMagnitude > 0.001f ? config.CurrentDirection.normalized : Vector2.down;
            side = new Vector2(-flow.y, flow.x);
            IsInitialized = true;
            // Start with useful supply already approaching both sides of the small raft.
            for (int i = 0; i < 6; i++) SpawnInLane(i % 2 == 0 ? -1 : 1, i / 2 * 3.8f - 2f);
            spawnTimer = config.SpawnInterval;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsInitialized) return;
            float dt = Mathf.Max(0f, deltaTime);
            for (int index = items.Count - 1; index >= 0; index--)
            {
                FloatingResource item = items[index];
                if (item == null) { items.RemoveAt(index); Notify(); continue; }
                item.Age += dt;
                Vector3 position = item.transform.position;
                position += new Vector3(flow.x, 0, flow.y) * (Mathf.Max(0f, configuration.DriftSpeed) * dt);
                if (item.Age > configuration.Lifetime || DeckPathfinder.HasDeck(building.Cells, building.WorldToCell(position)))
                { Remove(item); continue; }
                position.y = ocean.GetSurfaceHeight(position) + 0.08f + Mathf.Sin(item.Age * 1.6f + item.Phase) * 0.025f;
                item.transform.position = position;
                item.transform.rotation = Quaternion.Euler(Mathf.Sin(item.Age + item.Phase) * 3f,
                    item.Phase * Mathf.Rad2Deg, Mathf.Cos(item.Age * 0.8f + item.Phase) * 3f);
            }
            spawnTimer -= dt;
            if (dt > 0f && spawnTimer <= 0f && items.Count < configuration.MaximumItems)
            {
                SpawnInLane(sequence % 2 == 0 ? -1 : 1, configuration.SpawnAheadDistance);
                spawnTimer = Mathf.Max(1f, configuration.SpawnInterval);
            }
            if (building.Tool != BuildTool.None || Input.GetMouseButton(2) ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            if (Input.GetMouseButtonDown(0)) TryToggleMarkAt(sceneCamera.ScreenPointToRay(Input.mousePosition));
        }

        public bool TryToggleMarkAt(Ray ray)
        {
            if (!IsInitialized || building.Tool != BuildTool.None) return false;
            // Drifting objects move outside FixedUpdate; clicks must use their current visible positions,
            // including while simulation time is paused.
            Physics.SyncTransforms();
            if (!Physics.Raycast(ray, out RaycastHit hit, 220f)) return false;
            FloatingResource item = hit.collider.GetComponentInParent<FloatingResource>();
            return IsActive(item) && SetMarked(item, !item.IsMarked);
        }

        private void SpawnInLane(int lane, float ahead)
        {
            Vector3 origin3 = building.CellToWorld(Vector2Int.zero);
            Vector2 origin = new Vector2(origin3.x, origin3.z);
            float lateralMin = float.PositiveInfinity, lateralMax = float.NegativeInfinity, upstream = float.PositiveInfinity;
            float half = building.TileSize * 0.5f;
            float sideExtent = half * (Mathf.Abs(side.x) + Mathf.Abs(side.y));
            float flowExtent = half * (Mathf.Abs(flow.x) + Mathf.Abs(flow.y));
            foreach (DeckCell cell in building.Cells.Values)
            {
                if (cell.State != DeckState.Complete) continue;
                Vector3 world = building.CellToWorld(cell.Coordinate);
                Vector2 relative = new Vector2(world.x, world.z) - origin;
                lateralMin = Mathf.Min(lateralMin, Vector2.Dot(relative, side) - sideExtent);
                lateralMax = Mathf.Max(lateralMax, Vector2.Dot(relative, side) + sideExtent);
                upstream = Mathf.Min(upstream, Vector2.Dot(relative, flow) - flowExtent);
            }
            float lateral = lane < 0 ? lateralMin - configuration.LaneGap : lateralMax + configuration.LaneGap;
            Vector2 point = origin + side * lateral + flow * (upstream - ahead);
            ResourceType type = sequence % 3 == 1 ? ResourceType.Scrap : ResourceType.Wood;
            Spawn(type, new Vector3(point.x, 0, point.y));
        }

        public FloatingResource Spawn(ResourceType type, Vector3 worldPosition)
        {
            if (!IsInitialized || items.Count >= configuration.MaximumItems || (type != ResourceType.Wood && type != ResourceType.Scrap)) return null;
            GameObject prefab = type == ResourceType.Wood ? configuration.WoodPrefab : configuration.ScrapPrefab;
            FloatingResource item = Instantiate(prefab, transform).GetComponent<FloatingResource>();
            item.Type = type;
            item.Amount = type == ResourceType.Wood ? configuration.WoodPerItem : configuration.ScrapPerItem;
            item.Id = ++sequence;
            item.Phase = (sequence * 1.618f) % (2f * Mathf.PI);
            worldPosition.y = ocean.GetSurfaceHeight(worldPosition) + 0.08f;
            item.transform.position = worldPosition;
            item.ShowMarker(false);
            items.Add(item);
            Notify();
            return item;
        }

        public bool IsActive(FloatingResource item) => item != null && items.Contains(item);
        public bool SetMarked(FloatingResource item, bool marked)
        {
            if (!IsActive(item) || item.IsMarked == marked) return false;
            item.IsMarked = marked;
            if (!marked) item.ClaimOwner = null;
            item.ShowMarker(marked);
            Message = marked ? "已标记" + (item.Type == ResourceType.Wood ? "木材" : "废料") + "，船员会在够得着时打捞" : "已取消打捞标记";
            Notify();
            return true;
        }

        public bool IsInReach(Vector2Int cell, FloatingResource item)
        {
            if (!IsActive(item) || !DeckPathfinder.IsWalkable(building.Cells, cell) ||
                DeckPathfinder.HasDeck(building.Cells, building.WorldToCell(item.transform.position))) return false;
            bool edge = false;
            foreach (Vector2Int direction in DeckPathfinder.Directions)
                if (!DeckPathfinder.HasDeck(building.Cells, cell + direction)) { edge = true; break; }
            if (!edge) return false;
            Vector3 delta = item.transform.position - building.CellToWorld(cell);
            return delta.x * delta.x + delta.z * delta.z <= configuration.Reach * configuration.Reach;
        }

        public bool TryFindTask(Dictionary<Vector2Int, int> distances, object owner, out FloatingResource chosen, out Vector2Int approach)
        {
            chosen = null;
            approach = default;
            int best = int.MaxValue;
            foreach (FloatingResource item in items)
            {
                if (!item.IsMarked || (item.ClaimOwner != null && !ReferenceEquals(item.ClaimOwner, owner))) continue;
                foreach (var reachable in distances)
                    if (reachable.Value <= best && IsInReach(reachable.Key, item) &&
                        (reachable.Value < best || chosen == null || item.Id < chosen.Id))
                    { chosen = item; approach = reachable.Key; best = reachable.Value; }
            }
            return chosen != null;
        }

        public bool TryClaim(FloatingResource item, object owner)
        {
            if (!IsActive(item) || !item.IsMarked || owner == null || (item.ClaimOwner != null && !ReferenceEquals(item.ClaimOwner, owner))) return false;
            item.ClaimOwner = owner;
            return true;
        }

        public bool IsClaimValid(FloatingResource item, object owner) => IsActive(item) && !item.Collecting && item.IsMarked && ReferenceEquals(item.ClaimOwner, owner);
        public void ReleaseClaim(FloatingResource item, object owner)
        {
            if (IsActive(item) && ReferenceEquals(item.ClaimOwner, owner)) item.ClaimOwner = null;
        }

        public bool TryCollect(FloatingResource item, object owner, Vector2Int approach)
        {
            if (!IsClaimValid(item, owner) || !IsInReach(approach, item)) return false;
            item.Collecting = true; // Inventory callbacks cannot re-enter collection and credit the same item twice.
            if (!resources.Add(item.Type, item.Amount)) { item.Collecting = false; return false; }
            Message = "收集到" + (item.Type == ResourceType.Wood ? "木材" : "废料") + " × " + item.Amount;
            Remove(item);
            return true;
        }

        public bool Remove(FloatingResource item)
        {
            if (!IsActive(item)) return false;
            items.Remove(item);
            item.ClaimOwner = null;
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
            Notify();
            return true;
        }

        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            foreach (FloatingResource item in items) if (item != null) Destroy(item.gameObject);
            items.Clear();
            IsInitialized = false;
            Changed = null;
        }
    }
}
