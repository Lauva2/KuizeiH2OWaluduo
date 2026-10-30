using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;

namespace RaftGame.UI
{
    public sealed class BuildPanelModel : UIModel
    {
        public int Completed { get; private set; }
        public int Blueprints { get; private set; }
        public BuildTool Tool { get; private set; }
        public string Message { get; private set; }
        public string CrewStatus { get; private set; }
        public string WoodStatus { get; private set; }
        public string ScrapStatus { get; private set; }
        public string CostStatus { get; private set; }
        public string FishStatus { get; private set; }
        public bool FishingEnabled { get; private set; }
        public string CookedFishStatus { get; private set; }
        public string NeedsStatus { get; private set; }
        public int Grills { get; private set; }
        public int FoodTarget { get; private set; }
        public float Satiety { get; private set; }
        public float Hydration { get; private set; }
        public float Energy { get; private set; }
        public string EnergyStatus { get; private set; }
        public int WaterTarget { get; private set; }
        public int Purifiers { get; private set; }
        public int Nets { get; private set; }
        public string WaterStatus { get; private set; }

        public void Refresh(BuildManager manager, CrewManager crew = null, ResourceManager resources = null, SalvageManager salvage = null,
            FishingManager fishing = null, ProductionManager production = null, NeedsManager needs = null, WaterManager water = null)
        {
            Completed = manager.CompletedCount;
            Blueprints = manager.BlueprintCount;
            Tool = manager.Tool;
            Message = manager.Message;
            if (manager.Tool == BuildTool.None && salvage != null) Message = salvage.Message;
            if (manager.Tool == BuildTool.None && crew != null && crew.Recruitment != null && crew.Recruitment.HasRaft) Message = crew.Recruitment.Message;
            CrewStatus = crew != null && crew.IsInitialized ? crew.StatusText : "等待船员加入";
            WoodStatus = Count(resources, ResourceType.Wood);
            ScrapStatus = Count(resources, ResourceType.Scrap);
            bool facility = FacilityRules.TryKind(Tool, out var kind);
            ResourceCost cost = facility ? manager.FacilityCost(kind) : manager.DeckCost;
            CostStatus = resources != null ? $"{(facility ? FacilityRules.Name(kind) : "甲板")}：木材 {cost.Wood} + 废料 {cost.Scrap}" : "自由规划";
            if (Tool == BuildTool.None) CostStatus = "B 打开图册 · 发现更多图纸";
            FishStatus = Count(resources, ResourceType.Fish);
            FishingEnabled = fishing != null && fishing.AutomaticFishing;
            Grills = manager.GrillCount;
            FoodTarget = production != null ? production.Target : 0;
            CookedFishStatus = Count(resources, ResourceType.CookedFish);
            WaterStatus = Count(resources, ResourceType.Water);
            WaterTarget = water != null ? water.Target : 0;
            Purifiers = manager.PurifierCount;
            Nets = manager.NetCount;
            Satiety = needs != null ? needs.Satiety : 0f;
            Hydration = needs != null ? needs.Hydration : 0f;
            Energy = needs != null ? needs.Energy : 0;
            NeedsStatus = needs != null ? $"饱腹 {UnityEngine.Mathf.FloorToInt(Satiety)}%     水分 {UnityEngine.Mathf.FloorToInt(Hydration)}%     效率 {UnityEngine.Mathf.RoundToInt(needs.Efficiency * 100f)}%" : "船员状态 —";
            if (crew != null && crew.Count > 1)
            {
                float foodTotal = 0, waterTotal = 0, efficiency = 0, energy = 0;
                foreach (var member in crew.Members) { foodTotal += member.Needs.Satiety; waterTotal += member.Needs.Hydration; efficiency += member.Needs.Efficiency; energy += member.Needs.Energy; }
                Satiety = foodTotal / crew.Count; Hydration = waterTotal / crew.Count; Energy = energy / crew.Count;
                NeedsStatus = $"平均饱腹 {UnityEngine.Mathf.FloorToInt(Satiety)}%   水分 {UnityEngine.Mathf.FloorToInt(Hydration)}%   效率 {UnityEngine.Mathf.RoundToInt(efficiency / crew.Count * 100)}%";
            }
            EnergyStatus = (crew != null && crew.Count > 1 ? "平均精力 " : "精力 ") + UnityEngine.Mathf.FloorToInt(Energy) + "% · C 查看休息状态";
            NotifyChanged();
        }
        private static string Count(ResourceManager resources, ResourceType type) => resources != null ? resources.GetTotal(type).ToString() : "—";
    }
}
