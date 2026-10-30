using UnityEngine;
namespace RaftGame.Needs
{
    [CreateAssetMenu(menuName = "Raft Game/Rest Config")]
    public sealed class RestConfig : ScriptableObject
    {
        [Range(0, 99)] public float RestBelow = 30;
        [Range(1, 100)] public float WakeAt = 90;
        [Min(.1f)] public float RecoveryPerSecond = 3;
    }
}
