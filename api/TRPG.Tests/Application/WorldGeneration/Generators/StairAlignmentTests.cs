using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class StairAlignmentTests
{
    [Fact]
    public void Generate_PlacesEachStairExitAtTheSamePlanPoint_AsTheStairOnTheFloorAboveOrBelow()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var pairs = StairPairs(world).Where(pair => !HasFlightsBothWays(world, pair)).ToArray();
        Assert.NotEmpty(pairs);
        Assert.All(
            pairs,
            pair =>
            {
                Assert.Equal(PlanOffset(world, pair.Up), PlanOffset(world, pair.Down), 6);
                Assert.Equal(pair.Up.ExitY, pair.Down.ExitY, 6);
            }
        );
    }

    [Fact]
    public void Generate_ArrivesPastTheFrontOfTheMatchingStair_OnTheFloorAboveOrBelow()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            StairPairs(world),
            pair =>
            {
                var upstairs = world.LocationById(pair.Up.DestinationLocationId);
                var expected = ConnectorPointResolver.KeepInside(
                    ConnectorPointResolver.ResolveArrival(
                        new ConnectorExit(
                            pair.Down.Id,
                            new PlanarPoint(pair.Down.ExitX, pair.Down.ExitY),
                            pair.Down.ExitAngle
                        )
                        {
                            Stairs = pair.Down.StairDirection,
                        }
                    ),
                    new Footprint(upstairs.Width, upstairs.Depth)
                );
                Assert.Equal(expected.X, pair.Up.ArrivalX, 6);
                Assert.Equal(expected.Y, pair.Up.ArrivalY, 6);
            }
        );
    }

    [Fact]
    public void Generate_RecordsWhetherEachStairConnectorGoesUpOrDown()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            StairPairs(world),
            pair =>
            {
                Assert.Equal(StairDirection.Up, pair.Up.StairDirection);
                Assert.Equal(StairDirection.Down, pair.Down.StairDirection);
            }
        );
    }

    [Fact]
    public void Generate_LeavesDoorsWithoutAStairDirection()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);
        var stairs = StairConnectors(world).ToHashSet();

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            world.Input.Connectors.Where(connector => !stairs.Contains(connector)),
            connector => Assert.Null(connector.StairDirection)
        );
    }

    [Fact]
    public void Generate_KeepsPropsAndOtherExitsOutOfEveryStairFootprint()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var stairs = StairConnectors(world);
        Assert.NotEmpty(stairs);
        Assert.All(
            stairs,
            stair =>
            {
                var footprint = ExitKeepOut.Of(
                    new PlanarPoint(stair.ExitX, stair.ExitY),
                    stair.ExitAngle,
                    stair.StairDirection
                );
                var solids = world
                    .Input.Props.Concat(furniture)
                    .Where(prop => prop.LocationId == stair.OriginLocationId)
                    .Where(prop => prop is not Furniture { Model: PropModel.FurnitureRug })
                    .Select(prop =>
                        OrientedBox.From(
                            new Placement(prop.X, prop.Y, prop.Angle),
                            new Footprint(prop.Width, prop.Depth)
                        )
                    );
                var otherExits = world
                    .Input.Connectors.Where(other =>
                        other.OriginLocationId == stair.OriginLocationId && other.Id != stair.Id
                    )
                    .Where(other => other.StairDirection is not null)
                    .Select(other =>
                        ExitKeepOut.Of(
                            new PlanarPoint(other.ExitX, other.ExitY),
                            other.ExitAngle,
                            other.StairDirection
                        )
                    )
                    .Select(box => box with { Width = box.Width - 0.1, Depth = box.Depth - 0.1 });
                Assert.DoesNotContain(solids.Concat(otherExits), box => box.Overlaps(footprint));
            }
        );
    }

    [Fact]
    public void Generate_KeepsStairArrivalsInsideTheirRooms()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            StairConnectors(world),
            connector =>
            {
                var destination = world.LocationById(connector.DestinationLocationId);
                Assert.InRange(connector.ArrivalX, 0, destination.Width);
                Assert.InRange(connector.ArrivalY, 0, destination.Depth);
            }
        );
    }

    [Fact]
    public void Generate_StacksTheStairsOfHouses()
    {
        // Arrange
        var memberIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var world = MiniLayoutWorldBuilder.BuildHouseWorld(memberIds);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var pairs = StairPairs(world);
        Assert.NotEmpty(pairs);
        Assert.All(
            pairs,
            pair => Assert.Equal(PlanOffset(world, pair.Up), PlanOffset(world, pair.Down), 6)
        );
    }

    private static bool HasFlightsBothWays(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        StairPair pair
    ) =>
        new[] { pair.Up.OriginLocationId, pair.Down.OriginLocationId }.Any(roomId =>
            StairConnectors(world).Count(connector => connector.OriginLocationId == roomId) > 1
        );

    private static double PlanOffset(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        LocationConnector connector
    ) => connector.ExitX - world.LocationById(connector.OriginLocationId).Width / 2;

    private static IReadOnlyList<LocationConnector> StairConnectors(
        MiniLayoutWorldBuilder.MiniLayoutWorld world
    ) =>
        world
            .Input.Connectors.Where(connector =>
                FloorOf(world, connector.OriginLocationId) is { } from
                && FloorOf(world, connector.DestinationLocationId) is { } to
                && from != to
            )
            .ToArray();

    private static IReadOnlyList<StairPair> StairPairs(
        MiniLayoutWorldBuilder.MiniLayoutWorld world
    ) =>
        StairConnectors(world)
            .Where(connector =>
                FloorOf(world, connector.DestinationLocationId)
                > FloorOf(world, connector.OriginLocationId)
            )
            .Select(up => new StairPair(
                up,
                world.Input.Connectors.First(down =>
                    down.OriginLocationId == up.DestinationLocationId
                    && down.DestinationLocationId == up.OriginLocationId
                )
            ))
            .ToArray();

    private static int? FloorOf(MiniLayoutWorldBuilder.MiniLayoutWorld world, Guid locationId) =>
        world.Input.Rooms.FirstOrDefault(room => room.LocationId == locationId)?.FloorNumber;

    private sealed record StairPair(LocationConnector Up, LocationConnector Down);
}
