using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Resources;
using RaftGame.Building;
using RaftGame.Salvage;
using RaftGame.Crew;
namespace RaftGame.Core
{
    [Serializable] public sealed class DeckSave
    { public int X, Z; public bool Complete; public float Progress; public Vector2Int Coordinate => new Vector2Int(X, Z); }
    [Serializable] public sealed class FacilitySave
    { public int X, Z, Rotation; public FacilityKind Kind; public bool Complete; public float Progress;
        public bool HasBuildCost; public ResourceCost BuildCost; public List<NetSupply> NetContents; public bool HasCrop; public CropSave Crop;
        public Vector2Int Coordinate => new Vector2Int(X, Z); }
    [Serializable] public sealed class FloatingSave
    { public ResourceType Type; public FloatingKind Kind; public Vector3 Position; public float Age, Phase, DriftMultiplier = 1, Windage; public bool Marked; }
    [Serializable] public sealed class SaveData
    {
        public const int CurrentVersion = 6;
        public int Version = CurrentVersion;
        public List<CrewSave> CrewMembers;
        public RescueSave Recruitment;
        public bool PurifierUnlocked;
        public LootState Loot;
        public int DriftRandomState, DriftSequence;
        public string SavedUtc;
        public ResourceCost Inventory;
        public List<DeckSave> Decks = new List<DeckSave>();
        public List<FacilitySave> Facilities = new List<FacilitySave>();
        public List<FloatingSave> Floating = new List<FloatingSave>();
        public int CrewX, CrewZ, FoodTarget, WaterTarget, Rods, Speed;
        public float Satiety, Hydration, OceanTime, CameraYaw, CameraSize, SpawnTimer;
        public Vector3 CameraFocus;
        public bool AutomaticFishing, Paused;
        public double ElapsedSeconds;
    }
    [Serializable] public sealed class CrewSave
    {
        public int Id, X, Z;
        public string Name;
        public WorkPreference Preference;
        public float Satiety, Hydration;
        public float Energy = 85;
        public bool RestRequested;
        public Vector2Int Coordinate => new Vector2Int(X, Z);
    }
    [Serializable] public struct CropSave
    {
        public RaftGame.Farming.CropStage Stage;
        public float Progress;
        public bool Repeat;
    }
    [Serializable] public sealed class RescueSave
    {
        public bool Active, Marked;
        public Vector3 Position;
        public float Age, Remaining = 45;
    }
}
