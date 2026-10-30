using UnityEngine;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Production;
using RaftGame.Resources;
using RaftGame.Salvage;

namespace RaftGame.UI
{
    public sealed class FacilityPanelModel : UIModel
    {
        public string Title, Location, Work, Refund, Message;
        public int Target;
        public bool Moving, Confirming, IsNet, IsBed, IsDecoration, IsPlanter, Repeat, CanDemolish;
        public string Storage;
        public BuildTool IconTool;
        public void Refresh(FacilityManager facilities, CrewManager crew, ProductionManager food, WaterManager water, RaftGame.Needs.ComfortManager comfort = null, RaftGame.Farming.FarmingManager farming = null)
        {
            GrillCell cell = facilities.Selected;
            if (cell == null) return;
            IsNet = cell.Kind == FacilityKind.CollectionNet;
            IsBed = cell.Kind == FacilityKind.Bed;
            IsDecoration = cell.IsDecoration;
            IsPlanter = cell.Kind == FacilityKind.Planter; Repeat = cell.RepeatPlanting;
            Title = FacilityRules.Name(cell.Kind); IconTool = FacilityRules.ToolFor(cell.Kind);
            Location = $"位置 {cell.Coordinate.x}, {cell.Coordinate.y}  ·  操作位 {cell.WorkCell.x}, {cell.WorkCell.y}";
            if (IsDecoration) Location = $"位置 {cell.Coordinate.x}, {cell.Coordinate.y} · 保持甲板通行";
            Target = cell.Kind == FacilityKind.Grill ? food.Target : water.Target;
            Moving = facilities.IsMoving; Confirming = facilities.ConfirmingDemolition;
            CanDemolish = !Moving && (!IsNet || cell.Contents.Count == 0) && (!IsPlanter || cell.CropStage == RaftGame.Farming.CropStage.Empty);
            Storage = $"暂存  {cell.Contents.Count} / {CollectionNetManager.Capacity}" + (cell.Contents.Count == CollectionNetManager.Capacity ? " · 已满" : "");
            string workerStatus = crew.FacilityStatus(cell.Coordinate);
            Work = Moving ? "搬移预览 · 原址暂停生产" : workerStatus ?? "等待船员 / 补足储备时生产";
            if (IsBed) Work = Moving ? "搬移中 · 暂停安排休息" : workerStatus ?? "床位空闲 · 船员精力低时自动休息";
            if (IsDecoration && comfort != null) Work = $"同类合计 {comfort.Contribution(cell.Kind):0.#} 舒适\n回收本件减少 {comfort.RemovalContribution(cell.Kind):0.#} 分";
            if (IsPlanter && farming != null) Work = workerStatus ?? farming.Status(cell);
            if (IsNet) Work = Moving ? "搬移暂停拦截 · 网内物资保留" : workerStatus ?? (cell.Contents.Count == CollectionNetManager.Capacity ? "已满 · 等待船员收取" : "自动拦截网口经过的物资");
            ResourceCost cost = cell.BuildCost;
            Refund = $"返还：木材 {cost.Wood} · 废料 {cost.Scrap}";
            if (cost.Fish > 0 || cost.CookedFish > 0 || cost.Water > 0) Refund += $"\n鲜鱼 {cost.Fish} · 熟鱼 {cost.CookedFish} · 淡水 {cost.Water}";
            Message = facilities.Message;
            if (IsPlanter && !Moving && !Confirming) Message = "关闭后收完本轮再停种\n浇水优先保留饮用水；收空后可回收";
            if (IsNet && !Moving && !Confirming && cell.Contents.Count > 0)
            {
                Message = "";
                foreach (var supply in cell.Contents) Message += (Message.Length == 0 ? "" : " · ") + (supply.Kind == FloatingKind.Wood ? "木材" : supply.Kind == FloatingKind.Scrap ? "废料" : supply.Kind == FloatingKind.Bottle ? "水瓶" : "补给箱");
                Message += "\n收空后可拆除 · 搬移会保留物资";
            }
            NotifyChanged();
        }
    }
}
