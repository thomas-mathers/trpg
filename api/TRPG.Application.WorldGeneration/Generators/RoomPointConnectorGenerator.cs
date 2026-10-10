using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoomPointConnectorGenerator
{
    internal static IReadOnlyList<PointConnector> Generate(
        LocationLayoutContext context,
        IReadOnlyDictionary<Guid, TravelNode> nodeById,
        IReadOnlyCollection<Prop> furniture
    )
    {
        var furnitureByLocationId = furniture.ToLookup(prop => prop.LocationId);
        var connectors = new List<PointConnector>();

        foreach (var location in context.Locations.Where(location => IsWalkable(context, location)))
        {
            var grid = RoomNavigationGrid.Build(
                location,
                [.. context.PropsByLocationId[location.Id], .. furnitureByLocationId[location.Id]]
            );

            connectors.AddRange(
                LocalPointConnectors.Complete(
                    context,
                    location,
                    nodeById,
                    (from, to) =>
                        new LocalPointConnectors.Path(grid.FindPath(from.Position, to.Position))
                )
            );
        }

        return connectors;
    }

    private static bool IsWalkable(LocationLayoutContext context, Location location) =>
        location.Kind == LocationKind.Room
        && !BuildingTypes.Dungeon.Contains(
            context.BuildingById[context.RoomByLocationId[location.Id].BuildingId].BuildingType
        );
}
