using Microsoft.Extensions.DependencyInjection;
using TRPG.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Creatures.Queries;

public sealed class GetLocalMapQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetLocalMapQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetLocalMapQueryHandler>();

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsExploredRoomsAndTheirImmediateFrontier()
    {
        var building = Builders.MakeBuilding(
            worldId: _worldId,
            name: "The Ashen Reliquary",
            buildingType: BuildingType.Crypt
        );
        var visitedRoom = MakeRoom(building.Id, "Weathered Threshold", new Point(0, 0));
        var currentRoom = MakeRoom(
            building.Id,
            "Flooded Antechamber",
            new Point(10, 0),
            RoomRole.FloodedSump
        );
        var frontierRoom = MakeRoom(
            building.Id,
            "Drowned Shrine",
            new Point(20, 0),
            RoomRole.Shrine
        );
        var hiddenRoom = MakeRoom(
            building.Id,
            "Reliquary Vault",
            new Point(30, 0),
            RoomRole.BossChamber
        );
        var player = Builders.MakeCreature(_worldId, locationId: currentRoom.LocationId);
        var visitedToCurrent = Builders.MakeLocationConnector(
            visitedRoom.LocationId,
            currentRoom.LocationId,
            _worldId
        );
        var currentToVisited = Builders.MakeLocationConnector(
            currentRoom.LocationId,
            visitedRoom.LocationId,
            _worldId
        );
        var currentToFrontier = Builders.MakeLocationConnector(
            currentRoom.LocationId,
            frontierRoom.LocationId,
            _worldId
        );
        var frontierToCurrent = Builders.MakeLocationConnector(
            frontierRoom.LocationId,
            currentRoom.LocationId,
            _worldId
        );
        var frontierToHidden = Builders.MakeLocationConnector(
            frontierRoom.LocationId,
            hiddenRoom.LocationId,
            _worldId
        );
        var lockedDoor = Builders.MakeDoorConnector(
            currentToFrontier.Id,
            isLocked: true,
            worldId: _worldId
        );

        _context.Buildings.Add(building);
        _context.Rooms.AddRange(visitedRoom, currentRoom, frontierRoom, hiddenRoom);
        _context.Locations.AddRange(
            MakeLocation(visitedRoom),
            MakeLocation(currentRoom),
            MakeLocation(frontierRoom),
            MakeLocation(hiddenRoom)
        );
        _context.Creatures.Add(player);
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                WorldId = _worldId,
                KnowerId = player.Id,
                SubjectId = visitedRoom.LocationId,
                SubjectType = KnowledgeSubjectType.Room,
            }
        );
        _context.LocationConnectors.AddRange(
            visitedToCurrent,
            currentToVisited,
            currentToFrontier,
            frontierToCurrent,
            frontierToHidden
        );
        _context.DoorConnectors.Add(lockedDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var map = await _handler.Handle(
            new GetLocalMapQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(building.Id, map.BuildingId);
        Assert.Equal(currentRoom.Id, map.CurrentRoomId);
        Assert.DoesNotContain(map.Rooms, room => room.Id == hiddenRoom.Id);
        Assert.True(Assert.Single(map.Rooms, room => room.Id == currentRoom.Id).IsVisited);
        var frontier = Assert.Single(map.Rooms, room => room.Id == frontierRoom.Id);
        Assert.True(frontier.IsFrontier);
        Assert.Equal(RoomRole.Shrine, frontier.Role);
        Assert.Equal(new Rectangle(10, -5, 30, 5), frontier.Bounds);
        Assert.Equal(2, map.Passages.Count);
        Assert.Contains(
            map.Passages,
            passage =>
                passage.IsLocked
                && (
                    passage.OriginRoomId == currentRoom.Id
                    || passage.DestinationRoomId == currentRoom.Id
                )
                && (
                    passage.OriginRoomId == frontierRoom.Id
                    || passage.DestinationRoomId == frontierRoom.Id
                )
        );
    }

    [Fact]
    public async Task Handle_UsesReverseGeometryAndPreservesLock_WhenForwardPathIsEmpty()
    {
        // Arrange
        var building = Builders.MakeBuilding(worldId: _worldId);
        var current = MakeRoom(building.Id, "Entry", new Point(0, 0));
        var next = MakeRoom(building.Id, "Study", new Point(40, 0));
        var player = Builders.MakeCreature(_worldId, locationId: current.LocationId);
        var forward = MapConnector(current, next, new Polyline());
        var reverse = MapConnector(
            next,
            current,
            new Polyline { Points = [new Point(30, 0), new Point(10, 0)] }
        );
        _context.Buildings.Add(building);
        _context.Rooms.AddRange(current, next);
        _context.Locations.AddRange(MakeLocation(current), MakeLocation(next));
        _context.Creatures.Add(player);
        _context.LocationConnectors.AddRange(forward, reverse);
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(forward.Id, isLocked: true, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLocalMapQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var passage = Assert.Single(result.Passages);
        Assert.Equal(next.Id, passage.OriginRoomId);
        Assert.Equal(current.Id, passage.DestinationRoomId);
        Assert.Equal(reverse.Path!.Points, passage.Path!.Points);
        Assert.True(passage.IsLocked);
    }

    private LocationConnector MapConnector(Room origin, Room destination, Polyline path) =>
        new()
        {
            OriginLocationId = origin.LocationId,
            DestinationLocationId = destination.LocationId,
            DestinationLabel = destination.Name,
            Path = path,
            WorldId = _worldId,
        };

    private Room MakeRoom(Guid buildingId, string name, Point position, RoomRole? role = null) =>
        new()
        {
            BuildingId = buildingId,
            Bounds = new Rectangle(
                (int)position.X - 10,
                (int)position.Y - 5,
                (int)position.X + 10,
                (int)position.Y + 5
            ),
            Capacity = 4,
            Description = $"The {name}.",
            FloorNumber = 0,
            LocationId = Guid.NewGuid(),
            Name = name,
            Role = role,
            WorldId = _worldId,
        };

    private Location MakeLocation(Room room) =>
        Builders.MakeLocation(
            _worldId,
            roomId: room.Id,
            id: room.LocationId,
            kind: LocationKind.Room
        );
}
