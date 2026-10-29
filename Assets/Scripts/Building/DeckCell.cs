using UnityEngine;

namespace RaftGame.Building
{
    public enum BuildTool { None, DeckBlueprint, CancelBlueprint, GrillBlueprint }
    public enum DeckState { Planned, Constructing, Complete }

    public sealed class DeckCell
    {
        public Vector2Int Coordinate { get; }
        public DeckState State { get; internal set; }
        public float ConstructionProgress { get; internal set; }
        public bool IsBlocked { get; internal set; }
        internal GameObject Visual;

        internal DeckCell(Vector2Int coordinate, DeckState state, GameObject visual)
        {
            Coordinate = coordinate;
            State = state;
            Visual = visual;
        }
    }
}
