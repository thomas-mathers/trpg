using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;
using static TRPG.Tests.Helpers.MiniLayoutWorldBuilder;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CreatureLayoutGeneratorTests
{
    private const int CreaturesPerLocation = 3;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Place_KeepsEveryCreatureInsideItsLocation(int iteration)
    {
        // Arrange
        var world = LaidOutWorld(iteration);
        var creatures = CreaturesInEveryLocation(world);

        // Act
        CreatureLayoutGenerator.Place(LayoutInput(world, creatures));

        // Assert
        Assert.All(
            creatures,
            creature =>
            {
                var location = world.LocationById(creature.LocationId);
                Assert.True(
                    BodyAt(creature).IsInside(location.Width, location.Depth),
                    $"{location.Kind} {location.Name}: {creature.X}, {creature.Y}"
                );
            }
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Place_KeepsACreatureClearOfProps(int iteration)
    {
        // Arrange
        var world = LaidOutWorld(iteration);
        var creatures = CreaturesInEveryLocation(world, count: 1);

        // Act
        CreatureLayoutGenerator.Place(LayoutInput(world, creatures));

        // Assert
        var propOverlaps = creatures.Where(creature =>
            world
                .Input.Props.Where(prop => prop.LocationId == creature.LocationId && prop.Width > 0)
                .Any(prop => PropBox(prop).Overlaps(BodyAt(creature)))
        );
        Assert.Empty(propOverlaps);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Place_KeepsCreaturesApart_WhenTheLocationIsOutdoors(int iteration)
    {
        // Arrange
        var world = LaidOutWorld(iteration);
        var outdoors = world
            .Input.Locations.Where(location => location.Kind != LocationKind.Room)
            .ToArray();
        var creatures = CreaturesInEveryLocation(outdoors, CreaturesPerLocation);

        // Act
        CreatureLayoutGenerator.Place(LayoutInput(world, creatures));

        // Assert
        var overlapping = creatures.Where(creature =>
            creatures.Any(other =>
                other != creature
                && other.LocationId == creature.LocationId
                && BodyAt(other).Overlaps(BodyAt(creature))
            )
        );
        Assert.Empty(overlapping);
    }

    [Fact]
    public void Place_PutsAWorkerNextToTheirWorkstationFacingIt()
    {
        // Arrange
        var world = LaidOutWorld(1);
        var workstation = world
            .Input.Props.OfType<Workstation>()
            .First(candidate => candidate.Width > 0);
        var worker = new Creature
        {
            LocationId = workstation.LocationId,
            WorldId = workstation.WorldId,
        };
        workstation.OccupantId = worker.Id;

        // Act
        CreatureLayoutGenerator.Place(LayoutInput(world, [worker]));

        // Assert
        var distance = Math.Sqrt(
            Math.Pow(worker.X - workstation.X, 2) + Math.Pow(worker.Y - workstation.Y, 2)
        );
        var reach = Math.Max(workstation.Width, workstation.Depth) / 2 + 1.5;
        Assert.True(distance <= reach, $"distance {distance} exceeds {reach}");
    }

    [Fact]
    public void Place_PutsAWorkerBehindTheirTradeCounterFacingTheSameWay()
    {
        // Arrange
        var world = LaidOutWorld(1);
        var lobby = world.Input.Rooms.First(room => room.Name == "Lobby");
        var counter = world
            .Input.Props.OfType<Workstation>()
            .First(candidate =>
                candidate.LocationId == lobby.LocationId
                && candidate.WorkstationType == WorkstationType.Trade
            );
        var worker = new Creature { LocationId = counter.LocationId, WorldId = counter.WorldId };
        counter.OccupantId = worker.Id;

        // Act
        CreatureLayoutGenerator.Place(LayoutInput(world, [worker]));

        // Assert
        var towardFront =
            ((worker.X - counter.X) * Math.Sin(counter.Angle))
            - ((worker.Y - counter.Y) * Math.Cos(counter.Angle));
        Assert.True(towardFront < 0, $"worker is in front of the counter: {towardFront}");
        Assert.Equal(counter.Angle, worker.Angle, 1e-9);
    }

    [Fact]
    public void Place_PutsASeatedCreatureOnTheirSeat()
    {
        // Arrange
        var world = LaidOutWorld(1);
        var seat = world.Input.Props.OfType<Seat>().First(candidate => candidate.Width > 0);
        var sitter = new Creature { LocationId = seat.LocationId, WorldId = seat.WorldId };
        seat.OccupantId = sitter.Id;

        // Act
        CreatureLayoutGenerator.Place(LayoutInput(world, [sitter]));

        // Assert
        Assert.Equal(seat.X, sitter.X, 1.5);
        Assert.Equal(seat.Y, sitter.Y, 1.5);
    }

    [Fact]
    public void Place_ProducesTheSamePoses_WhenRunTwice()
    {
        // Arrange
        var world = LaidOutWorld(1);
        var creatures = CreaturesInEveryLocation(world);
        CreatureLayoutGenerator.Place(LayoutInput(world, creatures));
        var first = creatures
            .Select(creature => (creature.X, creature.Y, creature.Angle))
            .ToArray();

        // Act
        CreatureLayoutGenerator.Place(LayoutInput(world, creatures));

        // Assert
        Assert.Equal(first, creatures.Select(creature => (creature.X, creature.Y, creature.Angle)));
    }

    [Fact]
    public void PlaceAtLocationCenter_PutsThePlayerNearTheCenterClearOfBuildings()
    {
        // Arrange
        var world = LaidOutWorld(1);
        var district = world.Input.Locations.First(location =>
            location.Kind == LocationKind.District
        );
        var player = new Creature { LocationId = district.Id, WorldId = district.WorldId };

        // Act
        CreatureLayoutGenerator.PlaceAtLocationCenter(LayoutInput(world, [player]));

        // Assert
        var buildingOverlaps = world
            .Input.Buildings.Where(building => building.ExteriorLocationId == district.Id)
            .Where(building =>
                OrientedBox
                    .From(
                        new Placement(building.X, building.Y, building.Angle),
                        new Footprint(building.Width, building.Depth)
                    )
                    .Overlaps(BodyAt(player))
            );
        Assert.True(BodyAt(player).IsInside(district.Width, district.Depth));
        Assert.Empty(buildingOverlaps);
    }

    [Fact]
    public void PlaceAtArrival_PutsTheCreatureAtTheMatchingConnectorArrivalPoint()
    {
        // Arrange
        var origin = Builders.MakeLocation(width: 20, depth: 20);
        var room = Builders.MakeLocation(width: 10, depth: 10);
        var connector = Builders.MakeLocationConnector(origin.Id, room.Id, arrivalAngle: 1.5);
        var creature = Builders.MakeCreature(locationId: room.Id, previousLocationId: origin.Id);

        // Act
        CreatureLayoutGenerator.PlaceAtArrival(
            new CreatureLayoutInput([origin, room], [], [], [Placed(connector, 2, 7)], [creature])
        );

        // Assert
        Assert.Equal((2d, 7d, 1.5), (creature.X, creature.Y, creature.Angle));
    }

    [Fact]
    public void PlaceAtArrival_PutsTheCreatureAtTheDefaultArrival_WhenNoConnectorMatches()
    {
        // Arrange
        var room = Builders.MakeLocation(width: 10, depth: 8);
        var creature = Builders.MakeCreature(locationId: room.Id);

        // Act
        CreatureLayoutGenerator.PlaceAtArrival(
            new CreatureLayoutInput([room], [], [], [], [creature])
        );

        // Assert
        Assert.Equal((5d, 7d, 0d), (creature.X, creature.Y, creature.Angle));
    }

    [Fact]
    public void PlaceAtArrival_MovesTheCreatureOffAProp_WhenTheArrivalPointIsBlocked()
    {
        // Arrange
        var origin = Builders.MakeLocation(width: 20, depth: 20);
        var room = Builders.MakeLocation(width: 10, depth: 10);
        var connector = Builders.MakeLocationConnector(origin.Id, room.Id);
        var crate = Builders.MakeContainer(locationId: room.Id, x: 5, y: 5, width: 2, depth: 2);
        var creature = Builders.MakeCreature(locationId: room.Id, previousLocationId: origin.Id);

        // Act
        CreatureLayoutGenerator.PlaceAtArrival(
            new CreatureLayoutInput(
                [origin, room],
                [crate],
                [],
                [Placed(connector, 5, 5)],
                [creature]
            )
        );

        // Assert
        Assert.False(PropBox(crate).Overlaps(BodyAt(creature)));
    }

    private static PlacedConnector Placed(
        LocationConnector connector,
        double arrivalX,
        double arrivalY
    ) => new(connector, new Point(0, 0), new Point(arrivalX, arrivalY));

    private static MiniLayoutWorld LaidOutWorld(int iteration)
    {
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);
        world.Layout = LocationLayoutGenerator.Generate(world.Input);

        return world;
    }

    private static CreatureLayoutInput LayoutInput(
        MiniLayoutWorld world,
        IReadOnlyList<Creature> creatures
    ) =>
        new(
            [.. world.Input.Locations],
            [.. world.Input.Props],
            [.. world.Input.Buildings],
            [.. world.PlacedConnectors(world.Layout!)],
            creatures
        );

    private static Creature[] CreaturesInEveryLocation(
        MiniLayoutWorld world,
        int count = CreaturesPerLocation
    ) => CreaturesInEveryLocation([.. world.Input.Locations], count);

    private static Creature[] CreaturesInEveryLocation(
        IReadOnlyList<Location> locations,
        int count
    ) =>
        locations
            .SelectMany(location =>
                Enumerable
                    .Range(0, count)
                    .Select(_ => new Creature
                    {
                        LocationId = location.Id,
                        WorldId = location.WorldId,
                    })
            )
            .ToArray();

    private static OrientedBox BodyAt(Creature creature) =>
        OrientedBox.From(
            new Placement(creature.X, creature.Y, creature.Angle),
            CreaturePlacementResolver.Body
        );

    private static OrientedBox PropBox(Prop prop) =>
        OrientedBox.From(
            new Placement(prop.X, prop.Y, prop.Angle),
            new Footprint(prop.Width, prop.Depth)
        );
}
