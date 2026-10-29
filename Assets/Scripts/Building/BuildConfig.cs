using UnityEngine;
using RaftGame.Resources;

namespace RaftGame.Building
{
    [CreateAssetMenu(menuName = "Raft Game/Build Config")]
    public sealed class BuildConfig : ScriptableObject
    {
        [Min(0.5f)] public float TileSize = 2f;
        public float DeckHeight = 0.48f;
        [Min(2)] public int MaximumCoordinate = 64;
        public GameObject DeckPrefab;
        public GameObject BlueprintPrefab;
        public ResourceCost DeckCost = new ResourceCost(4, 1);
        public GameObject GrillPrefab;
        public GameObject GrillBlueprintPrefab;
        public ResourceCost GrillCost = new ResourceCost(4, 2);
        public Color ValidColor = new Color(0.15f, 0.91f, 0.70f, 0.38f);
        public Color InvalidColor = new Color(1f, 0.28f, 0.20f, 0.35f);
        public Color PlannedColor = new Color(0.24f, 0.74f, 0.95f, 0.28f);
    }
}
