using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RaftGame.Resources;
namespace RaftGame.UI
{
    public sealed class ResourceTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ResourceType Resource;
        public RectTransform AvailableFill;
        public RectTransform ReservedFill;
        public Text TooltipText;
        public RectTransform TooltipPanel;
        public string Description;
        private static ResourceTile hovered;
        public void Present(ResourceManager resources, int target)
        {
            int total = resources.GetTotal(Resource), reserved = resources.GetReserved(Resource);
            float denominator = target >= 0 ? Mathf.Max(1, target) : Mathf.Max(1, total);
            AvailableFill.anchorMax = new Vector2(Mathf.Clamp01((total - reserved) / denominator), 1);
            float occupied = Mathf.Clamp01(total / denominator), held = Mathf.Clamp01(reserved / denominator);
            ReservedFill.anchorMin = new Vector2(Mathf.Max(0, occupied - held), 0); ReservedFill.anchorMax = new Vector2(occupied, 1);
            string[] names = { "木材", "废料", "鲜鱼", "熟鱼", "淡水" };
            Description = $"{names[(int)Resource]}  {total}\n可用 {total - reserved} · 预留 {reserved}" + (target >= 0 ? $"\n储备目标 {target} · 短条显示目标进度" : "\n短条显示可用与预留占比");
            if (hovered == this) TooltipText.text = Description;
        }
        public void OnPointerEnter(PointerEventData data)
        {
            if (TooltipPanel == null || TooltipText == null) return;
            hovered = this; TooltipText.text = Description; TooltipPanel.gameObject.SetActive(true);
            var root = (RectTransform)TooltipPanel.parent;
            var canvas = GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, data.position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var position);
            float top = position.y - 14;
            var layout = GetComponentInParent<ManagementHudLayout>();
            if (layout != null && layout.Header != null)
            {
                var corners = new Vector3[4]; layout.Header.GetWorldCorners(corners);
                top = root.InverseTransformPoint(corners[0]).y - 8;
            }
            TooltipPanel.localPosition = new Vector3(Mathf.Clamp(position.x + 16, root.rect.xMin, root.rect.xMax - TooltipPanel.rect.width), Mathf.Clamp(top, root.rect.yMin + TooltipPanel.rect.height, root.rect.yMax), 0);
        }
        public void OnPointerExit(PointerEventData data) => Hide();
        private void OnDisable() => Hide();
        private void Hide() { if (hovered != this) return; hovered = null; if (TooltipPanel != null) TooltipPanel.gameObject.SetActive(false); }
    }
}
