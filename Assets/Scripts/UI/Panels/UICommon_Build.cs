using UnityEngine.UI;

namespace RaftGame.UI
{
    public sealed class UICommon_Build : UIView
    {
        public const string Address = nameof(UICommon_Build);
        public Button BuildButton;
        public Button CancelButton;
        public Text CompletedText;
        public Text BlueprintText;
        public Text StatusText;
        public Text ModeText;
        public Text CrewText;
        public Text WoodText;
        public Text ScrapText;
        public Text CostText;
        public Text FishText;
        public Button FishingButton;
        public Text FishingButtonText;
        public Button GrillButton;
        public Text GrillText;
        public Text CookedFishText;
        public Text NeedsText;
        public Button TargetMinusButton;
        public Button TargetPlusButton;
    }
}
