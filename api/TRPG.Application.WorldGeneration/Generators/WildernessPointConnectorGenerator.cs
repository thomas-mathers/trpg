using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class WildernessPointConnectorGenerator
{
    internal static IReadOnlyList<PointConnector> Generate(
        LocationLayoutContext context,
        IReadOnlyDictionary<Guid, TravelNode> nodeById
    ) =>
        [
            .. context
                .Locations.Where(location => location.Kind == LocationKind.Wilderness)
                .SelectMany(location =>
                    LocalPointConnectors.Complete(
                        context,
                        location,
                        nodeById,
                        StraightDistance,
                        connector => !IsDungeonEntrance(context, connector)
                    )
                ),
        ];

    private static double StraightDistance(TravelNode from, TravelNode to) =>
        Math.Sqrt(
            Math.Pow(to.Position.X - from.Position.X, 2)
                + Math.Pow(to.Position.Y - from.Position.Y, 2)
        );

    private static bool IsDungeonEntrance(
        LocationLayoutContext context,
        LocationConnector connector
    ) =>
        context.LocationById[connector.OriginLocationId].Kind == LocationKind.Room
        || context.LocationById[connector.DestinationLocationId].Kind == LocationKind.Room;
}
