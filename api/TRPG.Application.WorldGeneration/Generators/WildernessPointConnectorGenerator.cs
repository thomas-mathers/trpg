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
                        (from, to) => new LocalPointConnectors.Path([from.Position, to.Position]),
                        connector => !IsDungeonEntrance(context, connector)
                    )
                ),
        ];

    private static bool IsDungeonEntrance(
        LocationLayoutContext context,
        LocationConnector connector
    ) =>
        context.LocationById[connector.OriginLocationId].Kind == LocationKind.Room
        || context.LocationById[connector.DestinationLocationId].Kind == LocationKind.Room;
}
