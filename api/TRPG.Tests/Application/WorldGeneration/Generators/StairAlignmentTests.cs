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
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var pairs = StairPairs(world, layout)
            .Where(pair => !HasFlightsBothWays(world, pair))
            .ToArray();
        Assert.NotEmpty(pairs);
        Assert.All(
            pairs,
            pair =>
            {
                Assert.Equal(PlanOffset(world, pair.Up), PlanOffset(world, pair.Down), 6);
                Assert.Equal(pair.Up.Exit.Y, pair.Down.Exit.Y, 6);
            }
        );
    }

    [Fact]
    public void Generate_ArrivesPastTheFrontOfTheMatchingStair_OnTheFloorAboveOrBelow()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            StairPairs(world, layout),
            pair =>
            {
                var upstairs = world.LocationById(pair.Up.Connector.DestinationLocationId);
                var expected = ConnectorPointResolver.KeepInside(
                    ConnectorPointResolver.ResolveArrival(
                        new ConnectorExit(
                            pair.Down.Connector.Id,
                            new PlanarPoint(pair.Down.Exit.X, pair.Down.Exit.Y),
                            pair.Down.Connector.ExitAngle
                        )
                        {
                            Stairs = pair.Down.Connector.StairDirection,
                        }
                    ),
                    new Footprint(upstairs.Width, upstairs.Depth)
                );
                Assert.Equal(expected.X, pair.Up.Arrival.X, 6);
                Assert.Equal(expected.Y, pair.Up.Arrival.Y, 6);
            }
        );
    }

    [Fact]
    public void Generate_RecordsWhetherEachStairConnectorGoesUpOrDown()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            StairPairs(world, layout),
            pair =>
            {
                Assert.Equal(StairDirection.Up, pair.Up.Connector.StairDirection);
                Assert.Equal(StairDirection.Down, pair.Down.Connector.StairDirection);
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
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var stairs = Placed(world, layout, StairConnectors(world));
        Assert.NotEmpty(stairs);
        Assert.All(
            stairs,
            stair =>
            {
                var footprint = ExitKeepOut.Of(
                    new PlanarPoint(stair.Exit.X, stair.Exit.Y),
                    stair.Connector.ExitAngle,
                    stair.Connector.StairDirection
                );
                var solids = world
                    .Input.Props.Concat(layout.Props)
                    .Where(prop => prop.LocationId == stair.Connector.OriginLocationId)
                    .Where(prop =>
                        prop
                            is not Furniture
                            {
                                Model: PropModel.FurnitureRug or PropModel.FurnitureChandelier
                            }
                    )
                    .Select(prop =>
                        OrientedBox.From(
                            new Placement(prop.X, prop.Y, prop.Angle),
                            new Footprint(prop.Width, prop.Depth)
                        )
                    );
                var otherExits = world
                    .PlacedConnectors(layout)
                    .Where(other =>
                        other.Connector.OriginLocationId == stair.Connector.OriginLocationId
                        && other.Connector.Id != stair.Connector.Id
                    )
                    .Where(other => other.Connector.StairDirection is not null)
                    .Select(other =>
                        ExitKeepOut.Of(
                            new PlanarPoint(other.Exit.X, other.Exit.Y),
                            other.Connector.ExitAngle,
                            other.Connector.StairDirection
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
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            Placed(world, layout, StairConnectors(world)),
            placed =>
            {
                var destination = world.LocationById(placed.Connector.DestinationLocationId);
                Assert.InRange(placed.Arrival.X, 0, destination.Width);
                Assert.InRange(placed.Arrival.Y, 0, destination.Depth);
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
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var pairs = StairPairs(world, layout);
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
        new[] { pair.Up.Connector.OriginLocationId, pair.Down.Connector.OriginLocationId }.Any(
            roomId =>
                StairConnectors(world).Count(connector => connector.OriginLocationId == roomId) > 1
        );

    private static double PlanOffset(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        PlacedConnector placed
    ) => placed.Exit.X - world.LocationById(placed.Connector.OriginLocationId).Width / 2;

    private static IReadOnlyList<PlacedConnector> Placed(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        LocationLayoutResult layout,
        IReadOnlyCollection<LocationConnector> connectors
    ) => [.. world.PlacedConnectors(layout).Where(placed => connectors.Contains(placed.Connector))];

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
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        LocationLayoutResult layout
    )
    {
        var placedById = world.PlacedConnectors(layout).ToDictionary(placed => placed.Connector.Id);

        return
        [
            .. StairConnectors(world)
                .Where(connector =>
                    FloorOf(world, connector.DestinationLocationId)
                    > FloorOf(world, connector.OriginLocationId)
                )
                .Select(up => new StairPair(
                    placedById[up.Id],
                    placedById[
                        world
                            .Input.Connectors.First(down =>
                                down.OriginLocationId == up.DestinationLocationId
                                && down.DestinationLocationId == up.OriginLocationId
                            )
                            .Id
                    ]
                )),
        ];
    }

    private static int? FloorOf(MiniLayoutWorldBuilder.MiniLayoutWorld world, Guid locationId) =>
        world.Input.Rooms.FirstOrDefault(room => room.LocationId == locationId)?.FloorNumber;

    private sealed record StairPair(PlacedConnector Up, PlacedConnector Down);
}
