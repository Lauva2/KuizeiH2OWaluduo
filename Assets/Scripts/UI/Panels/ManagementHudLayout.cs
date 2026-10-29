using UnityEngine;

namespace RaftGame.UI
{
    /// <summary>Keeps the header's authored spacing intact on narrower PC aspect ratios.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ManagementHudLayout : MonoBehaviour
    {
        public RectTransform Header;
        public RectTransform CameraHints;
        public RectTransform TimeControls;
        public RectTransform CatalogWindow;
        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (Header == null) return;
            float width = ((RectTransform)transform).rect.width;
            float scale = Mathf.Clamp((width - 48f) / 1552f, 0.1f, 1f);
            Header.localScale = new Vector3(scale, scale, 1f);
            if (CameraHints != null) CameraHints.anchoredPosition = new Vector2(24, width < 1560f ? 180 : 24);
            if (TimeControls != null) TimeControls.anchoredPosition = new Vector2(-24, width < 1560f ? 180 : 24);
            if (CatalogWindow != null)
            {
                float fit = Mathf.Clamp(Mathf.Min((width - 48) / 920, (((RectTransform)transform).rect.height - 160) / 452), .2f, 1);
                CatalogWindow.localScale = Vector3.one * fit;
            }
        }
    }
}
