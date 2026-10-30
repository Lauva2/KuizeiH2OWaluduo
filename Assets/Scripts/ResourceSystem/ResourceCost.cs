using System;
using UnityEngine;

namespace RaftGame.Resources
{
    public enum ResourceType { Wood, Scrap, Fish, CookedFish, Water, Potato, BakedPotato }

    [Serializable]
    public struct ResourceCost
    {
        [Min(0)] public int Wood;
        [Min(0)] public int Scrap;
        [Min(0)] public int Fish;
        [Min(0)] public int CookedFish;
        [Min(0)] public int Water;
        [Min(0)] public int Potato;
        [Min(0)] public int BakedPotato;
        public ResourceCost(int wood, int scrap, int fish = 0, int cookedFish = 0, int water = 0, int potato = 0, int bakedPotato = 0)
        { Wood = wood; Scrap = scrap; Fish = fish; CookedFish = cookedFish; Water = water; Potato = potato; BakedPotato = bakedPotato; }
        public bool IsValid => Wood >= 0 && Scrap >= 0 && Fish >= 0 && CookedFish >= 0 && Water >= 0 && Potato >= 0 && BakedPotato >= 0;
        public int Get(ResourceType type) => type switch { ResourceType.Wood => Wood, ResourceType.Scrap => Scrap, ResourceType.Fish => Fish,
            ResourceType.CookedFish => CookedFish, ResourceType.Water => Water, ResourceType.Potato => Potato, ResourceType.BakedPotato => BakedPotato, _ => throw new ArgumentOutOfRangeException(nameof(type)) };
        public void Set(ResourceType type, int value)
        {
            switch (type) { case ResourceType.Wood: Wood = value; break; case ResourceType.Scrap: Scrap = value; break; case ResourceType.Fish: Fish = value; break;
                case ResourceType.CookedFish: CookedFish = value; break; case ResourceType.Water: Water = value; break; case ResourceType.Potato: Potato = value; break;
                case ResourceType.BakedPotato: BakedPotato = value; break; default: throw new ArgumentOutOfRangeException(nameof(type)); }
        }
        public static ResourceCost Single(ResourceType type, int amount = 1) { var cost = new ResourceCost(); cost.Set(type, amount); return cost; }
    }
}
