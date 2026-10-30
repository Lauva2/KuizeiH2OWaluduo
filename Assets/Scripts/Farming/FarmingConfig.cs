using UnityEngine;
namespace RaftGame.Farming
{
    public enum CropStage { Empty, Planted, Growing, Mature }
    public enum FarmAction { Plant, Water, Harvest }
    [CreateAssetMenu(menuName = "Raft Game/Farming Config")]
    public sealed class FarmingConfig : ScriptableObject
    {
        [Min(.1f)] public float PlantDuration = 2;
        [Min(.1f)] public float WaterDuration = 1.5f;
        [Min(.1f)] public float HarvestDuration = 2;
        [Min(1)] public float GrowthDuration = 60;
        [Min(2)] public int HarvestPotatoes = 3;
        [Min(1)] public int WaterPerCrop = 1;
        [Min(0)] public int SeedReserve = 1;
    }
}
