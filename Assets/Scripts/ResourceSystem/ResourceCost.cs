using System;
using UnityEngine;

namespace RaftGame.Resources
{
    public enum ResourceType { Wood, Scrap, Fish, CookedFish }

    [Serializable]
    public struct ResourceCost
    {
        [Min(0)] public int Wood;
        [Min(0)] public int Scrap;
        [Min(0)] public int Fish;
        [Min(0)] public int CookedFish;
        public ResourceCost(int wood, int scrap, int fish = 0, int cookedFish = 0)
        { Wood = wood; Scrap = scrap; Fish = fish; CookedFish = cookedFish; }
        public bool IsValid => Wood >= 0 && Scrap >= 0 && Fish >= 0 && CookedFish >= 0;
    }
}
