using UnityEngine;
namespace RaftGame.Resources
{
    [CreateAssetMenu(menuName = "Raft Game/Loot Config")]
    public sealed class LootConfig : ScriptableObject
    {
        [Header("Fishing weights: fish / bottled water / wood / scrap")]
        [Min(0)] public int FishWeight = 55;
        [Min(0)] public int WaterWeight = 25;
        [Min(0)] public int WoodWeight = 12;
        [Min(0)] public int ScrapWeight = 8;
        [Min(1)] public int WaterCatchGuarantee = 6;
        [Range(0, 1)] public float PurifierBlueprintChance = .12f;
        [Min(2)] public int BlueprintCrateGuarantee = 6;
    }
}
