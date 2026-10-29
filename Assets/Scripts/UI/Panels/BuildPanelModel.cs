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

        public void Refresh(BuildManager manager, CrewManager crew = null, ResourceManager resources = null, SalvageManager salvage = null,
            FishingManager fishing = null, ProductionManager production = null, NeedsManager needs = null)
        {
            Completed = manager.CompletedCount;
            Blueprints = manager.BlueprintCount;
            Tool = manager.Tool;
            Message = manager.Message;
            if (manager.Tool == BuildTool.None && salvage != null) Message = salvage.Message;
            CrewStatus = crew != null && crew.IsInitialized ? crew.StatusText : "等待船员加入";
            WoodStatus = resources != null ? $"木材 {resources.GetTotal(ResourceType.Wood)}  ·  可用 {resources.GetAvailable(ResourceType.Wood)}  ·  预留 {resources.GetReserved(ResourceType.Wood)}" : "木材 —";
            ScrapStatus = resources != null ? $"废料 {resources.GetTotal(ResourceType.Scrap)}  ·  可用 {resources.GetAvailable(ResourceType.Scrap)}  ·  预留 {resources.GetReserved(ResourceType.Scrap)}" : "废料 —";
            ResourceCost cost = Tool == BuildTool.GrillBlueprint ? manager.GrillCost : manager.DeckCost;
            CostStatus = resources != null ? $"{(Tool == BuildTool.GrillBlueprint ? "烤架" : "甲板")}：木材 {cost.Wood} + 废料 {cost.Scrap}" : "自由规划";
            FishStatus = resources != null ? $"鲜鱼 {resources.GetTotal(ResourceType.Fish)}  ·  鱼竿 {(fishing != null ? fishing.RodCount : 0)}" : "鲜鱼 —";
            FishingEnabled = fishing != null && fishing.AutomaticFishing;
            Grills = manager.GrillCount;
            FoodTarget = production != null ? production.Target : 0;
            CookedFishStatus = resources != null ? $"熟鱼 {resources.GetTotal(ResourceType.CookedFish)}  ·  目标 {FoodTarget}" : "熟鱼 —";
            NeedsStatus = needs != null ? $"饱腹 {UnityEngine.Mathf.FloorToInt(needs.Satiety)}% · 效率 {UnityEngine.Mathf.RoundToInt(needs.Efficiency * 100f)}%" : "饱腹 —";
            NotifyChanged();
        }
    }
}
