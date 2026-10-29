using System;
using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Needs
{
    public sealed class MealSession
    {
        public float Progress { get; internal set; }
        internal object Owner;
        internal bool Ended;
    }

    /// <summary>Gentle hunger: reduced work speed only, with no health or death state.</summary>
    public sealed class NeedsManager : MonoBehaviour
    {
        private NeedsConfig configuration;
        private ResourceManager resources;
        private MealSession active;
        private int previousSatiety = -1;
        private int previousEfficiency = -1;
        public bool IsInitialized { get; private set; }
        public float Satiety { get; private set; }
        public float Efficiency => !IsInitialized ? 1f : Mathf.Lerp(Mathf.Clamp(configuration.MinimumEfficiency, 0.1f, 1f), 1f,
            Mathf.Clamp01(Satiety / Mathf.Max(1f, configuration.HungryBelow)));
        public bool ShouldEat => IsInitialized && active == null && Satiety <= configuration.EatBelow && resources.GetAvailable(ResourceType.CookedFish) > 0;
        public int Version { get; private set; }
        public event Action Changed;

        public bool Initialize(NeedsConfig config, ResourceManager inventory)
        {
            if (IsInitialized) return true;
            if (config == null || inventory == null || !inventory.IsInitialized) return false;
            configuration = config; resources = inventory; IsInitialized = true; SetSatiety(config.StartingSatiety); return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsInitialized || deltaTime <= 0f) return;
            SetSatiety(Satiety - Mathf.Max(0f, configuration.DrainPerSecond) * deltaTime);
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
            var meal = new MealSession { Owner = owner };
            active = meal;
            if (!resources.TryReserve(meal, new ResourceCost(0, 0, 0, 1))) { active = null; return null; }
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
            IsInitialized = false; Changed = null;
        }
    }
}
