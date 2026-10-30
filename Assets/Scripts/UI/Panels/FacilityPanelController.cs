using UnityEngine.UI;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Production;
using RaftGame.Salvage;

namespace RaftGame.UI
{
    public sealed class FacilityPanelController : UIController
    {
        private readonly FacilityManager facilities;
        private readonly CrewManager crew;
        private readonly ProductionManager food;
        private readonly WaterManager water;
        private readonly CollectionNetManager nets;
        private readonly RaftGame.Needs.ComfortManager comfort;
        private readonly RaftGame.Farming.FarmingManager farming;
        private UICommon_Facility view;
        private FacilityPanelModel model;
        public FacilityPanelController(FacilityManager manager, CrewManager workers, ProductionManager cooking, WaterManager purifying, CollectionNetManager collecting = null, RaftGame.Needs.ComfortManager campComfort = null, RaftGame.Farming.FarmingManager crops = null)
        { facilities = manager; crew = workers; food = cooking; water = purifying; nets = collecting; comfort = campComfort; farming = crops; }
        protected override void OnInitialize()
        {
            view = (UICommon_Facility)View; model = (FacilityPanelModel)Model;
            foreach (Button button in view.GetComponentsInChildren<Button>(true)) button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        protected override void OnOpen()
        {
            view.CloseButton.onClick.AddListener(Close); view.MoveButton.onClick.AddListener(Move);
            view.DemolishButton.onClick.AddListener(facilities.RequestDemolition); view.ConfirmButton.onClick.AddListener(Demolish);
            view.BackButton.onClick.AddListener(facilities.CancelDemolition);
            view.TargetMinusButton.onClick.AddListener(Lower); view.TargetPlusButton.onClick.AddListener(Raise);
            facilities.Changed += Refresh; crew.Changed += Refresh; food.Changed += Refresh; water.Changed += Refresh;
            if (nets != null) nets.Changed += Refresh;
            if (comfort != null) comfort.Changed += Refresh;
            if (farming != null) farming.Changed += Refresh;
            model.Changed += Render; Refresh();
        }
        protected override void OnRefresh() => Refresh();
        private void Refresh() => model.Refresh(facilities, crew, food, water, comfort, farming);
        private void Close() => facilities.ClearSelection();
        private void Move() { if (facilities.IsMoving) facilities.CancelMove(); else facilities.BeginMove(); }
        private void Demolish() => facilities.ConfirmDemolition();
        private void Lower() => SetTarget(-1);
        private void Raise() => SetTarget(1);
        private void SetTarget(int offset)
        {
            if (facilities.Selected == null) return;
            if (facilities.Selected.Kind == FacilityKind.Planter) { if (offset > 0) farming?.SetRepeat(facilities.Selected, !facilities.Selected.RepeatPlanting); return; }
            if (facilities.Selected.IsDecoration || facilities.Selected.Kind == FacilityKind.CollectionNet || facilities.Selected.Kind == FacilityKind.Bed) return;
            if (facilities.Selected.Kind == FacilityKind.Grill) food.SetTarget(food.Target + offset); else water.SetTarget(water.Target + offset);
        }
        private void Render()
        {
            view.TitleText.text = model.Title; view.LocationText.text = model.Location; view.WorkText.text = model.Work;
            view.TargetText.text = model.IsPlanter ? "连续种植 · " + (model.Repeat ? "开" : "关") : model.IsDecoration ? "营地装饰 · 可通行" : model.IsBed ? "单人床位 · 自动休息" : model.IsNet ? model.Storage : $"储备目标  {model.Target}"; view.RefundText.text = model.Refund; view.MessageText.text = model.Message;
            view.Icon.Tool = model.IconTool; view.Icon.SetVerticesDirty();
            view.MoveText.text = model.Moving ? "取消搬移" : "免费搬移";
            view.DemolishButton.interactable = model.CanDemolish;
            view.TargetMinusButton.gameObject.SetActive(!model.IsNet && !model.IsBed && !model.IsDecoration && !model.IsPlanter); view.TargetPlusButton.gameObject.SetActive(!model.IsNet && !model.IsBed && !model.IsDecoration);
            view.TargetPlusButton.GetComponentInChildren<Text>(true).text = model.IsPlanter ? model.Repeat ? "停" : "种" : "+";
            view.ActionRoot.SetActive(!model.Confirming); view.ConfirmRoot.SetActive(model.Confirming);
            view.TargetMinusButton.interactable = model.Target > 0 && !model.Confirming;
            view.TargetPlusButton.interactable = (model.IsPlanter ? !model.Moving : model.Target < 20) && !model.Confirming;
        }
        protected override void OnClose()
        {
            view.CloseButton.onClick.RemoveListener(Close); view.MoveButton.onClick.RemoveListener(Move);
            view.DemolishButton.onClick.RemoveListener(facilities.RequestDemolition); view.ConfirmButton.onClick.RemoveListener(Demolish);
            view.BackButton.onClick.RemoveListener(facilities.CancelDemolition);
            view.TargetMinusButton.onClick.RemoveListener(Lower); view.TargetPlusButton.onClick.RemoveListener(Raise);
            facilities.Changed -= Refresh; crew.Changed -= Refresh; food.Changed -= Refresh; water.Changed -= Refresh; model.Changed -= Render;
            if (nets != null) nets.Changed -= Refresh;
            if (comfort != null) comfort.Changed -= Refresh;
            if (farming != null) farming.Changed -= Refresh;
        }
    }
}
