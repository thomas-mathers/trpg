using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class ConfrontOverdueRoomKeyOnMoveCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ConfrontOverdueRoomKeyOnMoveCommandHandler _handler = null!;
    private OverdueInnFixture _inn = null!;
    private Location _outside = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ConfrontOverdueRoomKeyOnMoveCommandHandler>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);
        _outside = Builders.MakeLocation(WorldId, _stateId);
        _context.States.Add(state);
        _context.Locations.Add(_outside);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _inn = await SeedOverdueInnBooking(_player.Id);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ConfrontsThePlayer_WhenLeavingTheInnWithAnOverdueRoomKey()
    {
        // Act
        var result = await _handler.Handle(
            new ConfrontOverdueRoomKeyOnMoveCommand
            {
                WorldId = WorldId,
                Playtime = TimeSpan.Zero,
                PlayerId = _player.Id,
                FromLocationId = _inn.GuestRoomLocationId,
                ToLocationId = _outside.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var encounter = Assert.IsType<TheftEncounter>(result.Encounter);
        Assert.Equal(_inn.InnkeeperId, encounter.ConfrontingCreatureId);
    }

    [Fact]
    public async Task Handle_ConfrontsThePlayer_WhenReturningToTheInnAfterTheKeyBecameOverdue()
    {
        // Act
        var result = await _handler.Handle(
            new ConfrontOverdueRoomKeyOnMoveCommand
            {
                WorldId = WorldId,
                Playtime = TimeSpan.Zero,
                PlayerId = _player.Id,
                FromLocationId = _outside.Id,
                ToLocationId = _inn.LobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var encounter = Assert.IsType<TheftEncounter>(result.Encounter);
        Assert.Equal(_inn.InnkeeperId, encounter.ConfrontingCreatureId);
    }

    [Fact]
    public async Task Handle_DoesNotConfrontThePlayer_WhenMovingBetweenRoomsInsideTheSameInn()
    {
        // Act
        var result = await _handler.Handle(
            new ConfrontOverdueRoomKeyOnMoveCommand
            {
                WorldId = WorldId,
                Playtime = TimeSpan.Zero,
                PlayerId = _player.Id,
                FromLocationId = _inn.GuestRoomLocationId,
                ToLocationId = _inn.LobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result.Encounter);
    }

    private async Task<OverdueInnFixture> SeedOverdueInnBooking(Guid playerId)
    {
        var lobbyLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Inn);
        var lobby = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: lobbyLocationId,
            name: "Lobby"
        );
        var lobbyLocation = Builders.MakeLocation(
            WorldId,
            _stateId,
            roomId: lobby.Id,
            id: lobbyLocationId
        );

        var innkeeper = Builders.MakeCreature(worldId: WorldId, locationId: lobbyLocationId);
        var counter = Builders.MakeWorkstation(
            worldId: WorldId,
            locationId: lobbyLocationId,
            ownerCreatureId: innkeeper.Id
        );

        var guestRoom = Builders.MakeRoom(building.Id, worldId: WorldId, name: "North Guest Room");
        var guestRoomLocation = Builders.MakeLocation(
            WorldId,
            _stateId,
            roomId: guestRoom.Id,
            id: guestRoom.LocationId
        );
        var entryConnector = Builders.MakeLocationConnector(
            lobbyLocationId,
            destinationLocationId: guestRoom.LocationId,
            worldId: WorldId
        );
        var door = Builders.MakeDoorConnector(entryConnector.Id, isLocked: true, worldId: WorldId);

        var key = Builders.MakeKey(
            WorldId,
            quantity: 1,
            ownerId: playerId,
            ownerType: OwnerType.Creature
        );
        var doorConnectorKey = Builders.MakeDoorConnectorKey(key.Id, door.Id, WorldId);
        var booking = Builders.MakeRoomBooking(
            WorldId,
            guestRoom.Id,
            key.Id,
            playerId,
            dueAtPlaytime: TimeSpan.Zero
        );

        _context.Buildings.Add(building);
        _context.Rooms.AddRange(lobby, guestRoom);
        _context.Locations.AddRange(lobbyLocation, guestRoomLocation);
        _context.Creatures.Add(innkeeper);
        _context.Props.Add(counter);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(door);
        _context.Items.Add(key);
        _context.DoorConnectorKeys.Add(doorConnectorKey);
        _context.RoomBookings.Add(booking);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new OverdueInnFixture(lobbyLocationId, guestRoom.LocationId, innkeeper.Id);
    }

    private sealed record OverdueInnFixture(
        Guid LobbyLocationId,
        Guid GuestRoomLocationId,
        Guid InnkeeperId
    );
}
