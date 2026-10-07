using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class ConnectorNodePlacer
{
    internal static IReadOnlyList<TravelNode> Place(
        LocationLayoutContext context,
        IReadOnlyDictionary<Guid, ConnectorExit> exitByConnectorId,
        IReadOnlyDictionary<Guid, Guid> portNodeIdByConnectorId
    )
    {
        var connectorsByPair = context.Connectors.ToLookup(connector => new ConnectorPair(
            connector.OriginLocationId,
            connector.DestinationLocationId
        ));
        var nodes = new List<TravelNode>();
        var arrivals = new Dictionary<Guid, Placement>();

        foreach (var connector in context.Connectors)
        {
            var exit = exitByConnectorId[connector.Id];
            var reverse = FindReverse(connectorsByPair, connector);
            var arrival = ResolveArrival(context, connector, reverse, exitByConnectorId);

            connector.ExitAngle = exit.FacingAngle;
            connector.StairDirection = exit.Stairs;
            connector.ArrivalAngle = arrival.Angle;
            arrivals[connector.Id] = arrival;

            if (portNodeIdByConnectorId.TryGetValue(connector.Id, out var portNodeId))
            {
                connector.OriginNodeId = portNodeId;
            }
            else
            {
                nodes.Add(
                    CreateNode(
                        context,
                        connector.OriginNodeId,
                        connector.OriginLocationId,
                        new Point(exit.Point.X, exit.Point.Y)
                    )
                );
            }
        }

        foreach (var connector in context.Connectors)
        {
            var reverse = FindReverse(connectorsByPair, connector);
            var destination = context.LocationById[connector.DestinationLocationId];

            if (destination.Kind == LocationKind.District && reverse is not null)
            {
                connector.DestinationNodeId = reverse.OriginNodeId;
                continue;
            }

            var arrival = arrivals[connector.Id];
            nodes.Add(
                CreateNode(
                    context,
                    connector.DestinationNodeId,
                    connector.DestinationLocationId,
                    new Point(arrival.X, arrival.Y)
                )
            );
        }

        return nodes;
    }

    private static Placement ResolveArrival(
        LocationLayoutContext context,
        LocationConnector connector,
        LocationConnector? reverse,
        IReadOnlyDictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        var destination = context.LocationById[connector.DestinationLocationId];
        var frame = new Footprint(Width: destination.Width, Depth: destination.Depth);

        return reverse is null
            ? ConnectorPointResolver.ResolveDefaultArrival(frame)
            : ConnectorPointResolver.KeepInside(
                ConnectorPointResolver.ResolveArrival(exitByConnectorId[reverse.Id]),
                frame
            );
    }

    private static TravelNode CreateNode(
        LocationLayoutContext context,
        Guid id,
        Guid locationId,
        Point position
    ) =>
        new()
        {
            Id = id,
            WorldId = context.LocationById[locationId].WorldId,
            LocationId = locationId,
            Position = position,
        };

    private static LocationConnector? FindReverse(
        ILookup<ConnectorPair, LocationConnector> connectorsByPair,
        LocationConnector connector
    )
    {
        var candidates = connectorsByPair[
            new ConnectorPair(connector.DestinationLocationId, connector.OriginLocationId)
        ]
            .OrderBy(candidate => candidate.Id)
            .ToArray();

        return candidates.FirstOrDefault(candidate => candidate.Name == connector.Name)
            ?? candidates.FirstOrDefault();
    }

    private record ConnectorPair(Guid OriginLocationId, Guid DestinationLocationId);
}
