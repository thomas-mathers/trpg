using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Jobs.Commands;
using TRPG.Application.Jobs.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Commands;

[Collection("Database")]
public sealed class SyncScheduleLockCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly Guid _worldId = Guid.NewGuid();
    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private AddCreatureCommandHandler _addCreature = null!;
    private AddJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private GetFrontDoorQueryHandler _getFrontDoor = null!;
    private SyncScheduleLockCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddJobCommandHandler(_context);
        _addCreature = new AddCreatureCommandHandler(_context);
        _addBuildingOwner = new AddBuildingOwnerCommandHandler(_context);
        _getFrontDoor = new GetFrontDoorQueryHandler(_context);
        _handler = new SyncScheduleLockCommandHandler(
            new GetAllOwnersByBuildingIdQueryHandler(_context),
            new GetAllJobsByCreatureIdQueryHandler(_context),
            new GetJobsOfBuildingWorkersQueryHandler(_context),
            new SetFrontDoorLockedCommandHandler(_context)
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private static InGameDate MakeDate(int hour) =>
        new(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, hour);

    [Fact]
    public async Task Handle_Locks_DuringSleepHours()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    owner.Id,
                    action: JobAction.Sleep,
                    startHour: 22,
                    endHour: 6,
                    priority: 100
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    owner.Id,
                    action: JobAction.Work,
                    startHour: 8,
                    endHour: 20,
                    priority: 50
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = MakeDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = frontDoor.RoomId },
            TestContext.Current.CancellationToken
        );
        Assert.True(door!.IsLocked);
    }

    [Fact]
    public async Task Handle_Unlocks_DuringWorkHours()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    owner.Id,
                    action: JobAction.Sleep,
                    startHour: 22,
                    endHour: 6,
                    priority: 100
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    owner.Id,
                    action: JobAction.Work,
                    startHour: 8,
                    endHour: 20,
                    priority: 50
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = MakeDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = frontDoor.RoomId },
            TestContext.Current.CancellationToken
        );
        Assert.False(door!.IsLocked);
    }

    [Fact]
    public async Task Handle_NeverLocks_InnOrTavern()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    owner.Id,
                    action: JobAction.Sleep,
                    startHour: 22,
                    endHour: 6,
                    priority: 100
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = BuildingType.Tavern,
                CurrentDate = MakeDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = frontDoor.RoomId },
            TestContext.Current.CancellationToken
        );
        Assert.False(door!.IsLocked);
    }

    [Fact]
    public async Task Handle_LocksShop_WhenNoWorkerIsOnShift()
    {
        // Arrange
        var worker = await SeedOwner();
        var shop = await SeedBuilding(worker.Id, BuildingType.Bakery);
        var frontDoor = await SeedFrontDoor(shop.Id);
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    worker.Id,
                    action: JobAction.Work,
                    startHour: 6,
                    endHour: 14,
                    priority: 50,
                    roomId: frontDoor.RoomId
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = MakeDate(16),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = frontDoor.RoomId },
            TestContext.Current.CancellationToken
        );
        Assert.True(door!.IsLocked);
    }

    [Fact]
    public async Task Handle_UnlocksShop_WhenAWorkerIsOnShift()
    {
        // Arrange
        var worker = await SeedOwner();
        var shop = await SeedBuilding(worker.Id, BuildingType.Bakery);
        var frontDoor = await SeedFrontDoor(shop.Id);
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    worker.Id,
                    action: JobAction.Work,
                    startHour: 6,
                    endHour: 14,
                    priority: 50,
                    roomId: frontDoor.RoomId
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = MakeDate(16),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = MakeDate(10),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = frontDoor.RoomId },
            TestContext.Current.CancellationToken
        );
        Assert.False(door!.IsLocked);
    }

    [Fact]
    public async Task Handle_LocksShop_WhenEveryWorkerIsOnADayOff()
    {
        // Arrange — the Work window covers this hour, but a higher-priority day-off job overrides it
        var worker = await SeedOwner();
        var shop = await SeedBuilding(worker.Id, BuildingType.Bakery);
        var frontDoor = await SeedFrontDoor(shop.Id);
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    worker.Id,
                    action: JobAction.Work,
                    startHour: 8,
                    endHour: 18,
                    priority: 50,
                    roomId: frontDoor.RoomId
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddJobCommand
            {
                Job = Builders.MakeJob(
                    worker.Id,
                    action: JobAction.Idle,
                    startHour: 8,
                    endHour: 18,
                    priority: 60,
                    specificDay: DayOfWeek.Thursday
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = shop.Id,
                BuildingType = shop.BuildingType,
                CurrentDate = MakeDate(10),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = frontDoor.RoomId },
            TestContext.Current.CancellationToken
        );
        Assert.True(door!.IsLocked);
    }

    private async Task<Creature> SeedOwner()
    {
        var owner = Builders.MakeCreature(_worldId);
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
        var building = Builders.MakeBuilding(
            Guid.NewGuid(),
            worldId: _worldId,
            buildingType: buildingType
        );
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _addBuildingOwner.Handle(
            new AddBuildingOwnerCommand { BuildingId = building.Id, OwnerId = ownerId },
            TestContext.Current.CancellationToken
        );
        return building;
    }

    private async Task<RoomConnector> SeedFrontDoor(Guid buildingId)
    {
        var entranceRoom = Builders.MakeRoom(buildingId, worldId: _worldId);
        var frontDoor = new RoomConnector
        {
            RoomId = entranceRoom.Id,
            WorldId = _worldId,
            Name = "Front Door",
            Description = "The door leading outside.",
            DestinationRoomId = null,
            IsLocked = false,
        };
        _context.Rooms.Add(entranceRoom);
        _context.Props.Add(frontDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return frontDoor;
    }
}
