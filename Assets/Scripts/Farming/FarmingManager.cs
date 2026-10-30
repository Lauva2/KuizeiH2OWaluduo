using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Resources;
using RaftGame.Needs;
namespace RaftGame.Farming
{
    public sealed class FarmSession
    {
        public GrillCell Planter { get; internal set; }
        public FarmAction Action { get; internal set; }
        public float Progress { get; internal set; }
        internal object Owner;
        internal bool Begun, Ended;
    }
    public sealed class FarmingManager : MonoBehaviour
    {
        private FarmingConfig configuration;
        private BuildManager build;
        private ResourceManager resources;
        private NeedsManager needs;
        private readonly Dictionary<GrillCell, FarmSession> active = new Dictionary<GrillCell, FarmSession>();
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;
        public int SeedReserve
        {
            get
            {
                if (!IsInitialized) return 0;
                foreach (var cell in build.Grills.Values) if (cell.Kind == FacilityKind.Planter && cell.RepeatPlanting) return Mathf.Max(0, configuration.SeedReserve);
                return 0;
            }
        }
        public bool Initialize(FarmingConfig config, BuildManager building, ResourceManager inventory, NeedsManager crewNeeds = null)
        {
            if (IsInitialized) return true;
            if (config == null || config.GrowthDuration <= 0 || config.HarvestPotatoes < 2 || config.WaterPerCrop < 1 || building == null || !building.IsInitialized || inventory == null || !inventory.IsInitialized) return false;
            configuration = config; build = building; resources = inventory; needs = crewNeeds; IsInitialized = true;
            build.FacilityChanging += Interrupt; build.LayoutRestoring += CancelAll; return true;
        }
        public void Tick(float deltaTime)
        {
            if (!IsInitialized || deltaTime <= 0) return;
            bool changed = false;
            foreach (var cell in build.Grills.Values)
            {
                if (!Available(cell) || cell.CropStage != CropStage.Growing) continue;
                int before = Mathf.FloorToInt(cell.GrowthProgress * 100);
                cell.GrowthProgress = Mathf.Clamp01(cell.GrowthProgress + deltaTime / configuration.GrowthDuration);
                if (cell.GrowthProgress >= 1) cell.CropStage = CropStage.Mature;
                cell.CropView?.Present(cell.CropStage, cell.GrowthProgress);
                if (before != Mathf.FloorToInt(cell.GrowthProgress * 100)) changed = true;
            }
            if (changed) Notify();
        }
        private bool Available(GrillCell cell) => build.IsCurrentFacility(cell) && cell.Kind == FacilityKind.Planter && !cell.IsBeingMoved;
        private bool Ready(GrillCell cell, FarmAction action)
        {
            if (!Available(cell)) return false;
            if (action == FarmAction.Harvest) return cell.CropStage == CropStage.Mature && resources.CanGrant(ResourceCost.Single(ResourceType.Potato, configuration.HarvestPotatoes));
            if (action == FarmAction.Plant) return cell.CropStage == CropStage.Empty && cell.RepeatPlanting && resources.GetAvailable(ResourceType.Potato) > 0;
            return cell.CropStage == CropStage.Planted && (long)resources.GetAvailable(ResourceType.Water) - (needs != null ? needs.DrinkingDemand : 0) >= configuration.WaterPerCrop;
        }
        public bool TryFindTask(IReadOnlyDictionary<Vector2Int, int> distances, out GrillCell result, out FarmAction action, bool harvestOnly = false)
        {
            result = null; action = FarmAction.Harvest; if (!IsInitialized) return false;
            foreach (var candidateAction in new[] { FarmAction.Harvest, FarmAction.Water, FarmAction.Plant })
            {
                if (harvestOnly && candidateAction != FarmAction.Harvest) break;
                int best = int.MaxValue;
                foreach (var cell in build.Grills.Values)
                    if (!active.ContainsKey(cell) && Ready(cell, candidateAction) && distances.TryGetValue(cell.WorkCell, out int distance) &&
                        (distance < best || (distance == best && (result == null || cell.Coordinate.x < result.Coordinate.x || (cell.Coordinate.x == result.Coordinate.x && cell.Coordinate.y < result.Coordinate.y)))))
                    { result = cell; best = distance; }
                if (result != null) { action = candidateAction; return true; }
            }
            return false;
        }
        public FarmSession TryAcquire(object owner, GrillCell cell, FarmAction action)
        {
            if (!IsInitialized || owner == null || cell == null || !Enum.IsDefined(typeof(FarmAction), action) || active.ContainsKey(cell) || !Ready(cell, action)) return null;
            foreach (var entry in active.Values) if (ReferenceEquals(entry.Owner, owner)) return null;
            var session = new FarmSession { Owner = owner, Planter = cell, Action = action }; active.Add(cell, session); Notify(); return session;
        }
        private bool Owns(FarmSession session, object owner) => session != null && session.Planter != null && !session.Ended && ReferenceEquals(session.Owner, owner) && active.TryGetValue(session.Planter, out var current) && ReferenceEquals(current, session);
        public bool IsValid(FarmSession session, object owner) => IsInitialized && Owns(session, owner) && Available(session.Planter) &&
            session.Planter.CropStage == (session.Action == FarmAction.Plant ? CropStage.Empty : session.Action == FarmAction.Water ? CropStage.Planted : CropStage.Mature) &&
            (session.Action != FarmAction.Plant || session.Planter.RepeatPlanting) &&
            (session.Action != FarmAction.Water || !session.Begun || resources.GetAvailable(ResourceType.Water) >= (needs != null ? needs.DrinkingDemand : 0));
        private ResourceCost Cost(FarmAction action) => action == FarmAction.Plant ? ResourceCost.Single(ResourceType.Potato) : action == FarmAction.Water ? ResourceCost.Single(ResourceType.Water, configuration.WaterPerCrop) : default;
        public bool Begin(FarmSession session, object owner, Vector2Int standing)
        {
            if (!IsValid(session, owner) || session.Begun || standing != session.Planter.WorkCell || !Ready(session.Planter, session.Action)) return false;
            if (session.Action != FarmAction.Harvest && !resources.TryReserve(session, Cost(session.Action))) return false;
            session.Begun = true; Notify(); return true;
        }
        public bool Advance(FarmSession session, object owner, Vector2Int standing, float deltaTime)
        {
            if (!IsValid(session, owner) || !session.Begun || standing != session.Planter.WorkCell) return false;
            float duration = session.Action == FarmAction.Plant ? configuration.PlantDuration : session.Action == FarmAction.Water ? configuration.WaterDuration : configuration.HarvestDuration;
            session.Progress = Mathf.Clamp01(session.Progress + Mathf.Max(0, deltaTime) / Mathf.Max(.1f, duration));
            if (session.Progress < 1) return true;
            Action finish = () =>
            {
                var cell = session.Planter; session.Ended = true; active.Remove(cell);
                cell.CropStage = session.Action == FarmAction.Plant ? CropStage.Planted : session.Action == FarmAction.Water ? CropStage.Growing : CropStage.Empty;
                cell.GrowthProgress = 0; cell.CropView?.Present(cell.CropStage, 0); Notify();
            };
            return session.Action == FarmAction.Harvest ? resources.TryGrant(ResourceCost.Single(ResourceType.Potato, configuration.HarvestPotatoes), finish) : resources.TryConvert(session, default, finish);
        }
        public void SetRepeat(GrillCell cell, bool repeat)
        {
            if (!IsInitialized || !build.IsCurrentFacility(cell) || cell.Kind != FacilityKind.Planter || cell.RepeatPlanting == repeat) return;
            if (!repeat && active.TryGetValue(cell, out var session) && session.Action == FarmAction.Plant) Cancel(session, session.Owner);
            cell.RepeatPlanting = repeat; Notify();
        }
        public string Status(GrillCell cell)
        {
            if (cell.IsBeingMoved) return "搬移中 · 保留本轮作物，暂停生长";
            if (cell.CropStage == CropStage.Mature) return $"已成熟 · 等待收获 {configuration.HarvestPotatoes} 份土豆";
            if (cell.CropStage == CropStage.Growing) return $"生长 {Mathf.FloorToInt(cell.GrowthProgress * 100)}% · 剩余约 {Mathf.CeilToInt((1 - cell.GrowthProgress) * configuration.GrowthDuration)} 秒";
            if (cell.CropStage == CropStage.Planted) return "等待浇水 · 先保留船员饮用的淡水";
            return cell.RepeatPlanting ? resources.GetAvailable(ResourceType.Potato) > 0 ? "等待播种 · 土豆可用作种薯" : "等待种薯 · 补给箱可找到土豆" : "本轮结束 · 连续种植已关闭";
        }
        public void Cancel(FarmSession session, object owner)
        {
            if (!Owns(session, owner)) return;
            session.Ended = true; active.Remove(session.Planter); resources.Release(session); Notify();
        }
        private void Interrupt(GrillCell cell) { if (active.TryGetValue(cell, out var session)) Cancel(session, session.Owner); }
        private void CancelAll() { foreach (var session in new List<FarmSession>(active.Values)) Cancel(session, session.Owner); }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            CancelAll(); if (build != null) { build.FacilityChanging -= Interrupt; build.LayoutRestoring -= CancelAll; }
            IsInitialized = false; Changed = null;
        }
    }
}
