using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DoorPlacementTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Generate_PutsEveryRoomDoorNodeOnAWall()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var doors = RoomDoors(world, layout).Where(IsAxisAligned).ToArray();
        Assert.NotEmpty(doors);
        Assert.All(
            doors,
            door =>
            {
                var room = world.LocationById(door.Connector.OriginLocationId);
                Assert.True(IsOnWall(door.Exit, room), $"{door.Exit} is not on a wall");
            }
        );
    }

    [Fact]
    public void Generate_ArrivesOneMeterInsideTheDestinationDoor_WhenTheDoorHasAReturnDoor()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var placedById = world.PlacedConnectors(layout).ToDictionary(placed => placed.Connector.Id);
        var arrivals = RoomDoors(world, layout)
            .Select(door => (Door: door, Return: ReturnDoor(world, placedById, door)))
            .Where(pair => pair.Return is not null)
            .ToArray();
        Assert.NotEmpty(arrivals);
        Assert.All(
            arrivals,
            pair =>
            {
                var distance = Distance(pair.Door.Arrival, pair.Return!.Exit);
                Assert.Equal(ConnectorPointResolver.ArrivalInset, distance, Tolerance);
            }
        );
    }

    private static PlacedConnector[] RoomDoors(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        LocationLayoutResult layout
    ) =>
        [
            .. world
                .PlacedConnectors(layout)
                .Where(placed =>
                    placed.Connector.StairDirection is null
                    && world.LocationById(placed.Connector.OriginLocationId).Kind
                        == LocationKind.Room
                ),
        ];

    private static PlacedConnector? ReturnDoor(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        IReadOnlyDictionary<Guid, PlacedConnector> placedById,
        PlacedConnector door
    )
    {
        var destination = world.LocationById(door.Connector.DestinationLocationId);
        var back = world.Input.Connectors.FirstOrDefault(connector =>
            connector.OriginLocationId == destination.Id
            && connector.DestinationLocationId == door.Connector.OriginLocationId
            && connector.StairDirection is null
        );

        return destination.Kind == LocationKind.Room && back is not null
            ? placedById[back.Id]
            : null;
    }

    private static bool IsAxisAligned(PlacedConnector door) =>
        Math.Abs(Math.Sin(2 * door.Connector.ExitAngle)) < Tolerance;

    private static bool IsOnWall(Point point, Location room) =>
        Math.Abs(point.X) < Tolerance
        || Math.Abs(point.X - room.Width) < Tolerance
        || Math.Abs(point.Y) < Tolerance
        || Math.Abs(point.Y - room.Depth) < Tolerance;

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
}
