using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Resources;

namespace RaftGame.Production
{
    public sealed class CookingSession
    {
        public GrillCell Grill { get; internal set; }
        public float Progress { get; internal set; }
        internal object Owner;
        internal bool Begun;
        internal bool Ended;
    }

    /// <summary>One crew, one batch at a time. Claims a grill for travel, reserves fuel and fish on arrival.</summary>
    public sealed class ProductionManager : MonoBehaviour
    {
        private ProductionConfig configuration;
        private BuildManager building;
        private ResourceManager resources;
        private CookingSession active;
        public bool IsInitialized { get; private set; }
        public int Target { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;

        public bool Initialize(ProductionConfig config, BuildManager build, ResourceManager inventory)
        {
            if (IsInitialized) return true;
            if (config == null || !config.Recipe.IsValid || config.Recipe.Fish < 1 || config.Recipe.Wood < 1 ||
                config.Recipe.CookedFish != 0 || build == null || !build.IsInitialized || inventory == null || !inventory.IsInitialized) return false;
            configuration = config; building = build; resources = inventory;
            Target = Mathf.Clamp(config.CookedFishTarget, 0, 20); IsInitialized = true; Notify(); return true;
        }

        public void SetTarget(int target)
        {
            target = Mathf.Clamp(target, 0, 20);
            if (!IsInitialized || target == Target) return;
            Target = target; Notify();
        }

        public bool TryFindTask(IReadOnlyDictionary<Vector2Int, int> distances, out GrillCell result)
        {
            result = null;
            if (!IsInitialized || active != null || resources.GetTotal(ResourceType.CookedFish) >= Target || !resources.CanAfford(configuration.Recipe)) return false;
            int best = int.MaxValue;
            foreach (GrillCell grill in building.Grills.Values)
                if (grill.Kind == FacilityKind.Grill && grill.State == DeckState.Complete && distances.TryGetValue(grill.WorkCell, out int distance) &&
                    (distance < best || (distance == best && (result == null || grill.Coordinate.x < result.Coordinate.x ||
                    (grill.Coordinate.x == result.Coordinate.x && grill.Coordinate.y < result.Coordinate.y)))))
                { result = grill; best = distance; }
            return result != null;
        }

        public CookingSession TryAcquire(object owner, GrillCell grill)
        {
            if (owner == null || !IsInitialized || active != null || !IsGrillValid(grill) ||
                resources.GetTotal(ResourceType.CookedFish) >= Target || !resources.CanAfford(configuration.Recipe)) return null;
            active = new CookingSession { Owner = owner, Grill = grill }; Notify(); return active;
        }

        private bool IsGrillValid(GrillCell grill) => grill != null && grill.Kind == FacilityKind.Grill && grill.State == DeckState.Complete &&
            building.Grills.TryGetValue(grill.Coordinate, out GrillCell current) && ReferenceEquals(grill, current);

        public bool IsValid(CookingSession session, object owner) => IsInitialized && session != null &&
            ReferenceEquals(active, session) && !session.Ended && ReferenceEquals(session.Owner, owner) && IsGrillValid(session.Grill);

        public bool Begin(CookingSession session, object owner, Vector2Int cell)
        {
            if (!IsValid(session, owner) || cell != session.Grill.WorkCell || session.Begun) return false;
            // Target changes affect the next batch; a batch already started can finish.
            if (resources.GetTotal(ResourceType.CookedFish) >= Target || !resources.TryReserve(session, configuration.Recipe)) return false;
            session.Begun = true;
            if (session.Grill.View != null) session.Grill.View.Present(true, 0f);
            Notify(); return true;
        }

        public bool Advance(CookingSession session, object owner, Vector2Int cell, float deltaTime)
        {
            if (!IsValid(session, owner) || !session.Begun || cell != session.Grill.WorkCell) return false;
            session.Progress = Mathf.Clamp01(session.Progress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.1f, configuration.CookingDuration));
            if (session.Grill.View != null) session.Grill.View.Present(true, session.Progress);
            if (session.Progress < 1f) return true;
            session.Ended = true; active = null;
            if (!resources.TryConvert(session, new ResourceCost(0, 0, 0, 1)))
            { resources.Release(session); StopVisual(session); Notify(); return false; }
            StopVisual(session); Notify(); return true;
        }

        public bool CanYieldFor(CookingSession session, ResourceCost cost) =>
            IsInitialized && ReferenceEquals(session, active) && resources.CanAffordAfterRelease(session, cost);

        public void Cancel(CookingSession session, object owner)
        {
            if (!IsValid(session, owner)) return;
            session.Ended = true; active = null;
            resources.Release(session); StopVisual(session); Notify();
        }

        private static void StopVisual(CookingSession session) { if (session.Grill.View != null) session.Grill.View.Present(false, 0f); }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            if (active != null) Cancel(active, active.Owner);
            IsInitialized = false; Changed = null;
        }
    }
}
