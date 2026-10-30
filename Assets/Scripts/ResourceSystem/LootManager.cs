using System;
using UnityEngine;
using RaftGame.Building;
namespace RaftGame.Resources
{
    [Serializable] public struct LootState
    { public int RandomState, CatchesWithoutWater, CratesWithoutBlueprint; }

    public sealed class LootManager : MonoBehaviour
    {
        private LootConfig configuration;
        private ResourceManager resources;
        private BuildManager building;
        private LootState state;
        private bool awarding;
        public bool IsInitialized { get; private set; }
        public string Message { get; private set; }
        public ResourceType LastResource { get; private set; }
        public event Action Changed;
        public bool Initialize(LootConfig config, ResourceManager inventory, BuildManager build)
        {
            if (config == null || config.FishWeight < 0 || config.WaterWeight < 0 || config.WoodWeight < 0 || config.ScrapWeight < 0 ||
                (long)config.FishWeight + config.WaterWeight + config.WoodWeight + config.ScrapWeight < 1 || inventory == null || !inventory.IsInitialized || build == null || !build.IsInitialized) return false;
            configuration = config; resources = inventory; building = build;
            state = new LootState { RandomState = SeaRandom.Seed() }; IsInitialized = true; return true;
        }
        public LootState Capture() => state;
        public void Restore(LootState saved) { state = saved; Message = null; Changed?.Invoke(); }
        public bool AwardCatch(int fishAmount)
        {
            if (!IsInitialized || awarding) return false;
            LootState next = state;
            double total = (double)configuration.FishWeight + configuration.WaterWeight + configuration.WoodWeight + configuration.ScrapWeight;
            double roll = SeaRandom.Value(ref next.RandomState) * total;
            ResourceType kind = roll < configuration.FishWeight ? ResourceType.Fish :
                roll < configuration.FishWeight + (double)configuration.WaterWeight ? ResourceType.Water :
                roll < configuration.FishWeight + (double)configuration.WaterWeight + configuration.WoodWeight ? ResourceType.Wood : ResourceType.Scrap;
            if (next.CatchesWithoutWater >= Mathf.Max(1, configuration.WaterCatchGuarantee) - 1) kind = ResourceType.Water;
            next.CatchesWithoutWater = kind == ResourceType.Water ? 0 : next.CatchesWithoutWater + 1;
            int amount = kind == ResourceType.Fish ? Mathf.Max(1, fishAmount) : kind == ResourceType.Wood ? 2 : 1;
            ResourceCost output = kind == ResourceType.Fish ? new ResourceCost(0, 0, amount) : kind == ResourceType.Water ? new ResourceCost(0, 0, 0, 0, amount) :
                kind == ResourceType.Wood ? new ResourceCost(amount, 0) : new ResourceCost(0, amount);
            return Award(next, output, false, kind, "钓获 · " + Name(kind) + " × " + amount);
        }
        public bool OpenCrate(Action commit = null)
        {
            if (!IsInitialized || awarding) return false;
            LootState next = state;
            float reward = SeaRandom.Value(ref next.RandomState);
            ResourceType kind = reward < .45f ? ResourceType.Water : reward < .75f ? ResourceType.Wood : ResourceType.Scrap;
            int amount = kind == ResourceType.Wood ? 3 : kind == ResourceType.Water ? 2 : 2;
            var output = kind == ResourceType.Water ? new ResourceCost(0, 0, 0, 0, amount) : kind == ResourceType.Wood ? new ResourceCost(amount, 0) : new ResourceCost(0, amount);
            output.Potato = 1;
            bool found = !building.PurifierUnlocked && (SeaRandom.Value(ref next.RandomState) < Mathf.Clamp01(configuration.PurifierBlueprintChance) ||
                next.CratesWithoutBlueprint >= Mathf.Max(2, configuration.BlueprintCrateGuarantee) - 1);
            next.CratesWithoutBlueprint = found || building.PurifierUnlocked ? 0 : next.CratesWithoutBlueprint + 1;
            string message = found ? "发现海水净化器图纸 · 按 B 查看 · 土豆 × 1" : "打开补给箱 · " + Name(kind) + " × " + amount + " · 土豆 × 1";
            return Award(next, output, found, kind, message, commit);
        }
        private bool Award(LootState next, ResourceCost output, bool unlock, ResourceType kind, string message, Action commit = null)
        {
            awarding = true; LootState previous = state; state = next;
            try
            {
                if (!resources.TryGrant(output, () => { commit?.Invoke(); if (unlock) building.UnlockPurifier(); })) { state = previous; return false; }
                LastResource = kind; Message = message; Changed?.Invoke(); return true;
            }
            finally { awarding = false; }
        }
        public static string Name(ResourceType kind) => kind == ResourceType.Water ? "瓶装淡水" : kind == ResourceType.Fish ? "鲜鱼" : kind == ResourceType.Wood ? "木材" : "废料";
        public void Shutdown() { IsInitialized = false; Changed = null; }
    }
}
