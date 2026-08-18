using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Scenes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Commands;

[Collection("Database")]
public sealed class SyncScheduleLockCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private AddCreatureCommandHandler _addCreature = null!;
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncScheduleLockCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _addJob = _serviceProvider.GetRequiredService<AddCreatureJobCommandHandler>();
        _addCreature = _serviceProvider.GetRequiredService<AddCreatureCommandHandler>();
        _addBuildingOwner = _serviceProvider.GetRequiredService<AddBuildingOwnerCommandHandler>();
        _handler = _serviceProvider.GetRequiredService<SyncScheduleLockCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private Task AddJob(CreatureJob job) =>
        _addJob.Handle(
            new AddCreatureJobCommand { CreatureJob = job },
            TestContext.Current.CancellationToken
        );

    private Task<DoorConnector> GetFrontDoor(Guid doorConnectorId) =>
        _context
            .DoorConnectors.AsNoTracking()
            .FirstAsync(c => c.Id == doorConnectorId, TestContext.Current.CancellationToken);

    private Task<Guid> GetOriginLocationId(Guid connectorId) =>
        _context
            .LocationConnectors.Where(connector => connector.Id == connectorId)
            .Select(connector => connector.OriginLocationId)
            .FirstAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_Locks_DuringSleepHours()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            )
        );
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Work,
                startHour: 8,
                endHour: 20,
                priority: 50
            )
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = Builders.MakeInGameDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await GetFrontDoor(frontDoor.Id);
        Assert.True(door.IsLocked);
    }

    [Fact]
    public async Task Handle_Unlocks_DuringWorkHours()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            )
        );
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Work,
                startHour: 8,
                endHour: 20,
                priority: 50
            )
        );
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = Builders.MakeInGameDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = Builders.MakeInGameDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await GetFrontDoor(frontDoor.Id);
        Assert.False(door.IsLocked);
    }

    [Fact]
    public async Task Handle_NeverLocks_InnOrTavern()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await AddJob(
            Builders.MakeCreatureJob(
                owner.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            )
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = BuildingType.Tavern,
                CurrentDate = Builders.MakeInGameDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await GetFrontDoor(frontDoor.Id);
        Assert.False(door.IsLocked);
    }

    [Fact]
    public async Task Handle_LocksShop_WhenNoWorkerIsOnShift()
    {
        // Arrange
        var worker = await SeedOwner();
        var shop = await SeedBuilding(worker.Id, BuildingType.Bakery);
        var frontDoor = await SeedFrontDoor(shop.Id);
        var workLocationId = await GetOriginLocationId(frontDoor.ConnectorId);
        await AddJob(
            Builders.MakeCreatureJob(
                worker.Id,
                action: CreatureJobAction.Work,
                startHour: 6,
                endHour: 14,
                priority: 50,
                locationId: workLocationId
            )
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = Builders.MakeInGameDate(16),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await GetFrontDoor(frontDoor.Id);
        Assert.True(door.IsLocked);
    }

    [Fact]
    public async Task Handle_UnlocksShop_WhenAWorkerIsOnShift()
    {
        // Arrange
        var worker = await SeedOwner();
        var shop = await SeedBuilding(worker.Id, BuildingType.Bakery);
        var frontDoor = await SeedFrontDoor(shop.Id);
        var workLocationId = await GetOriginLocationId(frontDoor.ConnectorId);
        await AddJob(
            Builders.MakeCreatureJob(
                worker.Id,
                action: CreatureJobAction.Work,
                startHour: 6,
                endHour: 14,
                priority: 50,
                locationId: workLocationId
            )
        );
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = Builders.MakeInGameDate(16),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = Builders.MakeInGameDate(10),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await GetFrontDoor(frontDoor.Id);
        Assert.False(door.IsLocked);
    }

    [Fact]
    public async Task Handle_LocksShop_WhenEveryWorkerIsOnADayOff()
    {
        // Arrange - the Work window covers this hour, but a higher-priority day-off job overrides it
        var worker = await SeedOwner();
        var shop = await SeedBuilding(worker.Id, BuildingType.Bakery);
        var frontDoor = await SeedFrontDoor(shop.Id);
        var workLocationId = await GetOriginLocationId(frontDoor.ConnectorId);
        await AddJob(
            Builders.MakeCreatureJob(
                worker.Id,
                action: CreatureJobAction.Work,
                startHour: 8,
                endHour: 18,
                priority: 50,
                locationId: workLocationId
            )
        );
        await AddJob(
            Builders.MakeCreatureJob(
                worker.Id,
                action: CreatureJobAction.Idle,
                startHour: 8,
                endHour: 18,
                priority: 60,
                specificDay: DayOfWeek.Thursday
            )
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = Builders.MakeInGameDate(10),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await GetFrontDoor(frontDoor.Id);
        Assert.True(door.IsLocked);
    }

    private async Task<Creature> SeedOwner()
    {
        var owner = Builders.MakeCreature(WorldId);
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = owner },
            TestContext.Current.CancellationToken
        );
        return owner;
    }

    private async Task<Building> SeedBuilding(
        Guid ownerId,
        BuildingType buildingType = BuildingType.House
    )
    {
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: buildingType);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = building.Id, OwnerId = ownerId },
            TestContext.Current.CancellationToken
        );
        return building;
    }

    private async Task<DoorConnector> SeedFrontDoor(Guid buildingId)
    {
        var entranceRoom = Builders.MakeRoom(buildingId, worldId: WorldId);
        var outsideLocation = Builders.MakeLocation(WorldId);
        var frontDoor = Builders.MakeLocationConnector(
            entranceRoom.LocationId,
            destinationLocationId: outsideLocation.Id,
            worldId: WorldId,
            name: "Front Door",
            description: "The door leading outside."
        );
        var door = Builders.MakeDoorConnector(frontDoor.Id, worldId: WorldId);
        _context.Rooms.Add(entranceRoom);
        _context.Locations.Add(outsideLocation);
        _context.LocationConnectors.Add(frontDoor);
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return door;
    }
}
