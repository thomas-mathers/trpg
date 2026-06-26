using System.Collections.ObjectModel;

namespace TRPG.Algorithms;

internal static class Graphs {
    public static ReadOnlyCollection<(TKey from, TKey to)> Prims<TKey>(
        List<TKey> vertices,
        Func<TKey, IEnumerable<TKey>> getNeighbors,
        Func<TKey, TKey, float> getCost
    ) where TKey : notnull {
        var cheapestCost = new Dictionary<TKey, float>();
        var cheapestEdge = new Dictionary<TKey, TKey>();
        
        foreach (var u in vertices) {
            cheapestCost[u] = float.PositiveInfinity;
        }
        
        cheapestCost[vertices[0]] = 0;
        
        var unexplored = new HashSet<TKey>(vertices);

        while (unexplored.Count > 0) {
            var from = unexplored.MinBy(v => cheapestCost[v]);

            if (from is null || float.IsPositiveInfinity(cheapestCost[from])) {
                break;
            }
            
            unexplored.Remove(from);

            foreach (var to in getNeighbors(from)) {
                var cost = getCost(from, to);
                
                if (!unexplored.Contains(to) || cost >= cheapestCost[to]) {
                    continue;
                }
                
                cheapestCost[to] = cost;
                cheapestEdge[to] = from;
            }
        }

        var edges = new List<(TKey from, TKey to)>();
        
        foreach (var (to, from) in cheapestEdge) {
            edges.Add((from, to));
        }

        return edges.AsReadOnly();
    }

    public static ReadOnlyCollection<TKey> Dijkstra<TKey>(
        TKey origin,
        TKey destination,
        Func<TKey, IEnumerable<TKey>> getNeighbors,
        Func<TKey, TKey, float> getCost) where TKey : notnull {
        var distances = new Dictionary<TKey, float> { [origin] = 0 };
        var previous = new Dictionary<TKey, TKey>();
        var queue = new PriorityQueue<TKey, float>();
        queue.Enqueue(origin, 0);

        while (queue.Count > 0) {
            var current = queue.Dequeue();
            if (EqualityComparer<TKey>.Default.Equals(current, destination)) {
                break;
            }

            foreach (var neighbor in getNeighbors(current)) {
                var newDist = distances[current] + getCost(current, neighbor);
                if (!distances.TryGetValue(neighbor, out var existing) || newDist < existing) {
                    distances[neighbor] = newDist;
                    previous[neighbor] = current;
                    queue.Enqueue(neighbor, newDist);
                }
            }
        }

        var path = new List<TKey>();
        var node = destination;
        
        while (previous.TryGetValue(node, out var prev)) {
            path.Insert(0, node);
            node = prev;
        }

        if (path.Count == 0 && !EqualityComparer<TKey>.Default.Equals(origin, destination)) {
            return path.AsReadOnly();
        }

        path.Insert(0, origin);
        return path.AsReadOnly();
    }
}