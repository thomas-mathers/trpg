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

        foreach (var connector in context.Connectors)
        {
            var exit = exitByConnectorId[connector.Id];
            var reverse = FindReverse(connectorsByPair, connector);
            var arrival = ResolveArrival(context, connector, reverse, exitByConnectorId);

            connector.ExitAngle = exit.FacingAngle;
            connector.StairDirection = exit.Stairs;
            connector.ArrivalAngle = arrival.Angle;

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
                        PortPosition(context, connector.OriginLocationId, exit)
                    )
                );
            }
        }

        foreach (var connector in context.Connectors)
        {
            var reverse = FindReverse(connectorsByPair, connector);

            if (reverse is not null && SharesPort(exitByConnectorId, connector, reverse))
            {
                connector.DestinationNodeId = reverse.OriginNodeId;
                continue;
            }

            var arrival = ResolveArrival(context, connector, reverse, exitByConnectorId);
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

    private static Point PortPosition(
        LocationLayoutContext context,
        Guid locationId,
        ConnectorExit exit
    )
    {
        var location = context.LocationById[locationId];
        if (location.Kind == LocationKind.District || exit.Stairs is not null)
        {
            return new Point(exit.Point.X, exit.Point.Y);
        }

        var arrival = ConnectorPointResolver.KeepInside(
            ConnectorPointResolver.ResolveArrival(exit),
            new Footprint(location.Width, location.Depth)
        );
        return new Point(arrival.X, arrival.Y);
    }

    private static bool SharesPort(
        IReadOnlyDictionary<Guid, ConnectorExit> exitByConnectorId,
        LocationConnector connector,
        LocationConnector reverse
    ) =>
        exitByConnectorId[connector.Id].Stairs is null
        && exitByConnectorId[reverse.Id].Stairs is null;

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
