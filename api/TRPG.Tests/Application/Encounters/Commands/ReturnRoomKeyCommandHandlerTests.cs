using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class ReturnRoomKeyCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ReturnRoomKeyCommandHandler _handler = null!;
    private Guid _lobbyLocationId;
    private Guid _guestRoomId;
    private Workstation _counter = null!;
    private Creature _innkeeper = null!;
    private Creature _player = null!;
    private GameSession _session = null!;
    private Item _key = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ReturnRoomKeyCommandHandler>();

        _lobbyLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Inn);
        var lobby = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: _lobbyLocationId,
            name: "Lobby"
        );
        var lobbyLocation = Builders.MakeLocation(WorldId, roomId: lobby.Id, id: _lobbyLocationId);

        _innkeeper = Builders.MakeCreature(worldId: WorldId, locationId: _lobbyLocationId);
        _counter = Builders.MakeWorkstation(
            worldId: WorldId,
            locationId: _lobbyLocationId,
            ownerCreatureId: _innkeeper.Id
        );

        var guestRoom = Builders.MakeRoom(building.Id, worldId: WorldId, name: "North Guest Room");
        _guestRoomId = guestRoom.Id;
        var guestRoomLocation = Builders.MakeLocation(
            WorldId,
            roomId: guestRoom.Id,
            id: guestRoom.LocationId
        );
        var entryConnector = Builders.MakeLocationConnector(
            _lobbyLocationId,
            destinationLocationId: guestRoom.LocationId,
            worldId: WorldId
        );
        var door = Builders.MakeDoorConnector(entryConnector.Id, isLocked: true, worldId: WorldId);

        _player = Builders.MakeCreature(worldId: WorldId, locationId: _lobbyLocationId);
        _key = Builders.MakeKey(
            WorldId,
            quantity: 1,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        var doorConnectorKey = Builders.MakeDoorConnectorKey(_key.Id, door.Id, WorldId);
        _session = Builders.MakeGameSession(WorldId, _player.Id, playtime: TimeSpan.FromHours(10));

        _context.Buildings.Add(building);
        _context.Rooms.AddRange(lobby, guestRoom);
        _context.Locations.AddRange(lobbyLocation, guestRoomLocation);
        _context.Creatures.AddRange(_innkeeper, _player);
        _context.Props.Add(_counter);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(door);
        _context.Items.Add(_key);
        _context.DoorConnectorKeys.Add(doorConnectorKey);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheKeyToTheWorkstation_WhenNotOverdue()
    {
        // Arrange
        var booking = Builders.MakeRoomBooking(
            WorldId,
            _guestRoomId,
            _key.Id,
            _player.Id,
            dueAtPlaytime: TimeSpan.FromHours(20)
        );
        _context.RoomBookings.Add(booking);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ReturnRoomKeyCommand
            {
                WorldId = WorldId,
                Playtime = _session.Playtime,
                PlayerId = _player.Id,
                LocationId = _lobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Equal(ReturnRoomKeyOutcome.Returned, result.Outcome);
        Assert.Null(result.Encounter);

        var updatedKey = await verifyContext.Items.SingleAsync(
            i => i.Id == _key.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(OwnerType.Workstation, updatedKey.Ownership.OwnerType);
        Assert.Equal(_counter.Id, updatedKey.Ownership.OwnerId);

        Assert.False(
            await verifyContext.RoomBookings.AnyAsync(
                b => b.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ReturnsNoActiveBooking_WhenPlayerHasNoBookingAtThisInn()
    {
        // Act
        var result = await _handler.Handle(
            new ReturnRoomKeyCommand
            {
                WorldId = WorldId,
                Playtime = _session.Playtime,
                PlayerId = _player.Id,
                LocationId = _lobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ReturnRoomKeyOutcome.NoActiveBooking, result.Outcome);
    }

    [Fact]
    public async Task Handle_ConfrontsThePlayerInstead_WhenTheKeyIsOverdue()
    {
        // Arrange
        var booking = Builders.MakeRoomBooking(
            WorldId,
            _guestRoomId,
            _key.Id,
            _player.Id,
            dueAtPlaytime: TimeSpan.FromHours(5)
        );
        _context.RoomBookings.Add(booking);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ReturnRoomKeyCommand
            {
                WorldId = WorldId,
                Playtime = _session.Playtime,
                PlayerId = _player.Id,
                LocationId = _lobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Equal(ReturnRoomKeyOutcome.Overdue, result.Outcome);
        Assert.NotNull(result.Encounter);
        Assert.Equal(_innkeeper.Id, result.Encounter.ConfrontingCreatureId);
        Assert.Contains(_key.Id, result.Encounter.ItemIds);

        var updatedKey = await verifyContext.Items.SingleAsync(
            i => i.Id == _key.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(OwnerType.Creature, updatedKey.Ownership.OwnerType);
        Assert.Equal(_player.Id, updatedKey.Ownership.OwnerId);

        Assert.False(
            await verifyContext.RoomBookings.AnyAsync(
                b => b.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
