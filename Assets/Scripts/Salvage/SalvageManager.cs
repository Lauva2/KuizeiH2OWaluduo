using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Ocean;
using RaftGame.Resources;
using RaftGame.Core;

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
        private LootManager loot;
        private int sequence;
        private int randomState;
        public int RandomState => randomState;
        public int Sequence => sequence;
        private float spawnTimer;
        public float SpawnTimer => spawnTimer;
        public int MaximumItems => configuration.MaximumItems;
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public IReadOnlyList<FloatingResource> Items => items;
        public int MarkedCount { get { int count = 0; foreach (FloatingResource item in items) if (item != null && item.IsMarked) count++; return count; } }
        public float GatherDuration => configuration.GatherDuration;
        public string Message { get; private set; } = "点击漂浮物标记打捞 · 再次点击取消";
        public event Action Changed;

        public bool Initialize(SalvageConfig config, BuildManager build, OceanManager sea, ResourceManager inventory, Camera camera, LootManager rewards = null)
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
            loot = rewards; randomState = SeaRandom.Seed();
            IsInitialized = true;
            for (int i = 0; i < Mathf.Min(config.StartingItems, config.MaximumItems); i++) SpawnRandom(SeaRandom.Range(ref randomState, .5f, 5));
            spawnTimer = NextInterval();
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
                Vector2 flow = ocean.GetDriftDirection(position, item.Windage);
                position += new Vector3(flow.x, 0, flow.y) * (Mathf.Max(0f, configuration.DriftSpeed) * item.DriftMultiplier * dt);
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
                SpawnRandom(SeaRandom.Range(ref randomState, configuration.SpawnAheadDistance * .45f, configuration.SpawnAheadDistance * 1.3f));
                spawnTimer = NextInterval();
            }
            if (building.CatalogOpen || building.Tool != BuildTool.None || Input.GetMouseButton(2) ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            if (Input.GetMouseButtonDown(0)) TryToggleMarkAt(sceneCamera.ScreenPointToRay(Input.mousePosition));
        }

        public bool TryToggleMarkAt(Ray ray)
        {
            if (!IsInitialized || building.CatalogOpen || building.Tool != BuildTool.None) return false;
            // Drifting objects move outside FixedUpdate; clicks must use their current visible positions,
            // including while simulation time is paused.
            Physics.SyncTransforms();
            if (!Physics.Raycast(ray, out RaycastHit hit, 220f)) return false;
            FloatingResource item = hit.collider.GetComponentInParent<FloatingResource>();
            return IsActive(item) && SetMarked(item, !item.IsMarked);
        }

        private float NextInterval() => Mathf.Max(1, configuration.SpawnInterval) * SeaRandom.Range(ref randomState, 1 - configuration.SpawnIntervalVariation, 1 + configuration.SpawnIntervalVariation);
        private void SpawnRandom(float ahead)
        {
            Vector2 flow = ocean.GetDriftDirection(building.CellToWorld(Vector2Int.zero), .3f);
            Vector2 side = new Vector2(-flow.y, flow.x);
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
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float lane = SeaRandom.Value(ref randomState);
                float lateral = lane < .35f ? lateralMin - SeaRandom.Range(ref randomState, .65f, 1.55f) : lane < .7f ? lateralMax + SeaRandom.Range(ref randomState, .65f, 1.55f) : SeaRandom.Range(ref randomState, lateralMin, lateralMax);
                Vector2 point = origin + side * lateral + flow * (upstream - ahead - SeaRandom.Range(ref randomState, 0, 2));
                Vector3 position = new Vector3(point.x, 0, point.y);
                if (DeckPathfinder.HasDeck(building.Cells, building.WorldToCell(position))) continue;
                bool overlap = false; foreach (var item in items) if (Vector2.Distance(new Vector2(item.transform.position.x, item.transform.position.z), point) < 1.5f) { overlap = true; break; }
                if (overlap) continue;
                float kindRoll = SeaRandom.Value(ref randomState);
                FloatingKind kind = kindRoll < .35f ? FloatingKind.Wood : kindRoll < .55f ? FloatingKind.Scrap : kindRoll < .75f ? FloatingKind.Bottle : FloatingKind.SupplyCrate;
                if (!Supports(kind)) kind = FloatingKind.Wood;
                Spawn(kind, position); return;
            }
        }

        public bool Supports(FloatingKind kind) => Prefab(kind) != null;
        private GameObject Prefab(FloatingKind kind) => kind == FloatingKind.Wood ? configuration.WoodPrefab : kind == FloatingKind.Scrap ? configuration.ScrapPrefab : kind == FloatingKind.Bottle ? configuration.BottlePrefab : kind == FloatingKind.SupplyCrate ? configuration.SupplyCratePrefab : null;
        public FloatingResource Spawn(ResourceType type, Vector3 worldPosition) => type == ResourceType.Wood ? Spawn(FloatingKind.Wood, worldPosition) : type == ResourceType.Scrap ? Spawn(FloatingKind.Scrap, worldPosition) : type == ResourceType.Water ? Spawn(FloatingKind.Bottle, worldPosition) : null;
        public FloatingResource Spawn(FloatingKind kind, Vector3 worldPosition) => SpawnItem(kind, worldPosition, false);
        private FloatingResource SpawnItem(FloatingKind kind, Vector3 worldPosition, bool restoring)
        {
            if (!IsInitialized || (!restoring && items.Count >= configuration.MaximumItems) || !Supports(kind)) return null;
            GameObject prefab = Prefab(kind);
            FloatingResource item = Instantiate(prefab, transform).GetComponent<FloatingResource>();
            item.Kind = kind;
            item.Amount = kind == FloatingKind.Wood ? configuration.WoodPerItem : kind == FloatingKind.Scrap ? configuration.ScrapPerItem : 1;
            item.Id = ++sequence;
            item.Phase = SeaRandom.Range(ref randomState, 0, Mathf.PI * 2);
            item.DriftMultiplier = SeaRandom.Range(ref randomState, .75f, 1.25f);
            item.Windage = SeaRandom.Range(ref randomState, kind == FloatingKind.Bottle ? .5f : .1f, kind == FloatingKind.Bottle ? .9f : .5f);
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
            Message = marked ? "已标记" + Name(item.Kind) + "，船员会在够得着时打捞" : "已取消打捞标记";
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
            bool received = item.Kind == FloatingKind.SupplyCrate ? loot != null && loot.OpenCrate() : resources.Add(item.Type, item.Amount);
            if (!received) { item.Collecting = false; return false; }
            Message = item.Kind == FloatingKind.SupplyCrate ? loot.Message : "收集到" + Name(item.Kind) + " × " + item.Amount;
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
        public List<FloatingSave> CaptureItems()
        {
            var result = new List<FloatingSave>();
            foreach (var item in items) if (item != null) result.Add(new FloatingSave { Type = item.Type, Kind = item.Kind, Position = item.transform.position, Age = item.Age, Phase = item.Phase, Marked = item.IsMarked, DriftMultiplier = item.DriftMultiplier, Windage = item.Windage });
            return result;
        }
        public void RestoreItems(List<FloatingSave> entries, float timer, int savedRandom = 0, int savedSequence = 0)
        {
            foreach (var item in items) if (item != null) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
            items.Clear(); sequence = 0; spawnTimer = timer;
            foreach (var entry in entries)
            {
                var item = SpawnItem(entry.Kind, entry.Position, true);
                if (item == null) continue;
                item.Age = entry.Age; item.Phase = entry.Phase; item.transform.position = entry.Position;
                item.DriftMultiplier = entry.DriftMultiplier; item.Windage = entry.Windage;
                item.transform.rotation = Quaternion.Euler(Mathf.Sin(item.Age + item.Phase) * 3f,
                    item.Phase * Mathf.Rad2Deg, Mathf.Cos(item.Age * .8f + item.Phase) * 3f);
                SetMarked(item, entry.Marked);
            }
            if (savedRandom != 0) randomState = savedRandom;
            sequence = savedSequence > 0 ? savedSequence : items.Count; Notify();
        }
        private static string Name(FloatingKind kind) => kind == FloatingKind.Wood ? "木材" : kind == FloatingKind.Scrap ? "废料" : kind == FloatingKind.Bottle ? "水瓶" : "补给箱";
        public void Shutdown()
        {
            foreach (FloatingResource item in items) if (item != null) Destroy(item.gameObject);
            items.Clear();
            IsInitialized = false;
            Changed = null;
        }
    }
}
