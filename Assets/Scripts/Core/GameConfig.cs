using UnityEngine;
using RaftGame.Ocean;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;

namespace RaftGame.Core
{
    [CreateAssetMenu(menuName = "Raft Game/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public OceanConfig Ocean;
        public BuildConfig Building;
        public CrewConfig Crew;
        public ResourceConfig Resources;
        public SalvageConfig Salvage;
        public FishingConfig Fishing;
        public ProductionConfig Production;
        public NeedsConfig Needs;
        public WaterConfig Water;
        public LootConfig Loot;
        public DayNightConfig DayNight;
        public OceanLifeConfig OceanLife;
        public CollectionNetConfig CollectionNets;
        public RescueConfig Rescue;
        public RestConfig Rest;
        public ComfortConfig Comfort;
        public RaftGame.Farming.FarmingConfig Farming;

        [Header("Orthographic camera")]
        [Range(30f, 80f)] public float CameraTilt = 52f;
        public float CameraYaw = 35f;
        [Min(1f)] public float CameraSize = 14f;
        [Min(1f)] public float MinimumCameraSize = 6f;
        [Min(1f)] public float MaximumCameraSize = 32f;
        [Min(0f)] public float PanSpeed = 14f;
        [Min(0f)] public float RotateSpeed = 65f;
        [Min(0f)] public float ZoomSpeed = 2f;
    }
}
