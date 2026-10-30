using UnityEngine;
using UnityEngine.UI;
using RaftGame.Core;
using RaftGame.Crew;
using UnityEngine.Events;
namespace RaftGame.UI
{
    public sealed class CrewPanelController : UIController
    {
        private readonly GameManager game;
        private readonly CrewManager crew;
        private readonly UnityAction[] actions = new UnityAction[6];
        private UIPop_Crew view;
        private CrewPanelModel model;
        public CrewPanelController(GameManager owner, CrewManager workers) { game = owner; crew = workers; }
        protected override void OnInitialize()
        {
            view = (UIPop_Crew)View; model = (CrewPanelModel)Model;
            foreach (var button in view.GetComponentsInChildren<Button>(true)) button.navigation = new Navigation { mode = Navigation.Mode.None };
            for (int row = 0; row < 2; row++) for (int preference = 0; preference < 3; preference++)
            { int id = row + 1; var value = (WorkPreference)preference; actions[row * 3 + preference] = () => crew.SetPreference(id, value); }
        }
        protected override void OnOpen()
        {
            view.CloseButton.onClick.AddListener(game.CloseCrewPanel);
            for (int row = 0; row < 2; row++)
            { view.Rows[row].Automatic.onClick.AddListener(actions[row * 3]); view.Rows[row].Gathering.onClick.AddListener(actions[row * 3 + 1]); view.Rows[row].Building.onClick.AddListener(actions[row * 3 + 2]); }
            crew.Changed += Refresh;
            if (game.Recruitment != null) game.Recruitment.Changed += Refresh;
            if (game.Comfort != null) game.Comfort.Changed += Refresh;
            game.Needs.Changed += Refresh; model.Changed += Render; Refresh();
        }
        protected override void OnRefresh() => Refresh();
        private void Refresh() => model.Refresh(crew, game.Recruitment);
        private void Render()
        {
            view.Heading.text = model.Heading; view.Notice.text = model.Notice;
            for (int index = 0; index < 2; index++)
            {
                var row = view.Rows[index]; var data = model.Rows[index];
                row.Name.text = data.Name; row.Needs.text = data.Needs; row.Activity.text = data.Activity; row.Location.text = data.Location;
                if (row.Energy != null) row.Energy.text = data.Energy;
                Button[] buttons = { row.Automatic, row.Gathering, row.Building };
                for (int button = 0; button < 3; button++)
                { buttons[button].interactable = data.Present; buttons[button].image.color = data.Present && (int)data.Preference == button ? new Color(.14f, .50f, .40f) : new Color(.43f, .49f, .42f); }
            }
        }
        protected override void OnClose()
        {
            view.CloseButton.onClick.RemoveListener(game.CloseCrewPanel);
            for (int row = 0; row < 2; row++)
            { view.Rows[row].Automatic.onClick.RemoveListener(actions[row * 3]); view.Rows[row].Gathering.onClick.RemoveListener(actions[row * 3 + 1]); view.Rows[row].Building.onClick.RemoveListener(actions[row * 3 + 2]); }
            crew.Changed -= Refresh; if (game.Recruitment != null) game.Recruitment.Changed -= Refresh;
            if (game.Comfort != null) game.Comfort.Changed -= Refresh;
            game.Needs.Changed -= Refresh; model.Changed -= Render; game.NotifyCrewPanelClosed();
        }
    }
}
