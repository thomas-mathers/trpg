using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

[Collection("Database")]
public sealed class SyncRestockPolicyCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncRestockPolicyCommandHandler _handler = null!;
    private readonly Guid _locationId = Guid.NewGuid();
    private Workstation _workstation = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncRestockPolicyCommandHandler>();

        var building = Builders.MakeBuilding(
            worldId: WorldId,
            buildingType: BuildingType.Apothecary
        );
        var room = Builders.MakeRoom(building.Id, worldId: WorldId, locationId: _locationId);
        var location = Builders.MakeLocation(WorldId, roomId: room.Id, id: _locationId);
        _workstation = Builders.MakeWorkstation(worldId: WorldId, locationId: _locationId);

        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Props.Add(_workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoRestockPolicyExistsForTheWorkstation()
    {
        // Act
        await _handler.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = _locationId,
                PlayerLevel = 5,
                CurrentPlaytime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Items.Where(i => i.Ownership.OwnerId == _workstation.Id)
                .AnyAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_StocksTheWorkstation_WhenScheduleHasTriggered()
    {
        // Arrange
        var policy = Builders.MakeRestockPolicy(WorldId, _workstation.Id);
        _context.RestockPolicies.Add(policy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — a full in-game day has passed, enough to trigger a daily-at-hour-0 schedule
        await _handler.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = _locationId,
                PlayerLevel = 5,
                CurrentPlaytime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert — Apothecary's stock is Gold plus three potion stacks
        await using var verifyContext = db.CreateContext();
        var items = await verifyContext
            .Items.Where(i => i.Ownership.OwnerId == _workstation.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(items.OfType<Gold>());
        Assert.Equal(3, items.OfType<Consumable>().Count());
    }

    [Fact]
    public async Task Handle_TopsUpAnExistingDepletedPotionStack_RatherThanDuplicatingIt()
    {
        // Arrange
        var policy = Builders.MakeRestockPolicy(WorldId, _workstation.Id);
        var depletedPotion = new Consumable
        {
            WorldId = WorldId,
            Resource = ResourceType.Hp,
            Quantity = 1,
            Ownership = new ItemOwnership
            {
                OwnerId = _workstation.Id,
                OwnerType = OwnerType.Workstation,
            },
        };
        _context.RestockPolicies.Add(policy);
        _context.Items.Add(depletedPotion);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = _locationId,
                PlayerLevel = 5,
                CurrentPlaytime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var hpPotions = await verifyContext
            .Items.OfType<Consumable>()
            .Where(c => c.Ownership.OwnerId == _workstation.Id && c.Resource == ResourceType.Hp)
            .ToListAsync(TestContext.Current.CancellationToken);
        var potion = Assert.Single(hpPotions);
        Assert.Equal(10, potion.Quantity);
    }

    [Fact]
    public async Task Handle_DoesNotRestock_WhenScheduleHasNotYetTriggered()
    {
        // Arrange
        var policy = Builders.MakeRestockPolicy(WorldId, _workstation.Id);
        _context.RestockPolicies.Add(policy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — only half an in-game day has passed
        await _handler.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = _locationId,
                PlayerLevel = 5,
                CurrentPlaytime = TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Items.Where(i => i.Ownership.OwnerId == _workstation.Id)
                .AnyAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_RegeneratesAMissingRoomKey_ForAnInnWorkstation_WhenScheduleHasTriggered()
    {
        // Arrange
        var innLocationId = Guid.NewGuid();
        var innBuilding = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Inn);
        var lobby = Builders.MakeRoom(
            innBuilding.Id,
            worldId: WorldId,
            locationId: innLocationId,
            name: "Lobby"
        );
        var lobbyLocation = Builders.MakeLocation(WorldId, roomId: lobby.Id, id: innLocationId);
        var counter = Builders.MakeWorkstation(worldId: WorldId, locationId: innLocationId);

        var guestRoom = Builders.MakeRoom(
            innBuilding.Id,
            worldId: WorldId,
            name: "North Guest Room"
        );
        var guestRoomLocation = Builders.MakeLocation(
            WorldId,
            roomId: guestRoom.Id,
            id: guestRoom.LocationId
        );
        var entryConnector = Builders.MakeLocationConnector(
            innLocationId,
            destinationLocationId: guestRoom.LocationId,
            worldId: WorldId
        );
        var door = Builders.MakeDoorConnector(entryConnector.Id, isLocked: true, worldId: WorldId);

        var policy = Builders.MakeRestockPolicy(WorldId, counter.Id);

        _context.Buildings.Add(innBuilding);
        _context.Rooms.AddRange(lobby, guestRoom);
        _context.Locations.AddRange(lobbyLocation, guestRoomLocation);
        _context.Props.Add(counter);
        _context.LocationConnectors.Add(entryConnector);
        _context.DoorConnectors.Add(door);
        _context.RestockPolicies.Add(policy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — a full in-game day has passed, enough to trigger a daily-at-hour-0 schedule
        await _handler.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = innLocationId,
                PlayerLevel = 5,
                CurrentPlaytime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var doorConnectorKey = await verifyContext.DoorConnectorKeys.SingleAsync(
            k => k.DoorConnectorId == door.Id,
            TestContext.Current.CancellationToken
        );
        var key = await verifyContext.Items.SingleAsync(
            i => i.Id == doorConnectorKey.ItemId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(OwnerType.Workstation, key.Ownership.OwnerType);
        Assert.Equal(counter.Id, key.Ownership.OwnerId);
    }

    [Fact]
    public async Task Handle_AdvancesLastSyncPlaytime_AfterRestocking()
    {
        // Arrange
        var policy = Builders.MakeRestockPolicy(WorldId, _workstation.Id);
        _context.RestockPolicies.Add(policy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var currentPlaytime = TimeSpan.FromHours(2);

        // Act
        await _handler.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = _locationId,
                PlayerLevel = 5,
                CurrentPlaytime = currentPlaytime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedPolicy = await verifyContext.RestockPolicies.SingleAsync(
            p => p.Id == policy.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(currentPlaytime, updatedPolicy.LastSyncPlaytime);
    }
}
