using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class OutdoorSignLayoutTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Generate_PlacesOneNameSignBesideEachDistrictBuildingDoor()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var props = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var districtBuildings = DistrictBuildings(world);
        Assert.NotEmpty(districtBuildings);
        Assert.All(
            districtBuildings,
            building =>
            {
                var sign = Assert.Single(
                    props.OfType<Sign>(),
                    candidate =>
                        candidate.LocationId == building.ExteriorLocationId
                        && candidate.Description == building.Name
                );
                Assert.Equal(building.Angle, sign.Angle, Tolerance);
                var along = AlongFacade(building, sign);
                Assert.InRange(Math.Abs(along), 1, 3);
            }
        );
    }

    [Fact]
    public void Generate_PlacesNoNameSignsOutsideDistricts()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var props = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var districtIds = world
            .Input.Locations.Where(location => location.Kind == LocationKind.District)
            .Select(location => location.Id)
            .ToHashSet();
        Assert.All(props.OfType<Sign>(), sign => Assert.Contains(sign.LocationId, districtIds));
    }

    [Fact]
    public void Generate_PlacesAnExitSignNearEachDistrictExit_ReadingToItsDestination()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var props = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var exits = world
            .Input.Connectors.Where(connector =>
                world.LocationById(connector.OriginLocationId).Kind == LocationKind.District
                && world.LocationById(connector.DestinationLocationId).Kind
                    is LocationKind.District
                        or LocationKind.Wilderness
            )
            .ToArray();
        Assert.NotEmpty(exits);
        Assert.All(
            exits,
            exit =>
            {
                var sign = Assert.Single(
                    props.OfType<Sign>(),
                    candidate =>
                        candidate.LocationId == exit.OriginLocationId
                        && candidate.Description == $"To {exit.DestinationLabel}"
                        && Distance(candidate, exit) < 5
                );
                Assert.Equal(exit.ExitAngle, sign.Angle, Tolerance);
            }
        );
    }

    [Fact]
    public void Generate_KeepsEverySignInsideItsDistrictAndClearOfBuildings()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var props = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            props.OfType<Sign>(),
            sign =>
            {
                var location = world.LocationById(sign.LocationId);
                var box = OrientedBox.From(
                    new Placement(sign.X, sign.Y, sign.Angle),
                    new Footprint(sign.Width, sign.Depth)
                );
                Assert.True(box.IsInside(location.Width, location.Depth));
                Assert.DoesNotContain(
                    world.Input.Buildings.Where(building =>
                        building.ExteriorLocationId == sign.LocationId
                    ),
                    building =>
                        box.Overlaps(
                            OrientedBox.From(
                                new Placement(building.X, building.Y, building.Angle),
                                new Footprint(building.Width, building.Depth)
                            )
                        )
                );
            }
        );
    }

    private static Building[] DistrictBuildings(MiniLayoutWorldBuilder.MiniLayoutWorld world) =>
        world
            .Input.Buildings.Where(building =>
                world.LocationById(building.ExteriorLocationId).Kind == LocationKind.District
            )
            .ToArray();

    private static double AlongFacade(Building building, Prop sign)
    {
        var (sin, cos) = Math.SinCos(building.Angle);

        return (sign.X - building.X) * cos + (sign.Y - building.Y) * sin;
    }

    private static double Distance(Prop sign, LocationConnector exit) =>
        Math.Sqrt(Math.Pow(sign.X - exit.ExitX, 2) + Math.Pow(sign.Y - exit.ExitY, 2));
}
