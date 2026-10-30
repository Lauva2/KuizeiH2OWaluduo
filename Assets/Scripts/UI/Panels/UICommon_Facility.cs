using UnityEngine;
using UnityEngine.UI;

namespace RaftGame.UI
{
    public sealed class UICommon_Facility : UIView
    {
        public const string Address = nameof(UICommon_Facility);
        public RectTransform Card;
        public Text TitleText, LocationText, WorkText, TargetText, RefundText, MessageText, MoveText;
        public BlueprintIcon Icon;
        public Button CloseButton, MoveButton, DemolishButton, ConfirmButton, BackButton, TargetMinusButton, TargetPlusButton;
        public GameObject ActionRoot, ConfirmRoot;
        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (Card == null) return;
            var position = new Vector2(-24, ((RectTransform)transform).rect.width < 1560 ? 40 : 0);
            if (Card.anchoredPosition != position) Card.anchoredPosition = position;
        }
    }
}
