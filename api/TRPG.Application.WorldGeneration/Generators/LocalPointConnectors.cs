using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class LocalPointConnectors
{
    internal sealed record Path(IReadOnlyList<Point> Points)
    {
        internal double Distance =>
            Points
                .Zip(Points.Skip(1))
                .Sum(pair =>
                    Math.Sqrt(
                        Math.Pow(pair.Second.X - pair.First.X, 2)
                            + Math.Pow(pair.Second.Y - pair.First.Y, 2)
                    )
                );
    }

    internal static IReadOnlyList<PointConnector> Complete(
        LocationLayoutContext context,
        Location location,
        IReadOnlyDictionary<Guid, TravelNode> nodeById,
        Func<TravelNode, TravelNode, Path> pathBetween,
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
                exits
                    .Where(exit => exit.Id != arrival.Id)
                    .Select(exit =>
                        CreateConnector(location, arrival, exit, pathBetween(arrival, exit))
                    )
            ),
        ];
    }

    private static PointConnector CreateConnector(
        Location location,
        TravelNode origin,
        TravelNode destination,
        Path path
    ) =>
        new()
        {
            WorldId = location.WorldId,
            LocationId = location.Id,
            OriginNodeId = origin.Id,
            DestinationNodeId = destination.Id,
            Distance = path.Distance,
            Waypoints = new Polyline { Points = path.Points.Skip(1).SkipLast(1).ToList() },
        };
}
