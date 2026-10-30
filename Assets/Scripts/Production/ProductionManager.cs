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
        public ResourceType Output { get; internal set; }
        internal ResourceCost Recipe;
        internal object Owner;
        internal bool Begun;
        internal bool Ended;
    }

    /// <summary>Claims each grill independently; reserves fuel and fish on arrival.</summary>
    public sealed class ProductionManager : MonoBehaviour
    {
        private ProductionConfig configuration;
        private BuildManager building;
        private ResourceManager resources;
        private RaftGame.Farming.FarmingManager farming;
        public long FoodCount => (long)resources.GetTotal(ResourceType.CookedFish) + resources.GetTotal(ResourceType.BakedPotato);
        private readonly Dictionary<GrillCell, CookingSession> active = new Dictionary<GrillCell, CookingSession>();
        public bool IsInitialized { get; private set; }
        public int Target { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;

        public bool Initialize(ProductionConfig config, BuildManager build, ResourceManager inventory, RaftGame.Farming.FarmingManager crops = null)
        {
            if (IsInitialized) return true;
            if (config == null || !config.Recipe.IsValid || config.Recipe.Fish < 1 || config.Recipe.Wood < 1 ||
                config.Recipe.CookedFish != 0 || !config.PotatoRecipe.IsValid || config.PotatoRecipe.Potato < 1 || config.PotatoRecipe.Wood < 1 || config.PotatoRecipe.BakedPotato != 0 ||
                build == null || !build.IsInitialized || inventory == null || !inventory.IsInitialized) return false;
            configuration = config; building = build; resources = inventory; farming = crops;
            Target = Mathf.Clamp(config.CookedFishTarget, 0, 20); building.FacilityChanging += InterruptFacility; IsInitialized = true; Notify(); return true;
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
            if (!IsInitialized || FoodCount + active.Count >= Target || !TryRecipe(out _, out _)) return false;
            int best = int.MaxValue;
            foreach (GrillCell grill in building.Grills.Values)
                if (grill.Kind == FacilityKind.Grill && !active.ContainsKey(grill) && !grill.IsBeingMoved && grill.State == DeckState.Complete && distances.TryGetValue(grill.WorkCell, out int distance) &&
                    (distance < best || (distance == best && (result == null || grill.Coordinate.x < result.Coordinate.x ||
                    (grill.Coordinate.x == result.Coordinate.x && grill.Coordinate.y < result.Coordinate.y)))))
                { result = grill; best = distance; }
            return result != null;
        }

        public CookingSession TryAcquire(object owner, GrillCell grill)
        {
            if (owner == null || !IsInitialized || !IsGrillValid(grill) || active.ContainsKey(grill) ||
                FoodCount + active.Count >= Target || !TryRecipe(out var recipe, out var output)) return null;
            var session = new CookingSession { Owner = owner, Grill = grill, Recipe = recipe, Output = output }; active.Add(grill, session); Notify(); return session;
        }
        private bool CanCookPotato(ResourceCost recipe) => resources.CanAfford(recipe) && (long)resources.GetAvailable(ResourceType.Potato) - (farming != null ? farming.SeedReserve : 0) >= recipe.Potato;
        private bool TryRecipe(out ResourceCost recipe, out ResourceType output)
        {
            bool fish = resources.CanAfford(configuration.Recipe), potato = CanCookPotato(configuration.PotatoRecipe);
            bool choosePotato = potato && (!fish || resources.GetTotal(ResourceType.CookedFish) > resources.GetTotal(ResourceType.BakedPotato));
            recipe = choosePotato ? configuration.PotatoRecipe : configuration.Recipe; output = choosePotato ? ResourceType.BakedPotato : ResourceType.CookedFish;
            return fish || potato;
        }

        private bool IsGrillValid(GrillCell grill) => grill != null && !grill.IsBeingMoved && grill.Kind == FacilityKind.Grill && grill.State == DeckState.Complete &&
            building.Grills.TryGetValue(grill.Coordinate, out GrillCell current) && ReferenceEquals(grill, current);

        public bool IsValid(CookingSession session, object owner) => IsInitialized && session != null &&
            active.TryGetValue(session.Grill, out var current) && ReferenceEquals(current, session) && !session.Ended && ReferenceEquals(session.Owner, owner) && IsGrillValid(session.Grill);

        public bool Begin(CookingSession session, object owner, Vector2Int cell)
        {
            if (!IsValid(session, owner) || cell != session.Grill.WorkCell || session.Begun) return false;
            // Target changes affect the next batch; a batch already started can finish.
            if (FoodCount >= Target || (session.Output == ResourceType.BakedPotato && !CanCookPotato(session.Recipe)) || !resources.TryReserve(session, session.Recipe)) return false;
            session.Begun = true;
            if (session.Grill.View != null) session.Grill.View.Present(true, 0f, session.Output);
            Notify(); return true;
        }

        public bool Advance(CookingSession session, object owner, Vector2Int cell, float deltaTime)
        {
            if (!IsValid(session, owner) || !session.Begun || cell != session.Grill.WorkCell) return false;
            session.Progress = Mathf.Clamp01(session.Progress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.1f, configuration.CookingDuration));
            if (session.Grill.View != null) session.Grill.View.Present(true, session.Progress, session.Output);
            if (session.Progress < 1f) return true;
            session.Ended = true; active.Remove(session.Grill);
            if (!resources.TryConvert(session, ResourceCost.Single(session.Output)))
            { resources.Release(session); StopVisual(session); Notify(); return false; }
            StopVisual(session); Notify(); return true;
        }

        public bool CanYieldFor(CookingSession session, ResourceCost cost) =>
            IsInitialized && session != null && active.TryGetValue(session.Grill, out var current) && ReferenceEquals(session, current) && resources.CanAffordAfterRelease(session, cost);

        public void Cancel(CookingSession session, object owner)
        {
            if (!IsValid(session, owner)) return;
            session.Ended = true; active.Remove(session.Grill);
            resources.Release(session); StopVisual(session); Notify();
        }

        private static void StopVisual(CookingSession session) { if (session.Grill.View != null) session.Grill.View.Present(false, 0f); }
        private void InterruptFacility(GrillCell cell) { if (active.TryGetValue(cell, out var session)) Cancel(session, session.Owner); }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            foreach (var session in new List<CookingSession>(active.Values)) Cancel(session, session.Owner);
            if (building != null) building.FacilityChanging -= InterruptFacility;
            IsInitialized = false; Changed = null;
        }
    }
}
