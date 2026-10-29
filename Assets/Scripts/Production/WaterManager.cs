using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Resources;
namespace RaftGame.Production
{
    public sealed class PurifyingSession
    {
        public GrillCell Purifier { get; internal set; }
        public float Progress { get; internal set; }
        internal object Owner;
        internal bool Begun, Ended;
    }
    public sealed class WaterManager : MonoBehaviour
    {
        private WaterConfig configuration;
        private BuildManager building;
        private ResourceManager resources;
        private PurifyingSession active;
        public bool IsInitialized { get; private set; }
        public int Target { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;
        public bool Initialize(WaterConfig config, BuildManager build, ResourceManager inventory)
        {
            if (IsInitialized) return true;
            if (config == null || !config.Recipe.IsValid || config.Recipe.Wood < 1 || config.Recipe.Water != 0 ||
                config.WaterPerBatch < 1 || config.WaterPerBatch > 20 || build == null || !build.IsInitialized || inventory == null || !inventory.IsInitialized) return false;
            configuration = config; building = build; resources = inventory;
            Target = Mathf.Clamp(config.WaterTarget, 0, 20); IsInitialized = true; Notify(); return true;
        }
        public void SetTarget(int target) { target = Mathf.Clamp(target, 0, 20); if (!IsInitialized || Target == target) return; Target = target; Notify(); }
        private bool CanStart() => IsInitialized && active == null && resources.GetTotal(ResourceType.Water) < Target && resources.CanAfford(configuration.Recipe) && resources.GetTotal(ResourceType.Water) <= int.MaxValue - configuration.WaterPerBatch;
        public bool TryFindTask(IReadOnlyDictionary<Vector2Int, int> distances, out GrillCell result)
        {
            result = null; if (!CanStart()) return false;
            int best = int.MaxValue;
            foreach (GrillCell candidate in building.Grills.Values)
                if (candidate.Kind == FacilityKind.Purifier && candidate.State == DeckState.Complete && distances.TryGetValue(candidate.WorkCell, out int distance) &&
                    (distance < best || (distance == best && (result == null || candidate.Coordinate.x < result.Coordinate.x || (candidate.Coordinate.x == result.Coordinate.x && candidate.Coordinate.y < result.Coordinate.y)))))
                { result = candidate; best = distance; }
            return result != null;
        }
        private bool Exists(GrillCell cell) => cell != null && cell.Kind == FacilityKind.Purifier && cell.State == DeckState.Complete && building.Grills.TryGetValue(cell.Coordinate, out var current) && ReferenceEquals(cell, current);
        public PurifyingSession TryAcquire(object owner, GrillCell purifier)
        {
            if (owner == null || !CanStart() || !Exists(purifier)) return null;
            active = new PurifyingSession { Owner = owner, Purifier = purifier }; Notify(); return active;
        }
        public bool IsValid(PurifyingSession session, object owner) => IsInitialized && session != null && ReferenceEquals(session, active) && !session.Ended && ReferenceEquals(session.Owner, owner) && Exists(session.Purifier);
        public bool Begin(PurifyingSession session, object owner, Vector2Int cell)
        {
            if (!IsValid(session, owner) || session.Begun || cell != session.Purifier.WorkCell || resources.GetTotal(ResourceType.Water) >= Target || !resources.TryReserve(session, configuration.Recipe)) return false;
            session.Begun = true; session.Purifier.WaterView?.Present(true, 0); Notify(); return true;
        }
        public bool Advance(PurifyingSession session, object owner, Vector2Int cell, float deltaTime)
        {
            if (!IsValid(session, owner) || !session.Begun || cell != session.Purifier.WorkCell) return false;
            session.Progress = Mathf.Clamp01(session.Progress + Mathf.Max(0, deltaTime) / Mathf.Max(0.1f, configuration.PurifyingDuration));
            session.Purifier.WaterView?.Present(true, session.Progress);
            if (session.Progress < 1) return true;
            session.Ended = true; active = null;
            bool converted = resources.TryConvert(session, new ResourceCost(0, 0, 0, 0, configuration.WaterPerBatch));
            if (!converted) resources.Release(session);
            session.Purifier.WaterView?.Present(false, 0); Notify(); return converted;
        }
        public bool CanYieldFor(PurifyingSession session, ResourceCost cost) => IsInitialized && ReferenceEquals(session, active) && resources.CanAffordAfterRelease(session, cost);
        public void Cancel(PurifyingSession session, object owner)
        {
            if (!IsValid(session, owner)) return;
            session.Ended = true; active = null; resources.Release(session); session.Purifier.WaterView?.Present(false, 0); Notify();
        }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown() { if (active != null) Cancel(active, active.Owner); IsInitialized = false; Changed = null; }
    }
}
