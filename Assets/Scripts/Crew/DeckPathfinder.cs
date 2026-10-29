using System.Collections.Generic;
using UnityEngine;
using RaftGame.Building;

namespace RaftGame.Crew
{
    /// <summary>Four-neighbor BFS over finished deck only; no NavMesh rebake when the raft grows.</summary>
    public static class DeckPathfinder
    {
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static bool IsWalkable(IReadOnlyDictionary<Vector2Int, DeckCell> cells, Vector2Int cell)
        {
            return cells.TryGetValue(cell, out DeckCell value) && value.State == DeckState.Complete && !value.IsBlocked;
        }

        public static bool HasDeck(IReadOnlyDictionary<Vector2Int, DeckCell> cells, Vector2Int cell) =>
            cells.TryGetValue(cell, out DeckCell value) && value.State == DeckState.Complete;

        public static void FindReachable(IReadOnlyDictionary<Vector2Int, DeckCell> cells, Vector2Int start,
            out Dictionary<Vector2Int, Vector2Int> parents, out Dictionary<Vector2Int, int> distances)
        {
            parents = new Dictionary<Vector2Int, Vector2Int>();
            distances = new Dictionary<Vector2Int, int>();
            if (!IsWalkable(cells, start)) return;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            distances.Add(start, 0);
            parents.Add(start, start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = current + direction;
                    if (distances.ContainsKey(next) || !IsWalkable(cells, next)) continue;
                    distances.Add(next, distances[current] + 1);
                    parents.Add(next, current);
                    queue.Enqueue(next);
                }
            }
        }

        public static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> parents, Vector2Int destination)
        {
            var path = new List<Vector2Int>();
            if (!parents.ContainsKey(destination)) return path;
            Vector2Int current = destination;
            while (true)
            {
                path.Add(current);
                Vector2Int previous = parents[current];
                if (previous == current) break;
                current = previous;
            }
            path.Reverse();
            return path;
        }
    }
}
