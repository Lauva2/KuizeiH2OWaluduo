using UnityEngine;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;

namespace RaftGame.UI
{
    public sealed class BuildPanelController : UIController
    {
        private readonly BuildManager manager;
        private UICommon_Build view;
        private BuildPanelModel model;
        private readonly CrewManager crew;
        private readonly ResourceManager resources;
        private readonly SalvageManager salvage;
        private readonly FishingManager fishing;
        private readonly ProductionManager production;
        private readonly NeedsManager needs;
        public BuildPanelController(BuildManager manager, CrewManager crew = null, ResourceManager resources = null, SalvageManager salvage = null,
            FishingManager fishing = null, ProductionManager production = null, NeedsManager needs = null)
        { this.manager = manager; this.crew = crew; this.resources = resources; this.salvage = salvage; this.fishing = fishing; this.production = production; this.needs = needs; }

        protected override void OnInitialize()
        {
            view = (UICommon_Build)View;
            model = (BuildPanelModel)Model;
        }

        protected override void OnOpen()
        {
            view.BuildButton.onClick.AddListener(ToggleBuild);
            view.CancelButton.onClick.AddListener(ToggleCancel);
            if (view.FishingButton != null) view.FishingButton.onClick.AddListener(ToggleFishing);
            if (view.GrillButton != null) view.GrillButton.onClick.AddListener(ToggleGrill);
            if (view.TargetMinusButton != null) view.TargetMinusButton.onClick.AddListener(LowerTarget);
            if (view.TargetPlusButton != null) view.TargetPlusButton.onClick.AddListener(RaiseTarget);
            manager.Changed += Refresh;
            if (crew != null) crew.Changed += Refresh;
            if (resources != null) resources.Changed += Refresh;
            if (salvage != null) salvage.Changed += Refresh;
            if (fishing != null) fishing.Changed += Refresh;
            if (production != null) production.Changed += Refresh;
            if (needs != null) needs.Changed += Refresh;
            model.Changed += Render;
            Refresh();
        }

        protected override void OnRefresh() => Refresh();
        private void Refresh() => model.Refresh(manager, crew, resources, salvage, fishing, production, needs);
        private void ToggleGrill() => manager.SetTool(manager.Tool == BuildTool.GrillBlueprint ? BuildTool.None : BuildTool.GrillBlueprint);
        private void LowerTarget() { if (production != null) production.SetTarget(production.Target - 1); }
        private void RaiseTarget() { if (production != null) production.SetTarget(production.Target + 1); }
        private void ToggleBuild() => manager.SetTool(manager.Tool == BuildTool.DeckBlueprint ? BuildTool.None : BuildTool.DeckBlueprint);
        private void ToggleCancel() => manager.SetTool(manager.Tool == BuildTool.CancelBlueprint ? BuildTool.None : BuildTool.CancelBlueprint);
        private void ToggleFishing() { if (fishing != null) fishing.SetAutomaticFishing(!fishing.AutomaticFishing); }

        private void Render()
        {
            view.CompletedText.text = $"已建甲板  {model.Completed}";
            view.BlueprintText.text = $"待施工  {model.Blueprints}";
            view.StatusText.text = model.Message;
            if (view.CrewText != null) view.CrewText.text = model.CrewStatus;
            if (view.WoodText != null) view.WoodText.text = model.WoodStatus;
            if (view.ScrapText != null) view.ScrapText.text = model.ScrapStatus;
            if (view.CostText != null) view.CostText.text = model.CostStatus;
            if (view.FishText != null) view.FishText.text = model.FishStatus;
            if (view.CookedFishText != null) view.CookedFishText.text = model.CookedFishStatus;
            if (view.NeedsText != null) view.NeedsText.text = model.NeedsStatus;
            if (view.GrillText != null) view.GrillText.text = $"已建烤架  {model.Grills}";
            if (view.GrillButton != null) view.GrillButton.image.color = model.Tool == BuildTool.GrillBlueprint ? new Color(0.12f, 0.50f, 0.44f) : new Color(0.22f, 0.34f, 0.32f);
            if (view.TargetMinusButton != null) view.TargetMinusButton.interactable = production != null && model.FoodTarget > 0;
            if (view.TargetPlusButton != null) view.TargetPlusButton.interactable = production != null && model.FoodTarget < 20;
            if (view.FishingButton != null)
            {
                view.FishingButton.interactable = fishing != null;
                view.FishingButton.image.color = model.FishingEnabled ? new Color(0.12f, 0.50f, 0.44f) : new Color(0.22f, 0.34f, 0.32f);
            }
            if (view.FishingButtonText != null) view.FishingButtonText.text = model.FishingEnabled ? "空闲钓鱼：开  F" : "空闲钓鱼：关  F";
            view.ModeText.text = model.Tool == BuildTool.None ? "自由规划" :
                model.Tool == BuildTool.DeckBlueprint ? "扩建甲板" : model.Tool == BuildTool.GrillBlueprint ? "放置烤架  R 旋转" : "撤销蓝图";
            view.BuildButton.image.color = model.Tool == BuildTool.DeckBlueprint ? new Color(0.12f, 0.50f, 0.44f) : new Color(0.22f, 0.34f, 0.32f);
            view.CancelButton.image.color = model.Tool == BuildTool.CancelBlueprint ? new Color(0.66f, 0.37f, 0.22f) : new Color(0.22f, 0.34f, 0.32f);
        }

        protected override void OnClose()
        {
            view.BuildButton.onClick.RemoveListener(ToggleBuild);
            view.CancelButton.onClick.RemoveListener(ToggleCancel);
            if (view.FishingButton != null) view.FishingButton.onClick.RemoveListener(ToggleFishing);
            if (view.GrillButton != null) view.GrillButton.onClick.RemoveListener(ToggleGrill);
            if (view.TargetMinusButton != null) view.TargetMinusButton.onClick.RemoveListener(LowerTarget);
            if (view.TargetPlusButton != null) view.TargetPlusButton.onClick.RemoveListener(RaiseTarget);
            manager.Changed -= Refresh;
            if (crew != null) crew.Changed -= Refresh;
            if (resources != null) resources.Changed -= Refresh;
            if (salvage != null) salvage.Changed -= Refresh;
            if (fishing != null) fishing.Changed -= Refresh;
            if (production != null) production.Changed -= Refresh;
            if (needs != null) needs.Changed -= Refresh;
            model.Changed -= Render;
        }
    }
}
