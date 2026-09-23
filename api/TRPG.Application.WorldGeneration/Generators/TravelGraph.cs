using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record TravelGraphEdge(Guid ConnectorId, Guid DestinationLocationId, float Distance);

internal record TravelPathLeg(
    Guid OriginLocationId,
    Guid ConnectorId,
    Guid DestinationLocationId,
    float Distance
);

internal static class TravelGraph
{
    public static IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> Build(
        WorldGeneratorResult world
    )
    {
        var travelConnectorsByConnectorId = world.TravelConnectors.ToDictionary(connector =>
            connector.ConnectorId
        );
        return world
            .LocationConnectors.Where(connector =>
                travelConnectorsByConnectorId.ContainsKey(connector.Id)
            )
            .GroupBy(connector => connector.OriginLocationId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<TravelGraphEdge>)
                        group
                            .Select(connector => new TravelGraphEdge(
                                connector.Id,
                                connector.DestinationLocationId,
                                travelConnectorsByConnectorId[connector.Id].Distance
                            ))
                            .ToArray()
            );
    }

    public static IReadOnlyList<TravelPathLeg> FindShortestPath(
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        Guid originLocationId,
        Guid destinationLocationId
    )
    {
        var distances = new Dictionary<Guid, float> { [originLocationId] = 0 };
        var previous = new Dictionary<Guid, PreviousTravelEdge>();
        var queue = new PriorityQueue<Guid, float>();
        queue.Enqueue(originLocationId, 0);

        while (queue.TryDequeue(out var locationId, out var distance))
        {
            if (locationId == destinationLocationId)
            {
                break;
            }

            if (distance > distances[locationId])
            {
                continue;
            }

            foreach (var edge in graph.GetValueOrDefault(locationId, []))
            {
                var candidateDistance = distance + edge.Distance;
                if (
                    distances.TryGetValue(edge.DestinationLocationId, out var knownDistance)
                    && knownDistance <= candidateDistance
                )
                {
                    continue;
                }

                distances[edge.DestinationLocationId] = candidateDistance;
                previous[edge.DestinationLocationId] = new PreviousTravelEdge(
                    locationId,
                    edge.ConnectorId,
                    edge.Distance
                );
                queue.Enqueue(edge.DestinationLocationId, candidateDistance);
            }
        }

        if (!distances.ContainsKey(destinationLocationId))
        {
            return [];
        }

        var reversed = new List<TravelPathLeg>();
        for (var current = destinationLocationId; current != originLocationId; )
        {
            var edge = previous[current];
            reversed.Add(
                new TravelPathLeg(edge.OriginLocationId, edge.ConnectorId, current, edge.Distance)
            );
            current = edge.OriginLocationId;
        }
        reversed.Reverse();
        return reversed.ToArray();
    }

    public static IReadOnlyList<TravelPathLeg> BuildCycle(
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        IReadOnlyList<Guid> orderedLocations
    )
    {
        var legs = new List<TravelPathLeg>();
        for (var index = 0; index < orderedLocations.Count; index++)
        {
            var from = orderedLocations[index];
            var to = orderedLocations[(index + 1) % orderedLocations.Count];
            legs.AddRange(FindShortestPath(graph, from, to));
        }
        return legs.ToArray();
    }

    private record PreviousTravelEdge(Guid OriginLocationId, Guid ConnectorId, float Distance);
}
