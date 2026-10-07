using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class LocalPointConnectors
{
    internal static IReadOnlyList<PointConnector> Complete(
        LocationLayoutContext context,
        Location location,
        IReadOnlyDictionary<Guid, TravelNode> nodeById,
        Func<TravelNode, TravelNode, double> measure,
        Func<LocationConnector, bool>? includeConnector = null
    )
    {
        var included = context
            .Connectors.Where(connector => includeConnector?.Invoke(connector) ?? true)
            .ToArray();
        var arrivals = included
            .Where(connector => connector.DestinationLocationId == location.Id)
            .Select(connector => nodeById[connector.DestinationNodeId])
            .DistinctBy(node => node.Id)
            .ToArray();
        var exits = included
            .Where(connector => connector.OriginLocationId == location.Id)
            .Select(connector => nodeById[connector.OriginNodeId])
            .DistinctBy(node => node.Id)
            .ToArray();

        return
        [
            .. arrivals.SelectMany(arrival =>
                exits.Select(exit => new PointConnector
                {
                    WorldId = location.WorldId,
                    LocationId = location.Id,
                    OriginNodeId = arrival.Id,
                    DestinationNodeId = exit.Id,
                    Distance = measure(arrival, exit),
                })
            ),
        ];
    }
}
