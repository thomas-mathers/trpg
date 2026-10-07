using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class LocationHopCycle
{
    internal static IReadOnlyList<LocationConnector> Build(
        IReadOnlyCollection<LocationConnector> connectors,
        IReadOnlyList<Guid> orderedLocationIds
    )
    {
        var connectorsByOrigin = connectors.ToLookup(connector => connector.OriginLocationId);

        return
        [
            .. orderedLocationIds.SelectMany(
                (locationId, index) =>
                    ShortestPath(
                        connectorsByOrigin,
                        locationId,
                        orderedLocationIds[(index + 1) % orderedLocationIds.Count]
                    )
            ),
        ];
    }

    private static IReadOnlyList<LocationConnector> ShortestPath(
        ILookup<Guid, LocationConnector> connectorsByOrigin,
        Guid originLocationId,
        Guid destinationLocationId
    )
    {
        var cameFrom = new Dictionary<Guid, LocationConnector>();
        var visited = new HashSet<Guid> { originLocationId };
        var queue = new Queue<Guid>([originLocationId]);

        while (queue.TryDequeue(out var current) && current != destinationLocationId)
        {
            foreach (var connector in connectorsByOrigin[current].OrderBy(c => c.Id))
            {
                if (visited.Add(connector.DestinationLocationId))
                {
                    cameFrom[connector.DestinationLocationId] = connector;
                    queue.Enqueue(connector.DestinationLocationId);
                }
            }
        }

        return Reconstruct(cameFrom, originLocationId, destinationLocationId);
    }

    private static IReadOnlyList<LocationConnector> Reconstruct(
        IReadOnlyDictionary<Guid, LocationConnector> cameFrom,
        Guid originLocationId,
        Guid destinationLocationId
    )
    {
        var path = new List<LocationConnector>();
        var current = destinationLocationId;

        while (current != originLocationId)
        {
            if (!cameFrom.TryGetValue(current, out var connector))
            {
                return [];
            }

            path.Add(connector);
            current = connector.OriginLocationId;
        }

        path.Reverse();

        return path;
    }
}
