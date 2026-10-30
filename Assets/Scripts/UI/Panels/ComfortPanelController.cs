using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Needs;
namespace RaftGame.UI
{
    public sealed class ComfortPanelController : UIController
    {
        private readonly GameManager game;
        private readonly ComfortManager comfort;
        private UIPop_Comfort view;
        private ComfortPanelModel model;
        public ComfortPanelController(GameManager owner, ComfortManager campComfort) { game = owner; comfort = campComfort; }
        protected override void OnInitialize()
        {
            view = (UIPop_Comfort)View; model = (ComfortPanelModel)Model;
            view.CloseButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        protected override void OnOpen()
        { view.CloseButton.onClick.AddListener(game.CloseComfortPanel); comfort.Changed += Refresh; model.Changed += Render; Refresh(); }
        protected override void OnRefresh() => Refresh();
        private void Refresh() => model.Refresh(comfort);
        private void Render()
        {
            view.ScoreText.text = model.Score; view.HintText.text = model.Hint;
            for (int index = 0; index < 3; index++)
            { var row = view.Rows[index]; var data = model.Rows[index]; row.Name.text = data.Name; row.Contribution.text = data.Contribution; row.Next.text = data.Next; }
        }
        protected override void OnClose()
        { view.CloseButton.onClick.RemoveListener(game.CloseComfortPanel); comfort.Changed -= Refresh; model.Changed -= Render; game.NotifyComfortPanelClosed(); }
    }
}
