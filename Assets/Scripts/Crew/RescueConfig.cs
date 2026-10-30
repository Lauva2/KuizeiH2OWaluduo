using UnityEngine;
namespace RaftGame.Crew
{
    [CreateAssetMenu(menuName = "Raft Game/Rescue Config")]
    public sealed class RescueConfig : ScriptableObject
    {
        public GameObject RaftPrefab;
        [Min(1)] public float FirstArrivalSeconds = 45;
        [Min(1)] public float RetrySeconds = 30;
        [Min(1)] public float UnmarkedLifetime = 180;
        [Min(.1f)] public float RescueDuration = 3;
        [Min(1)] public float Reach = 2.8f;
        [Min(0)] public float DriftSpeed = .08f;
    }
}
