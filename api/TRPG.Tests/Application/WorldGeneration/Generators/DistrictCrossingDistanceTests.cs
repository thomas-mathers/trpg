using TRPG.Application.Common.Navigation;
using TRPG.Application.Scenes.Navigation;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictCrossingDistanceTests
{
    private const double Tolerance = 0.5;

    [Fact]
    public void Generate_ChargesTheWalkedDistance_WhenARouteCrossesADistrict()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);
        var layout = LocationLayoutGenerator.Generate(world.Input);
        var district = world.Input.Districts.First();
        var entrance = world.Input.Connectors.Single(connector =>
            connector.DestinationLocationId == district.LocationId
            && world.LocationById(connector.OriginLocationId).Kind == LocationKind.Wilderness
        );
        var exit = world.Input.Connectors.Single(connector =>
            connector.OriginLocationId == district.LocationId
            && world.LocationById(connector.DestinationLocationId).Kind == LocationKind.District
        );
        var walked = WalkedMeters(world, layout, district, entrance, exit);

        // Act
        var charged = new TravelGraph(layout.PointConnectors, layout.TravelNodes).ShortestDistance(
            entrance.DestinationNodeId,
            exit.OriginNodeId
        );

        // Assert
        Assert.Equal(walked, charged, Tolerance);
    }

    private static double WalkedMeters(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        LocationLayoutResult layout,
        District district,
        LocationConnector entrance,
        LocationConnector exit
    )
    {
        var nodeById = layout.TravelNodes.ToDictionary(node => node.Id);
        var network = new LocationPointNetwork(
            [.. layout.TravelNodes.Where(node => node.LocationId == district.LocationId)],
            [
                .. layout.PointConnectors.Where(connector =>
                    connector.LocationId == district.LocationId
                ),
            ],
            world
                .Input.Connectors.Where(connector =>
                    connector.OriginLocationId == district.LocationId
                    || connector.DestinationLocationId == district.LocationId
                )
                .SelectMany(connector =>
                    new[] { connector.OriginNodeId, connector.DestinationNodeId }
                )
                .ToHashSet()
        );
        var grid = DistrictNavigationGrid.Build(
            world.LocationById(district.LocationId),
            world.Input.Buildings.Where(building =>
                building.ExteriorLocationId == district.LocationId
            )
        );
        var path = DistrictRoutePlanner.Plan(
            nodeById[entrance.DestinationNodeId].Position,
            nodeById[exit.OriginNodeId].Position,
            network,
            grid
        );

        return path.Zip(path.Skip(1)).Sum(pair => Distance(pair.First, pair.Second));
    }

    private static double Distance(Point from, Point to) =>
        Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
}
