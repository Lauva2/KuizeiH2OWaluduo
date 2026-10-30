using UnityEngine;

namespace RaftGame.Building
{
    public sealed class GrillView : MonoBehaviour
    {
        [SerializeField] private GameObject food;
        [SerializeField] private Light fireLight;
        [SerializeField] private Renderer fish;
        private MaterialPropertyBlock tint;
        public GameObject PotatoFood;
        public Renderer Potato;

        public void Present(bool cooking, float progress, RaftGame.Resources.ResourceType output = RaftGame.Resources.ResourceType.CookedFish)
        {
            bool potato = output == RaftGame.Resources.ResourceType.BakedPotato;
            if (food != null) food.SetActive(cooking && !potato);
            if (PotatoFood != null) PotatoFood.SetActive(cooking && potato);
            if (fireLight != null) { fireLight.enabled = cooking; fireLight.intensity = 1.4f; }
            if (fish == null) return;
            if (tint == null) tint = new MaterialPropertyBlock();
            tint.SetColor("_Color", Color.Lerp(new Color(0.46f, 0.74f, 0.73f), new Color(0.97f, 0.61f, 0.23f), Mathf.Clamp01(progress)));
            fish.SetPropertyBlock(tint);
            if (Potato != null)
            {
                tint.SetColor("_Color", Color.Lerp(new Color(.73f, .57f, .32f), new Color(.94f, .67f, .30f), Mathf.Clamp01(progress))); Potato.SetPropertyBlock(tint);
            }
        }
    }
}
