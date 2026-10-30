using UnityEngine;

namespace RaftGame.Building
{
    public enum BuildTool { None, DeckBlueprint, CancelBlueprint, GrillBlueprint, PurifierBlueprint, CollectionNetBlueprint, BedBlueprint, PlantBlueprint, RugBlueprint, DecorLampBlueprint, PlanterBlueprint }
    public enum FacilityKind { Grill, Purifier, CollectionNet, Bed, Plant, Rug, DecorLamp, Planter }
    public enum DeckState { Planned, Constructing, Complete }

    public static class FacilityRules
    {
        public static bool IsDecoration(FacilityKind kind) => kind == FacilityKind.Plant || kind == FacilityKind.Rug || kind == FacilityKind.DecorLamp;
        public static BuildTool ToolFor(FacilityKind kind) => kind switch
        {
            FacilityKind.Grill => BuildTool.GrillBlueprint, FacilityKind.Purifier => BuildTool.PurifierBlueprint,
            FacilityKind.CollectionNet => BuildTool.CollectionNetBlueprint, FacilityKind.Bed => BuildTool.BedBlueprint,
            FacilityKind.Planter => BuildTool.PlanterBlueprint,
            FacilityKind.Plant => BuildTool.PlantBlueprint, FacilityKind.Rug => BuildTool.RugBlueprint, _ => BuildTool.DecorLampBlueprint
        };
        public static bool TryKind(BuildTool tool, out FacilityKind kind)
        {
            kind = tool switch { BuildTool.PurifierBlueprint => FacilityKind.Purifier, BuildTool.CollectionNetBlueprint => FacilityKind.CollectionNet,
                BuildTool.PlanterBlueprint => FacilityKind.Planter,
                BuildTool.BedBlueprint => FacilityKind.Bed, BuildTool.PlantBlueprint => FacilityKind.Plant, BuildTool.RugBlueprint => FacilityKind.Rug,
                BuildTool.DecorLampBlueprint => FacilityKind.DecorLamp, _ => FacilityKind.Grill };
            return tool == BuildTool.GrillBlueprint || tool == BuildTool.PurifierBlueprint || tool == BuildTool.CollectionNetBlueprint || tool == BuildTool.BedBlueprint ||
                tool == BuildTool.PlantBlueprint || tool == BuildTool.RugBlueprint || tool == BuildTool.DecorLampBlueprint || tool == BuildTool.PlanterBlueprint;
        }
        public static string Name(FacilityKind kind) => kind switch { FacilityKind.Plant => "海岛盆栽", FacilityKind.Rug => "编织地毯", FacilityKind.DecorLamp => "营地装饰灯",
            FacilityKind.Planter => "土豆种植箱", FacilityKind.Bed => "基础床铺", FacilityKind.CollectionNet => "漂流收集网", FacilityKind.Purifier => "海水净化器", _ => "简易烤架" };
    }

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
