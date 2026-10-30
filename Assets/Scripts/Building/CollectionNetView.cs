using UnityEngine;
using System.Collections.Generic;
using RaftGame.Salvage;
namespace RaftGame.Building
{
    public sealed class CollectionNetView : MonoBehaviour
    {
        public GameObject[] Wood, Scrap, Bottles, Crates;
        public Renderer Indicator;
        private MaterialPropertyBlock tint;
        public void Present(IReadOnlyList<NetSupply> contents, bool moving)
        {
            for (int slot = 0; slot < CollectionNetManager.Capacity; slot++)
            {
                bool present = slot < contents.Count;
                Set(Wood, slot, present && contents[slot].Kind == FloatingKind.Wood);
                Set(Scrap, slot, present && contents[slot].Kind == FloatingKind.Scrap);
                Set(Bottles, slot, present && contents[slot].Kind == FloatingKind.Bottle);
                Set(Crates, slot, present && contents[slot].Kind == FloatingKind.SupplyCrate);
            }
            if (Indicator == null) return;
            if (tint == null) tint = new MaterialPropertyBlock();
            tint.SetColor("_Color", moving ? new Color(.43f, .62f, .66f) : contents.Count >= CollectionNetManager.Capacity ? new Color(.97f, .66f, .23f) : new Color(.23f, .70f, .52f));
            Indicator.SetPropertyBlock(tint);
        }
        private static void Set(GameObject[] array, int index, bool visible)
        { if (array != null && index < array.Length && array[index] != null) array[index].SetActive(visible); }
    }
}
