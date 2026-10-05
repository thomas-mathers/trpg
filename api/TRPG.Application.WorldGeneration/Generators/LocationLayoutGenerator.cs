using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class LocationLayoutGenerator
{
    internal static LocationLayoutResult Generate(LocationLayoutInput input)
    {
        var context = new LocationLayoutContext(input);
        var exitByConnectorId = new Dictionary<Guid, ConnectorExit>();

        var roomFurniture = RoomLayoutPass.Run(context, exitByConnectorId);
        var exterior = ExteriorLayoutPass.Run(context, exitByConnectorId);
        ApplyConnectorPoints(context, exitByConnectorId);

        return exterior with
        {
            Props = [.. roomFurniture, .. exterior.Props],
        };
    }

    private static void ApplyConnectorPoints(
        LocationLayoutContext context,
        IReadOnlyDictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        var connectorsByPair = context.Connectors.ToLookup(connector => new ConnectorPair(
            connector.OriginLocationId,
            connector.DestinationLocationId
        ));

        foreach (var connector in context.Connectors)
        {
            var exit = exitByConnectorId[connector.Id];
            var reverse = FindReverse(connectorsByPair, connector);
            var frame = DestinationFrame(context, connector);
            var arrival = reverse is null
                ? ConnectorPointResolver.ResolveDefaultArrival(frame)
                : ConnectorPointResolver.KeepInside(
                    ConnectorPointResolver.ResolveArrival(exitByConnectorId[reverse.Id]),
                    frame
                );

            connector.ExitX = exit.Point.X;
            connector.ExitY = exit.Point.Y;
            connector.ExitAngle = exit.FacingAngle;
            connector.StairDirection = exit.Stairs;
            connector.ArrivalX = arrival.X;
            connector.ArrivalY = arrival.Y;
            connector.ArrivalAngle = arrival.Angle;
        }
    }

    private static Footprint DestinationFrame(
        LocationLayoutContext context,
        LocationConnector connector
    )
    {
        var destination = context.LocationById[connector.DestinationLocationId];

        return new Footprint(Width: destination.Width, Depth: destination.Depth);
    }

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
