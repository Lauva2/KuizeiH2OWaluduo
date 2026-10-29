using UnityEngine;

namespace RaftGame.Ocean
{
    [CreateAssetMenu(menuName = "Raft Game/Ocean Life Config")]
    public sealed class OceanLifeConfig : ScriptableObject
    {
        public GameObject FishPrefab;
        public GameObject CoralFishPrefab;
        public GameObject BlueFishPrefab;
        [Range(1, 8)] public int Schools = 4;
        [Range(2, 8)] public int FishPerSchool = 4;
        public int Seed = 75931;
        [Min(4)] public float MinimumRadius = 5.5f;
        [Min(6)] public float MaximumRadius = 11.5f;
        [Range(.15f, 2)] public float SwimDepth = .55f;
        [Range(.1f, 2)] public float SwimSpeed = .7f;
        public Color SilverFish = new Color(.70f, .83f, .74f);
        public Color BlueFish = new Color(.43f, .76f, .81f);
        public Color CoralFish = new Color(.95f, .68f, .35f);
    }
}
