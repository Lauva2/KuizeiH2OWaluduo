using System;
using System.Collections.Generic;
using UnityEngine;

namespace RaftGame.Resources
{
    /// <summary>Total = available + reserved. Reservations belong to blueprint objects, not coordinates.</summary>
    public sealed class ResourceManager : MonoBehaviour
    {
        private readonly Dictionary<object, ResourceCost> reservations = new Dictionary<object, ResourceCost>();
        private ResourceCost total;
        private ResourceCost reserved;
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public int ReservationCount => reservations.Count;
        public event Action Changed;

        public bool Initialize(ResourceConfig config)
        {
            if (IsInitialized) return true;
            if (config == null || !config.StartingInventory.IsValid) return false;
            total = config.StartingInventory;
            reserved = default;
            reservations.Clear();
            IsInitialized = true;
            Notify();
            return true;
        }

        public int GetTotal(ResourceType type) => Value(total, type);
        public int GetReserved(ResourceType type) => Value(reserved, type);
        public int GetAvailable(ResourceType type) => GetTotal(type) - GetReserved(type);

        private static int Value(ResourceCost cost, ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return cost.Wood;
                case ResourceType.Scrap: return cost.Scrap;
                case ResourceType.Fish: return cost.Fish;
                case ResourceType.CookedFish: return cost.CookedFish;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public bool CanAfford(ResourceCost cost) => IsInitialized && cost.IsValid &&
            cost.Wood <= total.Wood - reserved.Wood && cost.Scrap <= total.Scrap - reserved.Scrap &&
            cost.Fish <= total.Fish - reserved.Fish && cost.CookedFish <= total.CookedFish - reserved.CookedFish;

        public bool CanAffordAfterRelease(object owner, ResourceCost cost)
        {
            if (!IsInitialized || !cost.IsValid) return false;
            ResourceCost held = default;
            if (owner != null) reservations.TryGetValue(owner, out held);
            return cost.Wood <= total.Wood - reserved.Wood + held.Wood && cost.Scrap <= total.Scrap - reserved.Scrap + held.Scrap &&
                cost.Fish <= total.Fish - reserved.Fish + held.Fish && cost.CookedFish <= total.CookedFish - reserved.CookedFish + held.CookedFish;
        }

        public bool Add(ResourceType type, int amount)
        {
            if (!IsInitialized || amount <= 0) return false;
            int current = GetTotal(type);
            if (current > int.MaxValue - amount) return false;
            if (type == ResourceType.Wood) total.Wood += amount;
            else if (type == ResourceType.Scrap) total.Scrap += amount;
            else if (type == ResourceType.Fish) total.Fish += amount;
            else total.CookedFish += amount;
            Notify();
            return true;
        }

        public bool TryReserve(object owner, ResourceCost cost)
        {
            if (owner == null || reservations.ContainsKey(owner) || !CanAfford(cost)) return false;
            reservations.Add(owner, cost);
            reserved.Wood += cost.Wood;
            reserved.Scrap += cost.Scrap;
            reserved.Fish += cost.Fish;
            reserved.CookedFish += cost.CookedFish;
            Notify();
            return true;
        }

        public bool Commit(object owner) => TryConvert(owner, default);

        // Validate all outputs before consuming any inputs; one event observes the entire transaction.
        public bool TryConvert(object owner, ResourceCost output)
        {
            if (!IsInitialized || !output.IsValid || owner == null || !reservations.TryGetValue(owner, out ResourceCost cost)) return false;
            long wood = (long)total.Wood - cost.Wood + output.Wood;
            long scrap = (long)total.Scrap - cost.Scrap + output.Scrap;
            long fish = (long)total.Fish - cost.Fish + output.Fish;
            long cooked = (long)total.CookedFish - cost.CookedFish + output.CookedFish;
            if (wood > int.MaxValue || scrap > int.MaxValue || fish > int.MaxValue || cooked > int.MaxValue) return false;
            reservations.Remove(owner);
            total = new ResourceCost((int)wood, (int)scrap, (int)fish, (int)cooked);
            reserved.Wood -= cost.Wood;
            reserved.Scrap -= cost.Scrap;
            reserved.Fish -= cost.Fish;
            reserved.CookedFish -= cost.CookedFish;
            Notify();
            return true;
        }

        public bool Release(object owner)
        {
            if (owner == null || !reservations.TryGetValue(owner, out ResourceCost cost)) return false;
            reservations.Remove(owner);
            reserved.Wood -= cost.Wood;
            reserved.Scrap -= cost.Scrap;
            reserved.Fish -= cost.Fish;
            reserved.CookedFish -= cost.CookedFish;
            Notify();
            return true;
        }

        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            reservations.Clear();
            reserved = default;
            IsInitialized = false;
            Changed = null;
        }
    }
}
