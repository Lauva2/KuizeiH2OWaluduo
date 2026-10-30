using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Production
{
    [CreateAssetMenu(menuName = "Raft Game/Production Config")]
    public sealed class ProductionConfig : ScriptableObject
    {
        [Range(0, 20)] public int CookedFishTarget = 4;
        public ResourceCost Recipe = new ResourceCost(1, 0, 1);
        public ResourceCost PotatoRecipe = new ResourceCost(1, 0, 0, 0, 0, 1);
        [Min(0.1f)] public float CookingDuration = 6f;
    }
}
