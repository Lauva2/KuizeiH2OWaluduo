using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Salvage
{
    public sealed class FloatingResource : MonoBehaviour
    {
        [SerializeField] private GameObject marker;
        public ResourceType Type { get; internal set; }
        public int Amount { get; internal set; }
        public int Id { get; internal set; }
        public bool IsMarked { get; internal set; }
        internal object ClaimOwner;
        internal bool Collecting;
        internal float Age;
        internal float Phase;
        public void ShowMarker(bool visible) { if (marker != null) marker.SetActive(visible); }
    }
}
