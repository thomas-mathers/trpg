namespace TRPG.Application.Common.Algorithms;

internal static class Graphs
{
    public static IReadOnlyList<(TKey From, TKey To)> MinimumSpanningTree<TKey>(
        TKey start,
        Func<TKey, IEnumerable<TKey>> getNeighbors,
        Func<TKey, TKey, double> getCost
    )
        where TKey : notnull
    {
        var cheapestCost = new Dictionary<TKey, double> { [start] = 0 };
        var cheapestEdge = new Dictionary<TKey, TKey>();
        var frontier = new PriorityQueue<TKey, double>();
        var visited = new HashSet<TKey>();

        frontier.Enqueue(start, 0);

        while (frontier.TryDequeue(out var from, out _))
        {
            if (!visited.Add(from))
            {
                continue;
            }

            foreach (var to in getNeighbors(from))
            {
                if (visited.Contains(to))
                {
                    continue;
                }

                var cost = getCost(from, to);
                if (cheapestCost.TryGetValue(to, out var best) && cost >= best)
                {
                    continue;
                }

                cheapestCost[to] = cost;
                cheapestEdge[to] = from;
                frontier.Enqueue(to, cost);
            }
        }

        var edges = new List<(TKey from, TKey to)>();

        foreach (var (to, from) in cheapestEdge)
        {
            edges.Add((from, to));
        }

        return edges.ToArray();
    }

    public static IReadOnlyList<TKey> ShortestPath<TKey>(
        TKey origin,
        TKey destination,
        Func<TKey, IEnumerable<TKey>> getNeighbors,
        Func<TKey, TKey, double> getCost
    )
        where TKey : notnull
    {
        var costs = new Dictionary<TKey, double> { [origin] = 0 };
        var cameFrom = new Dictionary<TKey, TKey>();
        var frontier = new PriorityQueue<TKey, double>();

        frontier.Enqueue(origin, 0);

        while (frontier.Count > 0)
        {
            var from = frontier.Dequeue();

            if (EqualityComparer<TKey>.Default.Equals(from, destination))
            {
                break;
            }

            foreach (var to in getNeighbors(from))
            {
                var cost = costs[from] + getCost(from, to);

                if (!costs.TryGetValue(to, out var bestCost) || cost < bestCost)
                {
                    costs[to] = cost;
                    cameFrom[to] = from;
                    frontier.Enqueue(to, cost);
                }
            }
        }

        var path = new List<TKey>();
        var node = destination;

        while (cameFrom.TryGetValue(node, out var prev))
        {
            path.Insert(0, node);
            node = prev;
        }

        if (path.Count == 0 && !EqualityComparer<TKey>.Default.Equals(origin, destination))
        {
            return path.ToArray();
        }

        path.Insert(0, origin);

        return path.ToArray();
    }
}
