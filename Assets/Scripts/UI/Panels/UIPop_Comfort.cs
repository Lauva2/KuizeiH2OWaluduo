using System;
using UnityEngine;
using UnityEngine.UI;
namespace RaftGame.UI
{
    [Serializable] public sealed class ComfortRowView
    { public Text Name, Contribution, Next; }
    public sealed class UIPop_Comfort : UIView
    {
        public const string Address = nameof(UIPop_Comfort);
        public RectTransform Window;
        public Text ScoreText, HintText;
        public Button CloseButton;
        public ComfortRowView[] Rows;
        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (Window == null) return;
            var size = ((RectTransform)transform).rect.size;
            Window.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((size.x - 48) / 800, (size.y - 48) / 470), .2f, 1);
        }
    }
}
