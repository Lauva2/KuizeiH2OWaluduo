using UnityEngine;
using RaftGame.Resources;
using System.Collections.Generic;
using RaftGame.Salvage;

namespace RaftGame.Building
{
    public sealed class GrillCell
    {
        private static readonly Vector2Int[] Facing = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public Vector2Int Coordinate { get; internal set; }
        public int Rotation { get; internal set; }
        public FacilityKind Kind { get; }
        public bool IsBeingMoved { get; internal set; }
        public ResourceCost BuildCost { get; internal set; }
        public bool IsDecoration => FacilityRules.IsDecoration(Kind);
        public Vector2Int WorkCell => WorkCoordinate(Kind, Coordinate, Rotation);
        public Vector2Int MouthCell => Coordinate - Facing[Rotation];
        internal readonly List<NetSupply> NetItems = new List<NetSupply>();
        public IReadOnlyList<NetSupply> Contents => NetItems;
        public CollectionNetView NetView { get; internal set; }
        public DeckState State { get; internal set; }
        public float ConstructionProgress { get; internal set; }
        public GrillView View { get; internal set; }
        public PurifierView WaterView { get; internal set; }
        public BedView BedView { get; internal set; }
        public DecorationView DecorationView { get; internal set; }
        public RaftGame.Farming.PlanterView CropView { get; internal set; }
        public RaftGame.Farming.CropStage CropStage { get; internal set; }
        public float GrowthProgress { get; internal set; }
        public bool RepeatPlanting { get; internal set; } = true;
        internal GameObject Visual;

        internal GrillCell(Vector2Int coordinate, int rotation, GameObject visual, FacilityKind kind = FacilityKind.Grill)
        { Coordinate = coordinate; Rotation = ((rotation % 4) + 4) % 4; Visual = visual; Kind = kind; State = DeckState.Planned; }

        public static Vector2Int WorkDirection(int rotation) => Facing[((rotation % 4) + 4) % 4];
        public static Vector2Int WorkCoordinate(FacilityKind kind, Vector2Int cell, int rotation) => FacilityRules.IsDecoration(kind) ? cell : cell + WorkDirection(rotation);
    }
}
