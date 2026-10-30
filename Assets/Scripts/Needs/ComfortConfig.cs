using UnityEngine;
namespace RaftGame.Needs
{
    [CreateAssetMenu(menuName = "Raft Game/Comfort Config")]
    public sealed class ComfortConfig : ScriptableObject
    {
        [Min(1)] public float FirstContribution = 20;
        [Range(0, .95f)] public float RepeatFactor = .5f;
        [Range(0, 1)] public float MaximumRestBonus = .25f;
        [Min(0)] public float LampIntensity = 1.1f;
    }
}
