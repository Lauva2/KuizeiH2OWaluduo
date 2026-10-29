using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Salvage
{
    public enum FloatingKind { Wood, Scrap, Bottle, SupplyCrate }
    public sealed class FloatingResource : MonoBehaviour
    {
        [SerializeField] private GameObject marker;
        public FloatingKind Kind { get; internal set; }
        public ResourceType Type { get => Kind == FloatingKind.Wood ? ResourceType.Wood : Kind == FloatingKind.Bottle ? ResourceType.Water : ResourceType.Scrap;
            internal set => Kind = value == ResourceType.Wood ? FloatingKind.Wood : value == ResourceType.Water ? FloatingKind.Bottle : FloatingKind.Scrap; }
        public int Amount { get; internal set; }
        public int Id { get; internal set; }
        public bool IsMarked { get; internal set; }
        internal object ClaimOwner;
        internal bool Collecting;
        internal float Age;
        internal float Phase;
        internal float DriftMultiplier = 1f;
        internal float Windage;
        public void ShowMarker(bool visible) { if (marker != null) marker.SetActive(visible); }
    }
}
