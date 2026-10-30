using System;
using System.Collections.Generic;
using UnityEngine;

namespace RaftGame.Resources
{
    /// <summary>Total = available + reserved. Every recipe and output uses the same checked transaction path.</summary>
    public sealed class ResourceManager : MonoBehaviour
    {
        public const int TypeCount = (int)ResourceType.BakedPotato + 1;
        private readonly Dictionary<object, ResourceCost> reservations = new Dictionary<object, ResourceCost>();
        private ResourceCost total, reserved;
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public int ReservationCount => reservations.Count;
        public event Action Changed;
        public bool Initialize(ResourceConfig config)
        {
            if (IsInitialized) return true;
            if (config == null || !config.StartingInventory.IsValid) return false;
            total = config.StartingInventory; reserved = default; reservations.Clear(); IsInitialized = true; Notify(); return true;
        }
        public int GetTotal(ResourceType type) => total.Get(type);
        public int GetReserved(ResourceType type) => reserved.Get(type);
        public int GetAvailable(ResourceType type) => GetTotal(type) - GetReserved(type);
        public bool CanAfford(ResourceCost cost) => CanAffordAfterRelease(null, cost);
        public bool CanAffordAfterRelease(object owner, ResourceCost cost)
        {
            if (!IsInitialized || !cost.IsValid) return false;
            ResourceCost held = default; if (owner != null) reservations.TryGetValue(owner, out held);
            for (int index = 0; index < TypeCount; index++)
            { var type = (ResourceType)index; if ((long)cost.Get(type) > (long)GetAvailable(type) + held.Get(type)) return false; }
            return true;
        }
        public bool Add(ResourceType type, int amount) => amount > 0 && TryGrant(ResourceCost.Single(type, amount));
        public bool TryReserve(object owner, ResourceCost cost)
        {
            if (owner == null || reservations.ContainsKey(owner) || !CanAfford(cost)) return false;
            reservations.Add(owner, cost); AdjustReserved(cost, 1); Notify(); return true;
        }
        public bool CanGrant(ResourceCost output)
        {
            if (!IsInitialized || !output.IsValid) return false;
            for (int index = 0; index < TypeCount; index++)
            { var type = (ResourceType)index; if ((long)GetTotal(type) + output.Get(type) > int.MaxValue) return false; }
            return true;
        }
        public bool TryGrant(ResourceCost output) => TryGrant(output, null);
        internal bool TryGrant(ResourceCost output, Action commit)
        {
            if (!CanGrant(output)) return false;
            for (int index = 0; index < TypeCount; index++) { var type = (ResourceType)index; total.Set(type, total.Get(type) + output.Get(type)); }
            commit?.Invoke(); Notify(); return true;
        }
        public bool Commit(object owner) => TryConvert(owner, default);
        public bool TryConvert(object owner, ResourceCost output) => TryConvert(owner, output, null);
        internal bool TryConvert(object owner, ResourceCost output, Action commit)
        {
            if (!IsInitialized || !output.IsValid || owner == null || !reservations.TryGetValue(owner, out var cost)) return false;
            var next = new ResourceCost();
            for (int index = 0; index < TypeCount; index++)
            {
                var type = (ResourceType)index; long value = (long)total.Get(type) - cost.Get(type) + output.Get(type);
                if (value < 0 || value > int.MaxValue) return false; next.Set(type, (int)value);
            }
            reservations.Remove(owner); total = next; AdjustReserved(cost, -1); commit?.Invoke(); Notify(); return true;
        }
        public bool Release(object owner)
        {
            if (owner == null || !reservations.TryGetValue(owner, out var cost)) return false;
            reservations.Remove(owner); AdjustReserved(cost, -1); Notify(); return true;
        }
        private void AdjustReserved(ResourceCost cost, int sign)
        { for (int index = 0; index < TypeCount; index++) { var type = (ResourceType)index; reserved.Set(type, reserved.Get(type) + cost.Get(type) * sign); } }
        public ResourceCost CaptureInventory() => total;
        public void RestoreInventory(ResourceCost inventory)
        {
            if (!inventory.IsValid) throw new ArgumentException("Invalid saved inventory.");
            reservations.Clear(); reserved = default; total = inventory; Notify();
        }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown() { reservations.Clear(); reserved = default; IsInitialized = false; Changed = null; }
    }
}
