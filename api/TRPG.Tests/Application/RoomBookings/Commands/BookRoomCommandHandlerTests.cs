using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.RoomBookings.Commands;

[Collection("Database")]
public sealed class BookRoomCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private BookRoomCommandHandler _handler = null!;
    private Guid _lobbyLocationId;
    private Workstation _counter = null!;
    private DoorConnector _guestRoomDoor = null!;
    private Item _spareKey = null!;
    private DoorConnectorKey _doorConnectorKey = null!;
    private Creature _player = null!;
    private GameSession _session = null!;
    private Bed _bed = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<BookRoomCommandHandler>();

        _lobbyLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Inn);
        var lobby = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: _lobbyLocationId,
            name: "Lobby"
        );
        var lobbyLocation = Builders.MakeLocation(WorldId, roomId: lobby.Id, id: _lobbyLocationId);
        _counter = Builders.MakeWorkstation(worldId: WorldId, locationId: _lobbyLocationId);

        var guestRoom = Builders.MakeRoom(building.Id, worldId: WorldId, name: "North Guest Room");
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
        _guestRoomDoor = Builders.MakeDoorConnector(
            entryConnector.Id,
            isLocked: true,
            worldId: WorldId
        );
        _spareKey = Builders.MakeKey(
            WorldId,
            quantity: 1,
            ownerId: _counter.Id,
            ownerType: OwnerType.Workstation
        );
        _doorConnectorKey = Builders.MakeDoorConnectorKey(_spareKey.Id, _guestRoomDoor.Id, WorldId);
        _bed = Builders.MakeBed(WorldId, locationId: guestRoom.LocationId);

        _player = Builders.MakeCreature(worldId: WorldId, locationId: _lobbyLocationId);
        _session = Builders.MakeGameSession(WorldId, _player.Id, playtime: TimeSpan.FromHours(10));

        _context.Buildings.Add(building);
        _context.Rooms.AddRange(lobby, guestRoom);
        _context.Locations.AddRange(lobbyLocation, guestRoomLocation);
        _context.Props.AddRange(_counter, _bed);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(_guestRoomDoor);
        _context.Items.Add(_spareKey);
        _context.DoorConnectorKeys.Add(_doorConnectorKey);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_BooksTheRoomAndChargesGold_WhenARoomIsAvailableAndPlayerCanAfford()
    {
        // Arrange
        var gold = Builders.MakeGold(
            WorldId,
            quantity: 10,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new BookRoomCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Playtime = _session.Playtime,
                LocationId = _lobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Equal(BookRoomOutcome.Booked, result.Outcome);
        Assert.Equal("North Guest Room", result.RoomName);
        Assert.Equal(5, result.GoldCharged);

        var updatedKey = await verifyContext.Items.SingleAsync(
            i => i.Id == _spareKey.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(OwnerType.Creature, updatedKey.Ownership.OwnerType);
        Assert.Equal(_player.Id, updatedKey.Ownership.OwnerId);

        var updatedGold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                i => i.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(5, updatedGold.Quantity);

        var booking = await verifyContext.RoomBookings.SingleAsync(
            b => b.PlayerId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_spareKey.Id, booking.KeyItemId);
        Assert.Equal(
            _session.Playtime + GameClock.RealTimePerInGameHour * 24,
            booking.DueAtPlaytime
        );

        var updatedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == _bed.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_player.Id, updatedBed.AssignedCreatureId);
    }

    [Fact]
    public async Task Handle_ReturnsInsufficientGold_WhenPlayerCannotAffordTheRate()
    {
        // Arrange
        var gold = Builders.MakeGold(
            WorldId,
            quantity: 2,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new BookRoomCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Playtime = _session.Playtime,
                LocationId = _lobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(BookRoomOutcome.InsufficientGold, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsNoVacancy_WhenNoSpareKeyIsAvailable()
    {
        // Arrange
        _context.DoorConnectorKeys.Remove(_doorConnectorKey);
        _context.Items.Remove(_spareKey);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new BookRoomCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Playtime = _session.Playtime,
                LocationId = _lobbyLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(BookRoomOutcome.NoVacancy, result.Outcome);
    }

    [Fact]
    public async Task Handle_ChargesNoGoldAndCreatesNoBooking_WhenTheGuestRoomHasNoBed()
    {
        // Arrange
        var gold = Builders.MakeGold(
            WorldId,
            quantity: 10,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        _context.Items.Add(gold);
        _context.Props.Remove(_bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new BookRoomCommand
                {
                    PlayerId = _player.Id,
                    WorldId = WorldId,
                    Playtime = _session.Playtime,
                    LocationId = _lobbyLocationId,
                },
                TestContext.Current.CancellationToken
            )
        );

        await using var verifyContext = db.CreateContext();
        var unchangedGold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                i => i.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(10, unchangedGold.Quantity);

        Assert.False(
            await verifyContext.RoomBookings.AnyAsync(
                b => b.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );

        var unchangedKey = await verifyContext.Items.SingleAsync(
            i => i.Id == _spareKey.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(OwnerType.Workstation, unchangedKey.Ownership.OwnerType);
    }

    [Fact]
    public async Task Handle_RollsBackTheGoldCharge_WhenTheKeyTransferFailsAfterPaymentIsTaken()
    {
        // Arrange — the key still passes the vacancy check (it's still workstation-owned) but has
        // no quantity left, so the transfer fails only after gold has already changed hands
        var gold = Builders.MakeGold(
            WorldId,
            quantity: 10,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        _context.Items.Add(gold);
        _spareKey.Quantity = 0;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new BookRoomCommand
                {
                    PlayerId = _player.Id,
                    WorldId = WorldId,
                    Playtime = _session.Playtime,
                    LocationId = _lobbyLocationId,
                },
                TestContext.Current.CancellationToken
            )
        );

        await using var verifyContext = db.CreateContext();
        var unchangedGold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                i => i.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(10, unchangedGold.Quantity);

        Assert.False(
            await verifyContext.RoomBookings.AnyAsync(
                b => b.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );

        var unchangedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == _bed.Id, TestContext.Current.CancellationToken);
        Assert.Null(unchangedBed.AssignedCreatureId);
    }
}
