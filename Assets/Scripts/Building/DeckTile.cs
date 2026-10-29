using UnityEngine;

namespace RaftGame.Building
{
    /// <summary>Authored tile coordinate. BuildManager owns occupancy and construction state.</summary>
    public sealed class DeckTile : MonoBehaviour
    {
        public Vector2Int Coordinate;
    }
}
