using UnityEngine;

namespace RaftGame.Fishing
{
    [CreateAssetMenu(menuName = "Raft Game/Fishing Config")]
    public sealed class FishingConfig : ScriptableObject
    {
        [Range(0, 1)] public int StartingRods = 1;
        public bool AutomaticByDefault = true;
        [Min(0.1f)] public float CatchDuration = 6f;
        [Min(1)] public int FishPerCatch = 1;
        [Min(0.5f)] public float CastDistance = 2.3f;
    }
}
