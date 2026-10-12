namespace TRPG.Domain.Models;

public record PlacedConnector(LocationConnector Connector, Point Exit, Point Arrival)
{
    public static IReadOnlyList<PlacedConnector> Place(
        IEnumerable<LocationConnector> connectors,
        IEnumerable<TravelNode> nodes
    )
    {
        var positionByNodeId = nodes.ToDictionary(node => node.Id, node => node.Position);

        return
        [
            .. connectors.Select(connector => new PlacedConnector(
                connector,
                positionByNodeId[connector.OriginNodeId],
                ArrivalPoint(connector, positionByNodeId[connector.DestinationNodeId])
            )),
        ];
    }

    private static Point ArrivalPoint(LocationConnector connector, Point destination) =>
        new(
            destination.X + connector.ArrivalInset * Math.Sin(connector.ArrivalAngle),
            destination.Y - connector.ArrivalInset * Math.Cos(connector.ArrivalAngle)
        );
}
