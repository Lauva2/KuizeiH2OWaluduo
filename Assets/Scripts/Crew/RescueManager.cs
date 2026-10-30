using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using RaftGame.Building;
using RaftGame.Core;
using RaftGame.Ocean;

namespace RaftGame.Crew
{
    public sealed class RescueSession
    {
        public Vector2Int Approach { get; internal set; }
        public float Progress { get; internal set; }
        internal object Owner;
        internal RescueRaftView Raft;
        internal bool Ended;
    }
    public sealed class RescueManager : MonoBehaviour
    {
        private RescueConfig configuration;
        private CrewManager crew;
        private BuildManager build;
        private OceanManager ocean;
        private Camera camera;
        private RescueRaftView raft;
        private RescueSession active;
        private float remaining, age;
        public bool IsInitialized { get; private set; }
        public bool HasRaft => raft != null;
        public bool Marked { get; private set; }
        public Vector3 Position => raft != null ? raft.transform.position : Vector3.zero;
        public int Version { get; private set; }
        public string Message { get; private set; } = "漂流途中会遇到救生筏，点击标记救援";
        public event Action Changed;
        public bool Initialize(RescueConfig config, CrewManager workers, BuildManager building, OceanManager sea, Camera sceneCamera)
        {
            if (IsInitialized) return true;
            if (config == null || config.RaftPrefab == null || config.RaftPrefab.GetComponent<RescueRaftView>() == null || workers == null || !workers.IsInitialized ||
                building == null || !building.IsInitialized || sea == null || !sea.IsInitialized || sceneCamera == null) return false;
            configuration = config; crew = workers; build = building; ocean = sea; camera = sceneCamera;
            remaining = Mathf.Max(1, config.FirstArrivalSeconds); IsInitialized = true; crew.AttachRecruitment(this); return true;
        }
        public void Tick(float deltaTime)
        {
            if (!IsInitialized) return;
            float dt = Mathf.Max(0, deltaTime);
            if (crew.Count >= CrewManager.Capacity) { if (HasRaft) Remove(); return; }
            if (!HasRaft)
            {
                remaining -= dt;
                if (dt > 0 && remaining <= 0) { if (!SpawnNearRaft()) remaining = Mathf.Max(1, configuration.RetrySeconds); }
            }
            else
            {
                Vector3 position = Position;
                if (!Marked)
                {
                    age += dt; var flow = ocean.GetDriftDirection(position, .3f);
                    position += new Vector3(flow.x, 0, flow.y) * (configuration.DriftSpeed * dt);
                }
                position.y = ocean.GetSurfaceHeight(position) + .12f; raft.transform.position = position;
                raft.transform.rotation = Quaternion.Euler(Mathf.Sin(ocean.ElapsedTime) * 2, 0, Mathf.Cos(ocean.ElapsedTime * .8f) * 2);
                if (!Marked && (age >= configuration.UnmarkedLifetime || DeckPathfinder.HasDeck(build.Cells, build.WorldToCell(position))))
                { Remove(); remaining = Mathf.Max(1, configuration.RetrySeconds); Message = "救生筏随海流漂远了，稍后还会遇到"; Notify(); }
            }
            if (build.WorldModalOpen || build.FacilityInputBlocked || build.CatalogOpen || build.Tool != BuildTool.None || Input.GetMouseButton(2) ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            if (Input.GetMouseButtonDown(0) && TryToggleAt(camera.ScreenPointToRay(Input.mousePosition))) build.FacilityInputBlocked = true;
        }
        public bool SpawnNearRaft()
        {
            if (!IsInitialized || HasRaft || !crew.CanRecruit(out _)) return false;
            DeckPathfinder.FindReachable(build.Cells, crew.CurrentCell, out _, out var distances);
            int best = int.MaxValue; Vector3 chosen = default; bool found = false;
            foreach (var entry in distances)
            {
                foreach (var direction in DeckPathfinder.Directions)
                {
                    Vector2Int water = entry.Key + direction;
                    if (build.Cells.ContainsKey(water)) continue;
                    bool mouth = false; foreach (var facility in build.Grills.Values) if (facility.Kind == FacilityKind.CollectionNet && facility.MouthCell == water) { mouth = true; break; }
                    if (mouth || entry.Value >= best) continue;
                    Vector3 origin = build.CellToWorld(entry.Key);
                    Vector3 outward = (build.CellToWorld(water) - origin).normalized;
                    chosen = origin + outward * Mathf.Min(configuration.Reach - .15f, build.TileSize * .5f + 1.3f);
                    best = entry.Value; found = true;
                }
            }
            if (!found) return false;
            Spawn(chosen); Message = "发现漂流救生筏！点击筏子标记救援"; Notify(); return true;
        }
        private void Spawn(Vector3 position)
        {
            raft = Instantiate(configuration.RaftPrefab, transform).GetComponent<RescueRaftView>(); raft.name = "Drifting Rescue Raft";
            raft.Initialize(camera); position.y = ocean.GetSurfaceHeight(position) + .12f; raft.transform.position = position; age = 0; Marked = false;
        }
        public bool TryToggleAt(Ray ray)
        {
            if (!IsInitialized || !HasRaft || build.WorldModalOpen || build.CatalogOpen || build.Tool != BuildTool.None) return false;
            var renderers = raft.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) return false;
            Bounds bounds = renderers[0].bounds; for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (!bounds.IntersectRay(ray)) return false;
            return SetMarked(!Marked);
        }
        public bool SetMarked(bool marked)
        {
            if (!HasRaft || Marked == marked || crew.Count >= CrewManager.Capacity) return false;
            Marked = marked; raft.SetMarked(marked);
            if (!marked && active != null) Cancel(active, active.Owner);
            Message = marked ? "已标记救援，船员会从甲板边缘接应" : "已取消救援标记"; Notify(); return true;
        }
        private bool InReach(Vector2Int cell) => DeckPathfinder.IsWalkable(build.Cells, cell) &&
            Vector2.Distance(new Vector2(Position.x, Position.z), new Vector2(build.CellToWorld(cell).x, build.CellToWorld(cell).z)) <= configuration.Reach;
        public bool TryFindTask(IReadOnlyDictionary<Vector2Int, int> distances, out Vector2Int approach)
        {
            approach = default;
            if (!IsInitialized || !HasRaft || !Marked || active != null || !crew.CanRecruit(out _)) return false;
            int best = int.MaxValue; bool found = false;
            foreach (var entry in distances) if (entry.Value < best && InReach(entry.Key)) { approach = entry.Key; best = entry.Value; found = true; }
            return found;
        }
        public RescueSession TryAcquire(object owner, Vector2Int approach)
        {
            if (owner == null || !IsInitialized || active != null || !HasRaft || !Marked || !InReach(approach) || !crew.CanRecruit(out _)) return null;
            active = new RescueSession { Owner = owner, Approach = approach, Raft = raft }; return active;
        }
        public bool IsValid(RescueSession session, object owner) => IsInitialized && session != null && !session.Ended && ReferenceEquals(active, session) &&
            ReferenceEquals(owner, session.Owner) && HasRaft && ReferenceEquals(raft, session.Raft) && Marked && crew.Count < CrewManager.Capacity && InReach(session.Approach);
        public bool Advance(RescueSession session, object owner, Vector2Int standing, float deltaTime)
        {
            if (!IsValid(session, owner) || standing != session.Approach) return false;
            session.Progress = Mathf.Clamp01(session.Progress + Mathf.Max(0, deltaTime) / Mathf.Max(.1f, configuration.RescueDuration));
            if (session.Progress < 1) return true;
            bool joined = crew.TryRecruit(out var member, () => { session.Ended = true; active = null; Remove(); });
            if (!joined) return false;
            Message = member.DisplayName + "加入营地！按 C 查看船员与分工"; Notify(); return true;
        }
        public void Cancel(RescueSession session, object owner)
        {
            if (session == null || session.Ended || !ReferenceEquals(active, session) || !ReferenceEquals(owner, session.Owner)) return;
            session.Ended = true; active = null;
        }
        private void Remove()
        {
            if (active != null) { active.Ended = true; active = null; }
            if (raft != null) { raft.gameObject.SetActive(false); Destroy(raft.gameObject); raft = null; }
            Marked = false;
        }
        public RescueSave Capture() => new RescueSave { Active = HasRaft, Position = Position, Marked = Marked, Age = age, Remaining = remaining };
        public void Restore(RescueSave data)
        {
            Remove(); remaining = data != null ? data.Remaining : configuration.FirstArrivalSeconds;
            if (data != null && data.Active && crew.Count < CrewManager.Capacity) { Spawn(data.Position); age = data.Age; Marked = data.Marked; raft.SetMarked(Marked); }
            Message = crew.Count == CrewManager.Capacity ? "两名船员已加入营地，可按 C 调整分工" : Marked ? "救援标记已恢复，等待船员接应" : "漂流途中会遇到救生筏，点击标记救援"; Notify();
        }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown() { Remove(); IsInitialized = false; Changed = null; }
    }
}
