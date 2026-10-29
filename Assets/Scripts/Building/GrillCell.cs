using UnityEngine;

namespace RaftGame.Building
{
    public sealed class GrillCell
    {
        private static readonly Vector2Int[] Facing = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public Vector2Int Coordinate { get; }
        public int Rotation { get; }
        public Vector2Int WorkCell => Coordinate + Facing[Rotation];
        public DeckState State { get; internal set; }
        public float ConstructionProgress { get; internal set; }
        public GrillView View { get; internal set; }
        internal GameObject Visual;

        internal GrillCell(Vector2Int coordinate, int rotation, GameObject visual)
        { Coordinate = coordinate; Rotation = ((rotation % 4) + 4) % 4; Visual = visual; State = DeckState.Planned; }

        public static Vector2Int WorkDirection(int rotation) => Facing[((rotation % 4) + 4) % 4];
    }
}
