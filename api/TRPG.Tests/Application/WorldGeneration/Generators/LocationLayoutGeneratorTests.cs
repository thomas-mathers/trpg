using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class LocationLayoutGeneratorTests
{
    private const double Tolerance = 1e-6;

    private static readonly BuildingType[] CityBuildingTypes = Enum.GetValues<BuildingType>()
        .Where(type => !BuildingTypes.Dungeon.Contains(type))
        .ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Generate_GivesEveryLocationAPositiveSize(int iteration)
    {
        // Arrange
        var world = BuildWorld(iteration);

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
        var world = BuildWorld(iteration);

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
        var world = BuildWorld(iteration);

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
        var world = BuildWorld(iteration);

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
        var world = BuildWorld(iteration);

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
        var world = BuildWorld(1);

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
        Assert.Equal(doorX + Math.Sin(building.Angle), leaving.ArrivalX, Tolerance);
        Assert.Equal(doorY - Math.Cos(building.Angle), leaving.ArrivalY, Tolerance);
        Assert.Equal(building.Angle, leaving.ArrivalAngle, Tolerance);
    }

    [Fact]
    public void Generate_ArrivesAtTheSouthDoorOfTheEntranceRoom_WhenEnteringABuilding()
    {
        // Arrange
        var world = BuildWorld(1);

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
        var world = BuildWorld(1);
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

    private static MiniWorld BuildWorld(int iteration)
    {
        var worldId = Guid.NewGuid();
        var stateA = MakeState(worldId, new Point(10, 10));
        var stateB = MakeState(worldId, new Point(40, -20));
        var wildernessA = MakeWilderness(worldId, stateA.Id);
        var wildernessB = MakeWilderness(worldId, stateB.Id);
        var cityId = Guid.NewGuid();
        var districts = new[] { DistrictType.CityCenter, DistrictType.Residential }
            .Select(type => DistrictGenerator.Generate(type, cityId, stateA.Id, worldId))
            .ToArray();
        List<Location> locations =
        [
            wildernessA,
            wildernessB,
            .. districts.Select(district => district.Location),
        ];
        var props = new List<Prop>(districts.SelectMany(district => district.Seats));
        var rooms = new List<Room>();
        var buildings = new List<Building>();
        var connectors = new List<LocationConnector>
        {
            MakeConnector(worldId, districts[0].Location, districts[1].Location, "Path"),
            MakeConnector(worldId, districts[1].Location, districts[0].Location, "Path"),
            MakeConnector(worldId, districts[0].Location, wildernessA, "Path"),
            MakeConnector(worldId, wildernessA, districts[0].Location, "Path"),
            MakeConnector(worldId, wildernessA, wildernessB, "Trail"),
            MakeConnector(worldId, wildernessB, wildernessA, "Trail"),
        };

        foreach (var type in CityBuildingTypes)
        {
            var spec = BuildingSpecCatalog.GetSpecs(
                type,
                Guid.NewGuid(),
                [Guid.NewGuid()],
                bedroomGroups: null
            );
            var result = new BuildingGenerator().Generate(
                new BuildingGeneratorInput(districts[(int)type % 2].Location, spec)
                {
                    Name = type.ToString(),
                }
            );
            buildings.Add(result.Building);
            rooms.AddRange(result.Rooms);
            locations.AddRange(result.Locations);
            props.AddRange(result.Props);
            connectors.AddRange(result.LocationConnectors);
        }

        foreach (var type in BuildingTypes.Dungeon)
        {
            var dungeon = DungeonGenerator.Generate(
                new DungeonGeneratorInput([], wildernessA, worldId)
                {
                    BuildingType = type,
                    Random = new Random(iteration * 100 + (int)type),
                }
            );
            buildings.Add(dungeon.Building);
            rooms.AddRange(dungeon.Rooms);
            locations.AddRange(dungeon.Locations);
            connectors.AddRange(dungeon.LocationConnectors);
        }

        return new MiniWorld(
            new LocationLayoutInput(
                locations,
                props,
                buildings,
                rooms,
                connectors,
                [stateA, stateB]
            )
        );
    }

    private static State MakeState(Guid worldId, Point center) =>
        new() { WorldId = worldId, Center = center };

    private static Location MakeWilderness(Guid worldId, Guid stateId) =>
        new()
        {
            WorldId = worldId,
            StateId = stateId,
            Kind = LocationKind.Wilderness,
        };

    private static LocationConnector MakeConnector(
        Guid worldId,
        Location origin,
        Location destination,
        string name
    ) =>
        new()
        {
            WorldId = worldId,
            OriginLocationId = origin.Id,
            DestinationLocationId = destination.Id,
            Name = name,
            DestinationLabel = name,
        };

    private sealed record MiniWorld(LocationLayoutInput Input)
    {
        internal Location LocationById(Guid id) => Input.Locations.Single(l => l.Id == id);

        internal LocationConnector EntranceConnector(Building building) =>
            Input.Connectors.Single(connector =>
                connector.OriginLocationId == building.ExteriorLocationId
                && Input.Rooms.Any(room =>
                    room.LocationId == connector.DestinationLocationId
                    && room.BuildingId == building.Id
                )
            );

        internal IReadOnlyList<double> Snapshot() =>
            Input
                .Locations.SelectMany(location => new[] { location.Width, location.Depth })
                .Concat(
                    Input.Props.SelectMany(prop => new[] { prop.X, prop.Y, prop.Angle, prop.Width })
                )
                .Concat(Input.Buildings.SelectMany(building => new[] { building.X, building.Y }))
                .Concat(
                    Input.Connectors.SelectMany(connector =>
                        new[]
                        {
                            connector.ExitX,
                            connector.ExitY,
                            connector.ArrivalX,
                            connector.ArrivalY,
                        }
                    )
                )
                .ToArray();
    }
}
