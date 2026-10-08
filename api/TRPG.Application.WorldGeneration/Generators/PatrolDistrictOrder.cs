using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class PatrolDistrictOrder
{
    internal static IReadOnlyList<Guid> Build(
        IReadOnlyList<Guid> districtLocationIds,
        IReadOnlyCollection<LocationConnector> connectors
    )
    {
        var neighbors = districtLocationIds.ToDictionary(
            id => id,
            id =>
                districtLocationIds
                    .Where(other =>
                        other != id
                        && connectors.Any(connector =>
                            connector.OriginLocationId == id
                            && connector.DestinationLocationId == other
                        )
                    )
                    .ToArray()
        );
        var visited = new HashSet<Guid>();
        var order = new List<Guid>();

        foreach (var root in districtLocationIds.OrderByDescending(id => neighbors[id].Length))
        {
            Visit(root, neighbors, visited, order);
        }

        return order;
    }

    private static void Visit(
        Guid districtLocationId,
        IReadOnlyDictionary<Guid, Guid[]> neighbors,
        HashSet<Guid> visited,
        List<Guid> order
    )
    {
        if (!visited.Add(districtLocationId))
        {
            return;
        }

        order.Add(districtLocationId);
        foreach (var neighbor in neighbors[districtLocationId])
        {
            Visit(neighbor, neighbors, visited, order);
        }
    }
}
