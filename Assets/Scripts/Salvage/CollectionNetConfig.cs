using UnityEngine;
namespace RaftGame.Salvage
{
    [CreateAssetMenu(menuName = "Raft Game/Collection Net Config")]
    public sealed class CollectionNetConfig : ScriptableObject
    {
        [Min(.1f)] public float CollectionDuration = 1.2f;
        [Range(.1f, .5f)] public float CaptureHalfWidthInTiles = .45f;
    }
}
