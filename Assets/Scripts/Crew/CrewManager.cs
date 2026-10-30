using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.Salvage;
using RaftGame.Fishing;
using RaftGame.Production;
using RaftGame.Needs;

namespace RaftGame.Crew
{
    public enum WorkPreference { Automatic, GatheringFirst, BuildingProductionFirst }
    public sealed class CrewManager : MonoBehaviour
    {
        public const int Capacity = 2;
        private readonly List<CrewWorker> members = new List<CrewWorker>();
        private readonly Dictionary<object, CrewWorker> tasks = new Dictionary<object, CrewWorker>();
        private readonly Dictionary<Vector2Int, CrewWorker> stations = new Dictionary<Vector2Int, CrewWorker>();
        private CrewConfig configuration;
        private BuildManager building;
        private Camera sceneCamera;
        private SalvageManager salvage;
        private FishingManager fishing;
        private ProductionManager production;
        private NeedsManager needs;
        private WaterManager water;
        private CollectionNetManager nets;
        public RescueManager Recruitment { get; private set; }
        public RestManager Rest { get; private set; }
        public RaftGame.Farming.FarmingManager Farming { get; private set; }
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public IReadOnlyList<CrewWorker> Members => members;
        public int Count => members.Count;
        public CrewWorker Primary => members.Count > 0 ? members[0] : null;
        public CrewActivity Activity => Primary != null ? Primary.Activity : CrewActivity.Idle;
        public Vector2Int CurrentCell => Primary != null ? Primary.CurrentCell : Vector2Int.zero;
        public Vector2Int? TaskCell => Primary?.TaskCell;
        public float WorkProgress => Primary != null ? Primary.WorkProgress : 0;
        public Transform Worker => Primary?.Worker;
        public string StatusText => Count < 2 ? Primary?.StatusText ?? "等待船员" : $"船员 {Count} 人 · C 查看分工";
        public string FirstName => configuration.FirstName;
        public event Action Changed;

        public bool Initialize(CrewConfig config, BuildManager build, Camera camera, SalvageManager salvageManager = null,
            FishingManager fishingManager = null, ProductionManager productionManager = null, NeedsManager needsManager = null,
            WaterManager waterManager = null, CollectionNetManager netManager = null, RaftGame.Farming.FarmingManager farmingManager = null)
        {
            if (IsInitialized) return true;
            if (config == null || config.CrewPrefab == null || build == null || !build.IsInitialized) return false;
            configuration = config; building = build; sceneCamera = camera; salvage = salvageManager; fishing = fishingManager;
            production = productionManager; needs = needsManager; water = waterManager; nets = netManager; Farming = farmingManager;
            if (!Add(1, config.FirstName, config.SpawnCell, WorkPreference.Automatic)) return false;
            building.IsCellOccupied = IsOccupied;
            IsInitialized = true; Notify(); return true;
        }
        private bool Add(int id, string name, Vector2Int cell, WorkPreference preference)
        {
            var member = new CrewWorker(id, name, preference);
            CrewNeeds personal = needs != null ? needs.AddMember(id) : null;
            if (!member.Initialize(this, configuration, building, sceneCamera, salvage, fishing, production, personal, water, nets)) return false;
            if (DeckPathfinder.IsWalkable(building.Cells, cell)) member.RestorePosition(cell);
            member.Changed += OnWorkerChanged; members.Add(member); return true;
        }
        public void AttachRecruitment(RescueManager rescue) { Recruitment = rescue; Notify(); }
        public void AttachRest(RestManager rest) { Rest = rest; Notify(); }
        public void Tick(float deltaTime) { if (IsInitialized) for (int index = 0; index < members.Count; index++) members[index].Tick(deltaTime); }
        public bool IsOccupied(Vector2Int cell)
        {
            foreach (var member in members)
                if (member.CurrentCell == cell || (member.Worker != null && building.WorldToCell(member.Worker.position) == cell)) return true;
            return false;
        }
        public bool CanRecruit(out Vector2Int landing)
        {
            landing = default;
            if (!IsInitialized || Count >= Capacity || needs == null || !needs.IsInitialized) return false;
            DeckPathfinder.FindReachable(building.Cells, CurrentCell, out _, out var distances);
            int best = int.MaxValue; bool found = false;
            foreach (var entry in distances)
                if (!IsOccupied(entry.Key) && !stations.ContainsKey(entry.Key) && (entry.Value < best || (entry.Value == best && Earlier(entry.Key, landing))))
                { landing = entry.Key; best = entry.Value; found = true; }
            return found;
        }
        public bool TryRecruit(out CrewWorker member, Action beforeNotify = null)
        {
            member = null;
            if (!CanRecruit(out var landing) || !Add(2, configuration.RecruitName, landing, WorkPreference.Automatic)) return false;
            member = members[members.Count - 1]; beforeNotify?.Invoke(); Notify(); return true;
        }
        public void SetPreference(int id, WorkPreference preference)
        {
            if (!Enum.IsDefined(typeof(WorkPreference), preference)) return;
            foreach (var member in members) if (member.Id == id) { member.SetPreference(preference); Notify(); return; }
        }
        internal bool CanClaim(CrewWorker member, object task, Vector2Int cell) =>
            (!tasks.TryGetValue(task, out var owner) || ReferenceEquals(owner, member)) &&
            (!stations.TryGetValue(cell, out var worker) || ReferenceEquals(worker, member));
        internal bool TryClaim(CrewWorker member, object task, Vector2Int cell)
        {
            if (!CanClaim(member, task, cell)) return false;
            tasks[task] = member; stations[cell] = member; Version++; return true;
        }
        internal bool StationAvailable(CrewWorker member, Vector2Int cell) => !stations.TryGetValue(cell, out var owner) || ReferenceEquals(owner, member);
        internal void ReleaseClaims(CrewWorker member)
        {
            var keys = new List<object>(); foreach (var entry in tasks) if (ReferenceEquals(entry.Value, member)) keys.Add(entry.Key);
            foreach (var key in keys) tasks.Remove(key);
            var cells = new List<Vector2Int>(); foreach (var entry in stations) if (ReferenceEquals(entry.Value, member)) cells.Add(entry.Key);
            foreach (var cell in cells) stations.Remove(cell);
            if (keys.Count + cells.Count > 0) Version++;
        }
        public string FacilityStatus(Vector2Int coordinate)
        { foreach (var member in members) if (member.TaskCell == coordinate) return member.DisplayName + "：" + member.StatusText.Replace("船员：", ""); return null; }
        public void CancelAssignments() { foreach (var member in members) member.CancelAssignments(); tasks.Clear(); stations.Clear(); Version++; }
        public void RestorePosition(Vector2Int coordinate) => Primary?.RestorePosition(coordinate);
        public List<CrewSave> CaptureMembers()
        {
            var result = new List<CrewSave>();
            foreach (var member in members) result.Add(new CrewSave { Id = member.Id, Name = member.DisplayName, Preference = member.Preference,
                X = member.CurrentCell.x, Z = member.CurrentCell.y, Satiety = member.Needs != null ? member.Needs.Satiety : 70, Hydration = member.Needs != null ? member.Needs.Hydration : 80, Energy = member.Needs != null ? member.Needs.Energy : 85, RestRequested = member.Needs != null && member.Needs.RestRequested });
            return result;
        }
        public void RestoreMembers(List<CrewSave> entries)
        {
            CancelAssignments();
            while (members.Count > entries.Count)
            { var last = members[members.Count - 1]; last.Changed -= OnWorkerChanged; last.Shutdown(); needs?.RemoveMember(last.Id); members.RemoveAt(members.Count - 1); }
            for (int index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (index >= members.Count) Add(entry.Id, entry.Name, entry.Coordinate, entry.Preference);
                var member = members[index]; member.RestoreIdentity(entry.Name, entry.Preference); member.RestorePosition(entry.Coordinate);
                member.Needs?.SetSatiety(entry.Satiety); member.Needs?.SetHydration(entry.Hydration);
                member.Needs?.SetEnergy(entry.Energy);
                member.Needs?.SetRestRequested(entry.RestRequested);
            }
            Notify();
        }
        private static bool Earlier(Vector2Int a, Vector2Int b) => a.x < b.x || (a.x == b.x && a.y < b.y);
        private void OnWorkerChanged() => Changed?.Invoke();
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            foreach (var member in members) { member.Changed -= OnWorkerChanged; member.Shutdown(); }
            members.Clear(); tasks.Clear(); stations.Clear(); if (building != null) building.IsCellOccupied = null;
            IsInitialized = false; Changed = null;
        }
    }
}
