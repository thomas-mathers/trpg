using System.Collections.ObjectModel;

namespace TRPG.Services;

internal static class Pathfinding
{
    public static ReadOnlyCollection<TEdge> Dijkstra<TNode, TEdge>(
        TNode origin,
        TNode destination,
        Func<TNode, IEnumerable<TEdge>> getNeighbors,
        Func<TEdge, float> getCost,
        Func<TEdge, TNode> getNode) where TNode : notnull
    {
        var distances = new Dictionary<TNode, float> { [origin] = 0 };
        var previous = new Dictionary<TNode, (TNode From, TEdge Edge)>();
        var queue = new PriorityQueue<TNode, float>();
        queue.Enqueue(origin, 0);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (EqualityComparer<TNode>.Default.Equals(current, destination)) break;

            foreach (var edge in getNeighbors(current))
            {
                var neighbor = getNode(edge);
                var newDist = distances[current] + getCost(edge);
                if (!distances.TryGetValue(neighbor, out var existing) || newDist < existing)
                {
                    distances[neighbor] = newDist;
                    previous[neighbor] = (current, edge);
                    queue.Enqueue(neighbor, newDist);
                }
            }
        }

        var path = new List<TEdge>();
        var node = destination;
        while (previous.TryGetValue(node, out var prev))
        {
            path.Insert(0, prev.Edge);
            node = prev.From;
        }
        return path.AsReadOnly();
    }
}
