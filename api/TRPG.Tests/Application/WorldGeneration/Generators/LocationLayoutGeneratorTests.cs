using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class LocationLayoutGeneratorTests
{
    private const double Tolerance = 1e-6;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_GivesEveryLocationAPositiveSize(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.NotEmpty(world.Input.Locations);
        Assert.All(
            world.Input.Locations,
            location =>
            {
                Assert.True(location.Width > 0);
                Assert.True(location.Depth > 0);
            }
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_KeepsEveryPropInsideItsLocation(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.NotEmpty(world.Input.Props);
        Assert.All(
            world.Input.Props,
            prop =>
            {
                var location = world.LocationById(prop.LocationId);
                Assert.True(
                    BoxOf(prop.X, prop.Y, prop.Angle, prop.Width, prop.Depth)
                        .IsInside(location.Width, location.Depth)
                );
            }
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_KeepsEveryBuildingInsideItsExterior(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            world.Input.Buildings,
            building =>
            {
                var exterior = world.LocationById(building.ExteriorLocationId);
                Assert.True(building.Width > 0 && building.Depth > 0);
                Assert.True(BoxOfBuilding(building).IsInside(exterior.Width, exterior.Depth));
            }
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_PlacesNoTwoBuildingsOverlapping_WithinOneExterior(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var overlapping = world
            .Input.Buildings.GroupBy(building => building.ExteriorLocationId)
            .SelectMany(group =>
            {
                var boxes = group.Select(BoxOfBuilding).ToArray();
                return boxes.SelectMany((box, index) => boxes.Skip(index + 1).Where(box.Overlaps));
            });
        Assert.Empty(overlapping);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_PlacesEveryConnectorPointInsideItsLocation(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.NotEmpty(world.Input.Connectors);
        Assert.All(
            world.Input.Connectors,
            connector =>
            {
                var origin = world.LocationById(connector.OriginLocationId);
                var destination = world.LocationById(connector.DestinationLocationId);
                Assert.True(IsInside(connector.ExitX, connector.ExitY, origin));
                Assert.True(IsInside(connector.ArrivalX, connector.ArrivalY, destination));
            }
        );
    }

    [Fact]
    public void Generate_ExitsABuildingAtItsDoorAndArrivesJustOutsideIt()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var building = world.Input.Buildings.First(building =>
            world.LocationById(building.ExteriorLocationId).Kind == LocationKind.District
        );
        var entrance = world.EntranceConnector(building);
        var leaving = world.Input.Connectors.Single(connector =>
            connector.OriginLocationId == entrance.DestinationLocationId
            && connector.DestinationLocationId == entrance.OriginLocationId
        );
        var doorX = building.X + building.Depth / 2 * Math.Sin(building.Angle);
        var doorY = building.Y - building.Depth / 2 * Math.Cos(building.Angle);
        Assert.Equal(doorX, entrance.ExitX, Tolerance);
        Assert.Equal(doorY, entrance.ExitY, Tolerance);
        Assert.Equal(building.Angle, entrance.ExitAngle, Tolerance);
        Assert.Equal(doorX + Math.Sin(building.Angle), leaving.ArrivalX, Tolerance);
        Assert.Equal(doorY - Math.Cos(building.Angle), leaving.ArrivalY, Tolerance);
        Assert.Equal(building.Angle, leaving.ArrivalAngle, Tolerance);
    }

    [Fact]
    public void Generate_ArrivesAtTheSouthDoorOfTheEntranceRoom_WhenEnteringABuilding()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var building = world.Input.Buildings.First(building =>
            world.LocationById(building.ExteriorLocationId).Kind == LocationKind.District
        );
        var entrance = world.EntranceConnector(building);
        var room = world.LocationById(entrance.DestinationLocationId);
        Assert.Equal(room.Depth - 1, entrance.ArrivalY, Tolerance);
        Assert.Equal(0, entrance.ArrivalAngle, Tolerance);
    }

    [Fact]
    public void Generate_ProducesTheSameLayout_WhenRunTwice()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);
        LocationLayoutGenerator.Generate(world.Input);
        var first = world.Snapshot();

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.Equal(first, world.Snapshot());
    }

    private static OrientedBox BoxOf(
        double x,
        double y,
        double angle,
        double width,
        double depth
    ) => OrientedBox.From(new Placement(x, y, angle), new Footprint(width, depth));

    private static OrientedBox BoxOfBuilding(Building building) =>
        BoxOf(building.X, building.Y, building.Angle, building.Width, building.Depth);

    private static bool IsInside(double x, double y, Location location) =>
        x >= -Tolerance
        && y >= -Tolerance
        && x <= location.Width + Tolerance
        && y <= location.Depth + Tolerance;
}
