using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Ocean;
using RaftGame.Resources;

namespace RaftGame.Fishing
{
    public readonly struct FishingSpot
    {
        public Vector2Int Cell { get; }
        public Vector2Int Direction { get; }
        public FishingSpot(Vector2Int cell, Vector2Int direction) { Cell = cell; Direction = direction; }
    }

    public sealed class FishingSession
    {
        public FishingSpot Spot { get; }
        public float Progress { get; internal set; }
        internal object Owner;
        internal bool Ended;
        internal FishingSession(FishingSpot spot, object owner) { Spot = spot; Owner = owner; }
    }

    /// <summary>Owns the starter rod and each single-use catch. CrewManager owns travel and task priority.</summary>
    public sealed class FishingManager : MonoBehaviour
    {
        private FishingConfig configuration;
        private BuildManager building;
        private ResourceManager resources;
        private OceanManager ocean;
        private FishingSession active;
        private LootManager loot;
        public bool IsInitialized { get; private set; }
        public bool AutomaticFishing { get; private set; }
        public int RodCount { get; private set; }
        public int AvailableRods => RodCount - (active != null ? 1 : 0);
        public int Version { get; private set; }
        public event Action Changed;

        public bool Initialize(FishingConfig config, BuildManager build, ResourceManager inventory, OceanManager sea, LootManager rewards = null)
        {
            if (IsInitialized) return true;
            if (config == null || build == null || !build.IsInitialized || inventory == null || !inventory.IsInitialized || sea == null || !sea.IsInitialized) return false;
            configuration = config; building = build; resources = inventory; ocean = sea;
            loot = rewards;
            RodCount = Mathf.Clamp(config.StartingRods, 0, 1);
            AutomaticFishing = config.AutomaticByDefault;
            IsInitialized = true;
            Notify();
            return true;
        }

        public void ProcessInput()
        {
            if (!IsInitialized) return;
            if (Input.GetKeyDown(KeyCode.F)) SetAutomaticFishing(!AutomaticFishing);
            if (active != null && !IsSpotValid(active.Spot)) Cancel(active, active.Owner);
        }

        public void SetAutomaticFishing(bool value)
        {
            if (!IsInitialized || AutomaticFishing == value) return;
            AutomaticFishing = value;
            if (!value && active != null) Cancel(active, active.Owner);
            Notify();
        }

        public bool IsSpotValid(FishingSpot spot)
        {
            if (!IsInitialized || !DeckPathfinder.IsWalkable(building.Cells, spot.Cell) ||
                Math.Abs((long)spot.Direction.x) + Math.Abs((long)spot.Direction.y) != 1) return false;
            // Planned tiles also exclude casting water, so new construction can never grow through a line.
            return !building.Cells.ContainsKey(spot.Cell + spot.Direction);
        }

        private bool CanFish() => IsInitialized && AutomaticFishing && AvailableRods > 0 &&
            (resources.GetTotal(ResourceType.Fish) <= int.MaxValue - Mathf.Max(1, configuration.FishPerCatch) ||
             (loot != null && (resources.GetTotal(ResourceType.Water) < int.MaxValue || resources.GetTotal(ResourceType.Wood) < int.MaxValue - 1 || resources.GetTotal(ResourceType.Scrap) < int.MaxValue)));

        public bool TryFindSpot(Dictionary<Vector2Int, int> distances, out FishingSpot spot)
        {
            spot = default;
            if (!CanFish()) return false;
            bool found = false;
            int best = int.MaxValue;
            foreach (var reachable in distances)
            {
                if (reachable.Value > best) continue;
                foreach (Vector2Int direction in DeckPathfinder.Directions)
                {
                    var candidate = new FishingSpot(reachable.Key, direction);
                    if (!IsSpotValid(candidate)) continue;
                    if (!found || reachable.Value < best || Earlier(reachable.Key, spot.Cell))
                    { spot = candidate; best = reachable.Value; found = true; }
                }
            }
            return found;
        }

        private static bool Earlier(Vector2Int a, Vector2Int b) => a.x < b.x || (a.x == b.x && a.y < b.y);

        public FishingSession TryAcquire(object owner, FishingSpot spot)
        {
            if (owner == null || !CanFish() || !IsSpotValid(spot)) return null;
            active = new FishingSession(spot, owner);
            Notify();
            return active;
        }

        private bool Owns(FishingSession session, object owner) => session != null && ReferenceEquals(active, session) &&
            ReferenceEquals(session.Owner, owner) && !session.Ended;
        public bool IsSessionValid(FishingSession session, object owner) => Owns(session, owner) && AutomaticFishing && IsSpotValid(session.Spot);

        public bool Advance(FishingSession session, object owner, Vector2Int standingCell, float deltaTime, out bool caught)
        {
            caught = false;
            if (!IsSessionValid(session, owner) || standingCell != session.Spot.Cell) return false;
            if (deltaTime <= 0f) return true;
            session.Progress = Mathf.Clamp01(session.Progress + deltaTime / Mathf.Max(0.1f, configuration.CatchDuration));
            if (session.Progress < 1f) return true;
            // Retire the session before inventory callbacks; a catch can only credit once.
            session.Ended = true;
            active = null;
            caught = loot != null ? loot.AwardCatch(configuration.FishPerCatch) : resources.Add(ResourceType.Fish, Mathf.Max(1, configuration.FishPerCatch));
            Notify();
            return true;
        }

        public bool Cancel(FishingSession session, object owner)
        {
            if (!Owns(session, owner)) return false;
            session.Ended = true;
            active = null;
            Notify();
            return true;
        }

        public Vector3 GetWaterPoint(FishingSpot spot)
        {
            float distance = Mathf.Clamp(configuration.CastDistance, building.TileSize * 0.55f, building.TileSize * 1.35f);
            Vector3 position = building.CellToWorld(spot.Cell);
            Vector3 direction = (building.CellToWorld(spot.Cell + spot.Direction) - position).normalized;
            position += direction * distance;
            position.y = ocean.GetSurfaceHeight(position) + 0.06f;
            return position;
        }

        private void Notify() { Version++; Changed?.Invoke(); }
        public void RestoreEquipment(int rods, bool automatic)
        {
            if (active != null) Cancel(active, active.Owner);
            RodCount = rods; AutomaticFishing = automatic; Notify();
        }
        public void Shutdown()
        {
            if (active != null) { active.Ended = true; active = null; }
            IsInitialized = false;
            Changed = null;
        }
    }
}
