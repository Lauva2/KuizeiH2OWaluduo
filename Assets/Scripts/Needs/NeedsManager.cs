using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Needs
{
    /// <summary>Owns independent personal needs. Legacy properties refer to member 1.</summary>
    public sealed class NeedsManager : MonoBehaviour
    {
        private readonly Dictionary<int, CrewNeeds> members = new Dictionary<int, CrewNeeds>();
        private NeedsConfig configuration;
        private ResourceManager resources;
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;
        public CrewNeeds Primary => GetMember(1);
        public float Satiety => Primary != null ? Primary.Satiety : 0;
        public float Hydration => Primary != null ? Primary.Hydration : 0;
        public float Efficiency => Primary != null ? Primary.Efficiency : 1;
        public float Energy => Primary != null ? Primary.Energy : 0;
        public float StartingEnergy => configuration != null ? configuration.StartingEnergy : 85;
        public int DrinkingDemand { get { int count = 0; foreach (var member in members.Values) if (member.WantsDrink) count++; return count; } }
        public bool ShouldEat => Primary != null && Primary.ShouldEat;
        public bool ShouldDrink => Primary != null && Primary.ShouldDrink;
        public bool Initialize(NeedsConfig config, ResourceManager inventory)
        {
            if (IsInitialized) return true;
            if (config == null || inventory == null || !inventory.IsInitialized) return false;
            configuration = config; resources = inventory; IsInitialized = true; AddMember(1); return true;
        }
        public CrewNeeds GetMember(int id) => members.TryGetValue(id, out var member) ? member : null;
        public CrewNeeds AddMember(int id)
        {
            if (!IsInitialized || id < 1 || id > 2) return null;
            if (members.TryGetValue(id, out var existing)) return existing;
            var member = new CrewNeeds(); member.Initialize(configuration, resources);
            member.Changed += Notify; members.Add(id, member); Notify(); return member;
        }
        public void RemoveMember(int id)
        {
            if (id == 1 || !members.TryGetValue(id, out var member)) return;
            member.Changed -= Notify; member.Shutdown(); members.Remove(id); Notify();
        }
        public void Tick(float deltaTime) { if (IsInitialized) foreach (var member in members.Values) member.Tick(deltaTime); }
        public void SetSatiety(float value) => Primary?.SetSatiety(value);
        public void SetHydration(float value) => Primary?.SetHydration(value);
        public MealSession TryBeginMeal(object owner) => Primary?.TryBeginMeal(owner);
        public bool Advance(MealSession session, object owner, float dt) => Primary != null && Primary.Advance(session, owner, dt);
        public void Cancel(MealSession session, object owner) => Primary?.Cancel(session, owner);
        public DrinkSession TryBeginDrink(object owner) => Primary?.TryBeginDrink(owner);
        public bool AdvanceDrink(DrinkSession session, object owner, float dt) => Primary != null && Primary.AdvanceDrink(session, owner, dt);
        public void CancelDrink(DrinkSession session, object owner) => Primary?.CancelDrink(session, owner);
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            foreach (var member in members.Values) { member.Changed -= Notify; member.Shutdown(); }
            members.Clear(); IsInitialized = false; Changed = null;
        }
    }
}
