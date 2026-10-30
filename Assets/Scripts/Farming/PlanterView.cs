using UnityEngine;
namespace RaftGame.Farming
{
    public sealed class PlanterView : MonoBehaviour
    {
        public Transform Plants;
        public Transform[] Sprouts;
        public GameObject Potatoes;
        public Renderer Soil;
        private MaterialPropertyBlock tint;
        public void Present(CropStage stage, float progress)
        {
            if (Plants != null)
            {
                Plants.gameObject.SetActive(stage != CropStage.Empty);
                float scale = stage == CropStage.Planted ? .28f : stage == CropStage.Mature ? 1 : Mathf.Lerp(.28f, 1, Mathf.Clamp01(progress));
                if (Sprouts != null) foreach (var sprout in Sprouts) if (sprout != null) sprout.localScale = Vector3.one * scale;
            }
            if (Potatoes != null) Potatoes.SetActive(stage == CropStage.Mature);
            if (Soil != null)
            {
                if (tint == null) tint = new MaterialPropertyBlock();
                tint.SetColor("_Color", stage == CropStage.Planted || stage == CropStage.Empty ? new Color(.42f, .30f, .18f) : new Color(.25f, .22f, .13f)); Soil.SetPropertyBlock(tint);
            }
        }
    }
}
