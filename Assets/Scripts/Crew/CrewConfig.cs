using UnityEngine;

namespace RaftGame.Crew
{
    [CreateAssetMenu(menuName = "Raft Game/Crew Config")]
    public sealed class CrewConfig : ScriptableObject
    {
        public GameObject CrewPrefab;
        public string FirstName = "阿航";
        public string RecruitName = "小澜";
        public Vector2Int SpawnCell = Vector2Int.zero;
        [Min(0.1f)] public float WalkSpeed = 1.8f;
        [Min(0.1f)] public float ConstructionDuration = 4.5f;
        [Min(1f)] public float TurnSpeed = 540f;
        [Min(0f)] public float FootOffset = 0.08f;
        [Min(0.1f)] public float RetryInterval = 0.5f;
    }
}
