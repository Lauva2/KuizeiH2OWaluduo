using UnityEngine;

namespace RaftGame.Resources
{
    [CreateAssetMenu(menuName = "Raft Game/Resource Config")]
    public sealed class ResourceConfig : ScriptableObject
    {
        public ResourceCost StartingInventory = new ResourceCost(8, 2);
    }
}
