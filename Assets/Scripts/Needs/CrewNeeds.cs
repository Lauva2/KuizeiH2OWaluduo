using System;
using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Needs
{
    public sealed class MealSession
    {
        public ResourceType Food { get; internal set; }
        public float Progress { get; internal set; }
        internal object Owner;
        internal bool Ended;
    }
    public sealed class DrinkSession
    {
        public float Progress { get; internal set; }
        internal object Owner;
        internal bool Ended;
    }

    /// <summary>Gentle hunger: reduced work speed only, with no health or death state.</summary>
    public sealed class CrewNeeds
    {
        private NeedsConfig configuration;
        private ResourceManager resources;
        private MealSession active;
        private DrinkSession drink;
        private int previousHydration = -1;
        private int previousSatiety = -1;
        private int previousEfficiency = -1;
        private int previousEnergy = -1;
        public bool IsInitialized { get; private set; }
        public float Satiety { get; private set; }
        public float Hydration { get; private set; }
        public float Energy { get; private set; }
        public bool RestRequested { get; private set; }
        public float Efficiency => !IsInitialized ? 1f : Mathf.Lerp(Mathf.Clamp(configuration.MinimumEfficiency, 0.1f, 1f), 1f,
            Mathf.Min(Mathf.Clamp01(Energy / Mathf.Max(1, configuration.TiredBelow)),
                Mathf.Min(Mathf.Clamp01(Satiety / Mathf.Max(1f, configuration.HungryBelow)), Mathf.Clamp01(Hydration / Mathf.Max(1f, configuration.ThirstyBelow)))));
        public bool ShouldEat => IsInitialized && active == null && drink == null && Satiety <= configuration.EatBelow && (resources.GetAvailable(ResourceType.CookedFish) > 0 || resources.GetAvailable(ResourceType.BakedPotato) > 0);
        public bool ShouldDrink => IsInitialized && active == null && drink == null && Hydration <= configuration.DrinkBelow && resources.GetAvailable(ResourceType.Water) > 0;
        public bool WantsDrink => IsInitialized && drink == null && Hydration <= configuration.DrinkBelow;
        public int Version { get; private set; }
        public event Action Changed;

        public bool Initialize(NeedsConfig config, ResourceManager inventory)
        {
            if (IsInitialized) return true;
            if (config == null || inventory == null || !inventory.IsInitialized) return false;
            configuration = config; resources = inventory; IsInitialized = true; Energy = Mathf.Clamp(config.StartingEnergy, 0, 100);
            Hydration = Mathf.Clamp(config.StartingHydration, 0, 100); SetSatiety(config.StartingSatiety); return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsInitialized || deltaTime <= 0f) return;
            SetSatiety(Satiety - Mathf.Max(0f, configuration.DrainPerSecond) * deltaTime);
            SetHydration(Hydration - Mathf.Max(0f, configuration.ThirstDrainPerSecond) * deltaTime);
        }

        public void SetHydration(float value)
        {
            if (!IsInitialized || float.IsNaN(value)) return;
            Hydration = Mathf.Clamp(value, 0, 100);
            int level = Mathf.FloorToInt(Hydration), efficiency = Mathf.RoundToInt(Efficiency * 100);
            if (level == previousHydration && efficiency == previousEfficiency) return;
            previousHydration = level; previousEfficiency = efficiency; Notify();
        }
        public void TickEnergy(float deltaTime, bool working)
        {
            if (!IsInitialized || deltaTime <= 0) return;
            SetEnergy(Energy - Mathf.Max(0, working ? configuration.WorkEnergyDrain : configuration.IdleEnergyDrain) * deltaTime);
        }
        public void SetEnergy(float value)
        {
            if (!IsInitialized || float.IsNaN(value) || float.IsInfinity(value)) return;
            Energy = Mathf.Clamp(value, 0, 100);
            int level = Mathf.FloorToInt(Energy), efficiency = Mathf.RoundToInt(Efficiency * 100);
            if (level == previousEnergy && efficiency == previousEfficiency) return;
            previousEnergy = level; previousEfficiency = efficiency; Notify();
        }
        public void SetRestRequested(bool value) { if (RestRequested == value) return; RestRequested = value; Notify(); }
        public DrinkSession TryBeginDrink(object owner)
        {
            if (owner == null || !ShouldDrink) return null;
            var session = new DrinkSession { Owner = owner }; drink = session;
            if (!resources.TryReserve(session, new ResourceCost(0, 0, 0, 0, 1))) { drink = null; return null; }
            Notify(); return session;
        }
        private bool ValidDrink(DrinkSession session, object owner) => IsInitialized && session != null && ReferenceEquals(session, drink) && !session.Ended && ReferenceEquals(session.Owner, owner);
        public bool AdvanceDrink(DrinkSession session, object owner, float deltaTime)
        {
            if (!ValidDrink(session, owner)) return false;
            session.Progress = Mathf.Clamp01(session.Progress + Mathf.Max(0, deltaTime) / Mathf.Max(0.1f, configuration.DrinkingDuration));
            if (session.Progress < 1) return true;
            session.Ended = true; drink = null;
            if (!resources.Commit(session)) { Notify(); return false; }
            SetHydration(Hydration + Mathf.Max(1, configuration.DrinkRestore)); Notify(); return true;
        }
        public void CancelDrink(DrinkSession session, object owner)
        {
            if (!ValidDrink(session, owner)) return;
            session.Ended = true; drink = null; resources.Release(session); Notify();
        }

        public void SetSatiety(float value)
        {
            if (!IsInitialized || float.IsNaN(value)) return;
            Satiety = Mathf.Clamp(value, 0f, 100f);
            int satiety = Mathf.FloorToInt(Satiety), efficiency = Mathf.RoundToInt(Efficiency * 100f);
            if (satiety == previousSatiety && efficiency == previousEfficiency) return;
            previousSatiety = satiety; previousEfficiency = efficiency; Notify();
        }

        public MealSession TryBeginMeal(object owner)
        {
            if (owner == null || !ShouldEat) return null;
            var meal = new MealSession { Owner = owner, Food = resources.GetAvailable(ResourceType.CookedFish) > 0 ? ResourceType.CookedFish : ResourceType.BakedPotato };
            active = meal;
            if (!resources.TryReserve(meal, ResourceCost.Single(meal.Food))) { active = null; return null; }
            Notify(); return meal;
        }

        public bool Advance(MealSession meal, object owner, float deltaTime)
        {
            if (!Valid(meal, owner)) return false;
            meal.Progress = Mathf.Clamp01(meal.Progress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.1f, configuration.EatingDuration));
            if (meal.Progress < 1f) return true;
            meal.Ended = true; active = null;
            if (!resources.Commit(meal)) { Notify(); return false; }
            SetSatiety(Satiety + Mathf.Max(1f, configuration.MealRestore)); Notify(); return true;
        }

        private bool Valid(MealSession meal, object owner) => IsInitialized && meal != null && !meal.Ended && ReferenceEquals(active, meal) && ReferenceEquals(meal.Owner, owner);
        public void Cancel(MealSession meal, object owner)
        {
            if (!Valid(meal, owner)) return;
            meal.Ended = true; active = null; resources.Release(meal); Notify();
        }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            if (active != null) Cancel(active, active.Owner);
            if (drink != null) CancelDrink(drink, drink.Owner);
            IsInitialized = false; Changed = null;
        }
    }
}
