using UnityEngine;
using RaftGame.Resources;
namespace RaftGame.Production
{
    [CreateAssetMenu(menuName = "Raft Game/Water Config")]
    public sealed class WaterConfig : ScriptableObject
    {
        [Range(0, 20)] public int WaterTarget = 4;
        public ResourceCost Recipe = new ResourceCost(1, 0);
        [Min(0.1f)] public float PurifyingDuration = 8f;
        [Range(1, 20)] public int WaterPerBatch = 2;
    }
}
