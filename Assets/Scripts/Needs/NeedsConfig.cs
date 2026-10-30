using UnityEngine;

namespace RaftGame.Needs
{
    [CreateAssetMenu(menuName = "Raft Game/Needs Config")]
    public sealed class NeedsConfig : ScriptableObject
    {
        [Range(0, 100)] public float StartingSatiety = 70;
        [Min(0)] public float DrainPerSecond = 0.08f;
        [Range(0, 100)] public float EatBelow = 55;
        [Range(1, 100)] public float MealRestore = 35;
        [Min(0.1f)] public float EatingDuration = 2.5f;
        [Range(1, 100)] public float HungryBelow = 25;
        [Range(0.1f, 1f)] public float MinimumEfficiency = 0.65f;
        [Range(0, 100)] public float StartingHydration = 80;
        [Min(0)] public float ThirstDrainPerSecond = 0.07f;
        [Range(0, 100)] public float DrinkBelow = 55;
        [Range(1, 100)] public float DrinkRestore = 40;
        [Min(0.1f)] public float DrinkingDuration = 2f;
        [Range(1, 100)] public float ThirstyBelow = 25;
        [Range(0, 100)] public float StartingEnergy = 85;
        [Min(0)] public float WorkEnergyDrain = .12f;
        [Min(0)] public float IdleEnergyDrain = .02f;
        [Range(1, 100)] public float TiredBelow = 25;
    }
}
