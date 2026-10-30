using System;
using UnityEngine;
using UnityEngine.UI;
namespace RaftGame.UI
{
    [Serializable] public sealed class CrewRowView
    {
        public Text Name, Needs, Energy, Activity, Location;
        public Button Automatic, Gathering, Building;
    }
    public sealed class UIPop_Crew : UIView
    {
        public const string Address = nameof(UIPop_Crew);
        public RectTransform Window;
        public Text Heading, Notice;
        public Button CloseButton;
        public CrewRowView[] Rows;
        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (Window == null) return;
            var size = ((RectTransform)transform).rect.size;
            float scale = Mathf.Clamp(Mathf.Min((size.x - 48) / 848, (size.y - 48) / 468), .2f, 1);
            Window.localScale = Vector3.one * scale;
        }
    }
}
