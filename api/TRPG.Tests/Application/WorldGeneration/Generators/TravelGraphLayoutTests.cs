using TRPG.Application.Scenes.Navigation;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class TravelGraphLayoutTests
{
    private const double Tolerance = 1e-6;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_JoinsNodesOfDifferentLocations_WithEveryLocationConnector(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var nodeById = layout.TravelNodes.ToDictionary(node => node.Id);
        Assert.NotEmpty(world.Input.Connectors);
        Assert.All(
            world.Input.Connectors,
            connector =>
            {
                Assert.NotEqual(connector.OriginLocationId, connector.DestinationLocationId);
                Assert.Equal(
                    connector.OriginLocationId,
                    nodeById[connector.OriginNodeId].LocationId
                );
                Assert.Equal(
                    connector.DestinationLocationId,
                    nodeById[connector.DestinationNodeId].LocationId
                );
            }
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_JoinsNodesOfOneLocation_WithEveryPointConnector(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var nodeById = layout.TravelNodes.ToDictionary(node => node.Id);
        Assert.NotEmpty(layout.PointConnectors);
        Assert.All(
            layout.PointConnectors,
            connector =>
            {
                Assert.Equal(connector.LocationId, nodeById[connector.OriginNodeId].LocationId);
                Assert.Equal(
                    connector.LocationId,
                    nodeById[connector.DestinationNodeId].LocationId
                );
            }
        );
    }

    [Fact]
    public void Generate_LeavesNodesOfDungeonEntrancesUnwalked_WhenWildernessIsPlanned()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);
        var roomLocationIds = world.Input.Rooms.Select(room => room.LocationId).ToHashSet();
        var entranceNodeIds = world
            .Input.Connectors.Where(connector =>
                roomLocationIds.Contains(connector.OriginLocationId)
                || roomLocationIds.Contains(connector.DestinationLocationId)
            )
            .SelectMany(connector => new[] { connector.OriginNodeId, connector.DestinationNodeId })
            .ToHashSet();

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var wildernessIds = world
            .Input.Locations.Where(location => location.Kind == LocationKind.Wilderness)
            .Select(location => location.Id)
            .ToHashSet();
        var wildernessWalks = layout.PointConnectors.Where(connector =>
            wildernessIds.Contains(connector.LocationId)
        );
        Assert.NotEmpty(wildernessWalks);
        Assert.All(
            wildernessWalks,
            connector =>
            {
                Assert.DoesNotContain(connector.OriginNodeId, entranceNodeIds);
                Assert.DoesNotContain(connector.DestinationNodeId, entranceNodeIds);
            }
        );
    }

    [Fact]
    public void Generate_MeasuresTheWildernessWalkAsAStraightLine()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var nodeById = layout.TravelNodes.ToDictionary(node => node.Id);
        var wildernessIds = world
            .Input.Locations.Where(location => location.Kind == LocationKind.Wilderness)
            .Select(location => location.Id)
            .ToHashSet();
        var wildernessWalks = layout.PointConnectors.Where(connector =>
            wildernessIds.Contains(connector.LocationId)
        );
        Assert.NotEmpty(wildernessWalks);
        Assert.All(
            wildernessWalks,
            connector =>
                Assert.Equal(
                    Straight(
                        nodeById[connector.OriginNodeId].Position,
                        nodeById[connector.DestinationNodeId].Position
                    ),
                    connector.Distance,
                    Tolerance
                )
        );
    }

    [Fact]
    public void Generate_MeasuresTheRoomWalkAsTheNavigationGridPathLength()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildHouseWorld([Guid.NewGuid()]);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var nodeById = layout.TravelNodes.ToDictionary(node => node.Id);
        var roomWalks = layout.PointConnectors.Where(connector =>
            world.LocationById(connector.LocationId).Kind == LocationKind.Room
        );
        Assert.NotEmpty(roomWalks);
        Assert.All(
            roomWalks,
            connector =>
            {
                var grid = RoomNavigationGrid.Build(
                    world.LocationById(connector.LocationId),
                    [
                        .. world.Input.Props.Where(prop => prop.LocationId == connector.LocationId),
                        .. layout.Props.Where(prop => prop.LocationId == connector.LocationId),
                    ]
                );
                var path = grid.FindPath(
                    nodeById[connector.OriginNodeId].Position,
                    nodeById[connector.DestinationNodeId].Position
                );

                Assert.Equal(PathLength(path), connector.Distance, Tolerance);
            }
        );
    }

    [Fact]
    public void Generate_CreatesWalkableApproachNodesForRoutableProps()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildHouseWorld([Guid.NewGuid()]);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var anchors = world
            .Input.Props.Concat(layout.Props)
            .Where(prop =>
                prop
                    is Seat
                        or Bed { AssignedCreatureId: not null }
                        or Workstation { AssignedCreatureId: not null }
            )
            .ToArray();
        var nodeIds = layout.TravelNodes.Select(node => node.Id).ToHashSet();
        Assert.NotEmpty(anchors);
        Assert.All(anchors, anchor => Assert.Contains(anchor.ApproachNodeId, nodeIds));
    }

    [Fact]
    public void Generate_JoinsEveryApproachNodeToTheGraphWithABidirectionalWalk()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildHouseWorld([Guid.NewGuid()]);

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var anchors = world
            .Input.Props.Concat(layout.Props)
            .Where(prop => prop.ApproachNodeId is not null)
            .ToArray();
        Assert.NotEmpty(anchors);
        Assert.All(
            anchors,
            anchor =>
                Assert.Contains(
                    layout.PointConnectors,
                    connector =>
                        connector.Bidirectional
                        && connector.OriginNodeId == anchor.ApproachNodeId
                        && connector.RoadClass is null
                        && connector.Distance > 0
                )
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Generate_SharesExitNodes_BetweenReciprocalDistrictConnectors(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);
        var districtIds = world
            .Input.Locations.Where(location => location.Kind == LocationKind.District)
            .Select(location => location.Id)
            .ToHashSet();

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var districtLinks = world.Input.Connectors.Where(connector =>
            districtIds.Contains(connector.OriginLocationId)
            && districtIds.Contains(connector.DestinationLocationId)
        );
        Assert.NotEmpty(districtLinks);
        Assert.All(
            districtLinks,
            connector =>
                Assert.Contains(
                    districtLinks,
                    reverse =>
                        reverse.OriginNodeId == connector.DestinationNodeId
                        && reverse.DestinationNodeId == connector.OriginNodeId
                )
        );
    }

    private static double PathLength(IReadOnlyList<Point> path) =>
        path.Zip(path.Skip(1)).Sum(pair => Straight(pair.First, pair.Second));

    private static double Straight(Point from, Point to) =>
        Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
}
