using UnityEngine;

namespace RaftGame.Salvage
{
    [CreateAssetMenu(menuName = "Raft Game/Salvage Config")]
    public sealed class SalvageConfig : ScriptableObject
    {
        public GameObject WoodPrefab;
        public GameObject ScrapPrefab;
        public Vector2 CurrentDirection = new Vector2(0, -1);
        [Min(0f)] public float DriftSpeed = 0.35f;
        [Min(1f)] public float SpawnInterval = 5f;
        [Min(0f)] public float SpawnAheadDistance = 7f;
        [Min(0.2f)] public float LaneGap = 1.1f;
        [Min(0.5f)] public float Reach = 2.8f;
        [Min(0.1f)] public float GatherDuration = 1.2f;
        [Min(5f)] public float Lifetime = 85f;
        [Range(4, 50)] public int MaximumItems = 18;
        [Min(1)] public int WoodPerItem = 2;
        [Min(1)] public int ScrapPerItem = 1;
    }
}
