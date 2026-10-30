using System;
using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;
using RaftGame.Crew;
using RaftGame.Resources;

namespace RaftGame.Salvage
{
    [Serializable] public sealed class NetSupply { public FloatingKind Kind; public int Amount; }
    public sealed class NetCollectionSession
    {
        public GrillCell Net { get; internal set; }
        public float Progress { get; internal set; }
        internal NetSupply Supply;
        internal object Owner;
        internal bool Ended, Completing;
    }

    public sealed class CollectionNetManager : MonoBehaviour
    {
        public const int Capacity = 4;
        private CollectionNetConfig configuration;
        private BuildManager building;
        private SalvageManager salvage;
        private ResourceManager resources;
        private LootManager loot;
        private readonly Dictionary<GrillCell, NetCollectionSession> sessions = new Dictionary<GrillCell, NetCollectionSession>();
        private int observedLayout = -1;
        private bool capturing;
        public bool IsInitialized { get; private set; }
        public int Version { get; private set; }
        public event Action Changed;

        public bool Initialize(CollectionNetConfig config, BuildManager build, SalvageManager drift, ResourceManager inventory, LootManager rewards)
        {
            if (IsInitialized) return true;
            if (config == null || config.CollectionDuration <= 0 || config.CaptureHalfWidthInTiles <= 0 || config.CaptureHalfWidthInTiles > .5f ||
                build == null || !build.IsInitialized || drift == null || !drift.IsInitialized || inventory == null || !inventory.IsInitialized || rewards == null || !rewards.IsInitialized) return false;
            configuration = config; building = build; salvage = drift; resources = inventory; loot = rewards;
            salvage.NetCapture = TryCapture; building.Changed += OnLayoutChanged; building.FacilityChanging += Interrupt;
            building.LayoutRestoring += CancelAll; IsInitialized = true; OnLayoutChanged(); return true;
        }

        private bool Available(GrillCell cell) => building.IsCurrentFacility(cell) && cell.Kind == FacilityKind.CollectionNet && !cell.IsBeingMoved;

        public bool TryCapture(FloatingResource item, Vector3 from, Vector3 to)
        {
            if (!IsInitialized || capturing || !salvage.IsActive(item) || item.Collecting || item.Amount <= 0) return false;
            GrillCell chosen = null; float nearest = float.MaxValue;
            foreach (GrillCell net in building.Grills.Values)
            {
                if (!Available(net) || net.Contents.Count >= Capacity || building.Cells.ContainsKey(net.MouthCell)) continue;
                Vector3 center = building.CellToWorld(net.Coordinate);
                Quaternion inverse = Quaternion.Inverse(building.FacilityRotation(net.Rotation));
                Vector3 a = inverse * (from - center), b = inverse * (to - center);
                float size = building.TileSize, half = size * configuration.CaptureHalfWidthInTiles;
                if (!Segment(a.x, a.z, b.x, b.z, -half, half, -1.5f * size, -.5f * size, out float entry)) continue;
                if (entry < nearest || (Mathf.Approximately(entry, nearest) && (chosen == null || Earlier(net.Coordinate, chosen.Coordinate))))
                { nearest = entry; chosen = net; }
            }
            if (chosen == null) return false;
            // A long simulation step must not sweep supplies through a deck into a net on the far side.
            Quaternion boardInverse = Quaternion.Inverse(building.FacilityRotation(0));
            foreach (DeckCell deck in building.Cells.Values)
            {
                if (deck.State != DeckState.Complete) continue;
                Vector3 center = building.CellToWorld(deck.Coordinate), a = boardInverse * (from - center), b = boardInverse * (to - center);
                float half = building.TileSize * .5f;
                if (Segment(a.x, a.z, b.x, b.z, -half, half, -half, half, out float entry) && entry < nearest - .0001f) return false;
            }
            capturing = true;
            try
            {
                bool stored = salvage.StoreInNet(item, () =>
                {
                    chosen.NetItems.Add(new NetSupply { Kind = item.Kind, Amount = item.Amount }); Present(chosen);
                });
                if (stored) Notify(); return stored;
            }
            finally { capturing = false; }
        }

        private static bool Segment(float ax, float ay, float bx, float by, float minX, float maxX, float minY, float maxY, out float entry)
        {
            entry = 0; float exit = 1;
            return Slab(ax, bx - ax, minX, maxX, ref entry, ref exit) && Slab(ay, by - ay, minY, maxY, ref entry, ref exit);
        }
        private static bool Slab(float origin, float delta, float min, float max, ref float entry, ref float exit)
        {
            if (Mathf.Abs(delta) < .000001f) return origin >= min && origin <= max;
            float a = (min - origin) / delta, b = (max - origin) / delta;
            entry = Mathf.Max(entry, Mathf.Min(a, b)); exit = Mathf.Min(exit, Mathf.Max(a, b)); return entry <= exit;
        }

        public bool TryFindTask(IReadOnlyDictionary<Vector2Int, int> distances, out GrillCell result)
        {
            result = null; if (!IsInitialized) return false;
            int best = int.MaxValue; bool bestFull = false;
            foreach (GrillCell net in building.Grills.Values)
            {
                if (!Available(net) || net.Contents.Count == 0 || sessions.ContainsKey(net) || !CanReceive(net.Contents[0]) || !distances.TryGetValue(net.WorkCell, out int distance)) continue;
                bool full = net.Contents.Count == Capacity;
                if (result == null || (full && !bestFull) || (full == bestFull && (distance < best || (distance == best && Earlier(net.Coordinate, result.Coordinate)))))
                { result = net; best = distance; bestFull = full; }
            }
            return result != null;
        }
        private bool CanReceive(NetSupply supply) => supply.Kind == FloatingKind.SupplyCrate || resources.CanGrant(Output(supply));
        private static ResourceCost Output(NetSupply supply) => supply.Kind == FloatingKind.Wood ? new ResourceCost(supply.Amount, 0) :
            supply.Kind == FloatingKind.Bottle ? new ResourceCost(0, 0, 0, 0, supply.Amount) : new ResourceCost(0, supply.Amount);

        public NetCollectionSession TryAcquire(object owner, GrillCell net)
        {
            if (!IsInitialized || owner == null || !Available(net) || net.Contents.Count == 0 || sessions.ContainsKey(net) || !CanReceive(net.Contents[0])) return null;
            var session = new NetCollectionSession { Net = net, Owner = owner, Supply = net.Contents[0] };
            sessions.Add(net, session); return session;
        }
        public bool IsValid(NetCollectionSession session, object owner) => IsInitialized && session != null && !session.Ended && !session.Completing &&
            ReferenceEquals(owner, session.Owner) && Available(session.Net) && sessions.TryGetValue(session.Net, out var current) && ReferenceEquals(session, current) &&
            session.Net.Contents.Count > 0 && ReferenceEquals(session.Supply, session.Net.Contents[0]);

        public bool Advance(NetCollectionSession session, object owner, Vector2Int cell, float deltaTime)
        {
            if (!IsValid(session, owner) || cell != session.Net.WorkCell || !DeckPathfinder.IsWalkable(building.Cells, cell)) return false;
            session.Progress = Mathf.Clamp01(session.Progress + Mathf.Max(0, deltaTime) / Mathf.Max(.1f, configuration.CollectionDuration));
            if (session.Progress < 1) return true;
            session.Completing = true;
            try
            {
                Action consume = () => { session.Net.NetItems.RemoveAt(0); session.Ended = true; sessions.Remove(session.Net); Present(session.Net); };
                bool received = session.Supply.Kind == FloatingKind.SupplyCrate ? loot.OpenCrate(consume) : resources.TryGrant(Output(session.Supply), consume);
                if (received) Notify(); return received;
            }
            finally { session.Completing = false; }
        }
        public void Cancel(NetCollectionSession session, object owner)
        {
            if (!IsValid(session, owner)) return;
            session.Ended = true; sessions.Remove(session.Net);
        }
        private void Interrupt(GrillCell cell) { if (sessions.TryGetValue(cell, out var session)) Cancel(session, session.Owner); }
        private void CancelAll()
        {
            foreach (var session in sessions.Values) session.Ended = true;
            sessions.Clear();
        }
        private void OnLayoutChanged()
        {
            if (!IsInitialized || observedLayout == building.LayoutVersion) return;
            observedLayout = building.LayoutVersion;
            foreach (var net in building.Grills.Values) if (net.Kind == FacilityKind.CollectionNet && net.State == DeckState.Complete) Present(net);
            Notify();
        }
        private static bool Earlier(Vector2Int a, Vector2Int b) => a.x < b.x || (a.x == b.x && a.y < b.y);
        private static void Present(GrillCell net) { if (net.NetView != null) net.NetView.Present(net.Contents, net.IsBeingMoved); }
        private void Notify() { Version++; Changed?.Invoke(); }
        public void Shutdown()
        {
            if (!IsInitialized) return;
            CancelAll(); salvage.NetCapture = null; building.Changed -= OnLayoutChanged; building.FacilityChanging -= Interrupt; building.LayoutRestoring -= CancelAll;
            IsInitialized = false; Changed = null;
        }
    }
}
