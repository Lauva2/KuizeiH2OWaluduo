using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Crew;

namespace RaftGame.Needs
{
    public sealed class RestSession
    {
        public GrillCell Bed { get; internal set; }
        public float Progress { get; internal set; }
        internal object Owner;
        internal CrewNeeds Needs;
        internal float StartingEnergy;
        internal bool Begun, Ended;
    }
    public sealed class RestManager : MonoBehaviour
    {
        private readonly Dictionary<GrillCell, RestSession> active = new Dictionary<GrillCell, RestSession>();
        private RestConfig configuration;
        private BuildManager build;
        private ComfortManager comfort;
        public float RecoveryMultiplier => comfort != null ? comfort.RestMultiplier : 1;
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;
        public bool Initialize(RestConfig config, BuildManager building, CrewManager crew, ComfortManager campComfort = null)
        {
            if (IsInitialized) return true;
            if (config == null || config.WakeAt <= config.RestBelow || config.RecoveryPerSecond <= 0 ||
                building == null || !building.IsInitialized || crew == null || !crew.IsInitialized) return false;
            configuration = config; build = building; comfort = campComfort; IsInitialized = true;
            build.FacilityChanging += Interrupt; build.LayoutRestoring += CancelAll; crew.AttachRest(this); return true;
        }
        public bool NeedsRest(CrewNeeds needs) => IsInitialized && needs != null && needs.IsInitialized && (needs.Energy <= configuration.RestBelow || (needs.RestRequested && needs.Energy < configuration.WakeAt));
        private bool Exists(GrillCell bed) => build.IsCurrentFacility(bed) && bed.Kind == FacilityKind.Bed && !bed.IsBeingMoved && bed.BedView != null && bed.BedView.SleepAnchor != null;
        public bool TryFindBed(IReadOnlyDictionary<Vector2Int, int> distances, CrewNeeds needs, out GrillCell bed)
        {
            bed = null; if (!NeedsRest(needs)) return false;
            int best = int.MaxValue;
            foreach (var candidate in build.Grills.Values)
                if (Exists(candidate) && !active.ContainsKey(candidate) && distances.TryGetValue(candidate.WorkCell, out int distance) &&
                    (distance < best || (distance == best && (bed == null || candidate.Coordinate.x < bed.Coordinate.x || (candidate.Coordinate.x == bed.Coordinate.x && candidate.Coordinate.y < bed.Coordinate.y)))))
                { bed = candidate; best = distance; }
            return bed != null;
        }
        public RestSession TryAcquire(object owner, GrillCell bed, CrewNeeds needs)
        {
            if (owner == null || !NeedsRest(needs) || !Exists(bed) || active.ContainsKey(bed)) return null;
            foreach (var entry in active.Values) if (ReferenceEquals(entry.Owner, owner) || ReferenceEquals(entry.Needs, needs)) return null;
            var session = new RestSession { Bed = bed, Owner = owner, Needs = needs }; active.Add(bed, session); needs.SetRestRequested(true); Notify(); return session;
        }
        private bool Owns(RestSession session, object owner) => session != null && !session.Ended && ReferenceEquals(session.Owner, owner) && active.TryGetValue(session.Bed, out var current) && ReferenceEquals(current, session);
        public bool IsValid(RestSession session, object owner) => IsInitialized && Owns(session, owner) && Exists(session.Bed) && session.Needs.IsInitialized;
        public bool Begin(RestSession session, object owner, Vector2Int cell)
        {
            if (!IsValid(session, owner) || session.Begun || cell != session.Bed.WorkCell) return false;
            session.Begun = true; session.StartingEnergy = session.Needs.Energy; session.Bed.BedView.Present(true); Notify(); return true;
        }
        public bool Advance(RestSession session, object owner, Vector2Int cell, float deltaTime)
        {
            if (!IsValid(session, owner) || !session.Begun || cell != session.Bed.WorkCell) return false;
            if (deltaTime <= 0) return true;
            if (session.Needs.Energy < configuration.WakeAt) session.Needs.SetEnergy(Mathf.Min(configuration.WakeAt, session.Needs.Energy + deltaTime * configuration.RecoveryPerSecond * RecoveryMultiplier));
            session.Progress = Mathf.Clamp01((session.Needs.Energy - session.StartingEnergy) / Mathf.Max(.01f, configuration.WakeAt - session.StartingEnergy));
            if (session.Needs.Energy < configuration.WakeAt) return true;
            session.Progress = 1; session.Needs.SetRestRequested(false); End(session); return true;
        }
        public void Cancel(RestSession session, object owner) { if (Owns(session, owner)) End(session); }
        private void End(RestSession session)
        {
            session.Ended = true; active.Remove(session.Bed); session.Bed.BedView?.Present(false); Notify();
        }
        public bool IsOccupied(GrillCell bed) => bed != null && active.ContainsKey(bed);
        private void Interrupt(GrillCell bed) { if (active.TryGetValue(bed, out var session)) End(session); }
        private void CancelAll() { foreach (var session in new List<RestSession>(active.Values)) End(session); }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            CancelAll(); if (build != null) { build.FacilityChanging -= Interrupt; build.LayoutRestoring -= CancelAll; }
            IsInitialized = false; Changed = null;
        }
    }
}
