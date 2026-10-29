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
    }
}
