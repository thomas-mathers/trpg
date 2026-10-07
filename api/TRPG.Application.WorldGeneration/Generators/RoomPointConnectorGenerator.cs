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
                    (from, to) => PathLength(grid.FindPath(from.Position, to.Position))
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

    private static double PathLength(IReadOnlyList<Point> path) =>
        path.Zip(path.Skip(1))
            .Sum(pair =>
                Math.Sqrt(
                    Math.Pow(pair.Second.X - pair.First.X, 2)
                        + Math.Pow(pair.Second.Y - pair.First.Y, 2)
                )
            );
}
