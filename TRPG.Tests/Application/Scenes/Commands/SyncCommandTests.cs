using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Commands;

[Collection("Database")]
public sealed class SyncCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private AddBuildingOwnerCommandHandler _addBuildingOwner = null!;
    private AddCreatureCommandHandler _addCreature = null!;
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private GetWorkstationsByRoomIdQueryHandler _getWorkstationsByRoomId = null!;
    private SyncCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddCreatureJobCommandHandler(_context);
        _addCreature = new AddCreatureCommandHandler(_context);
        _addBuildingOwner = new AddBuildingOwnerCommandHandler(_context);
        _getWorkstationsByRoomId = new GetWorkstationsByRoomIdQueryHandler(_context);
        var executeJob = new ExecuteCreatureJobCommandHandler(
            new UpdateCreaturesCommandHandler(_context)
        );
        var getAllJobsByCreatureId = new GetAllCreatureJobsByCreatureIdQueryHandler(_context);
        var syncScheduleLock = new SyncScheduleLockCommandHandler(
            new GetAllOwnersByBuildingIdQueryHandler(_context),
            getAllJobsByCreatureId,
            new GetCreatureJobsOfBuildingWorkersQueryHandler(_context),
            new SetFrontDoorLockedCommandHandler(_context)
        );
        _handler = new SyncCommandHandler(
            new GetCreatureIdsWithCreatureJobInRoomQueryHandler(_context),
            getAllJobsByCreatureId,
            new GetCreatureIdsByDistrictQueryHandler(_context),
            new GetCreatureByIdQueryHandler(_context),
            executeJob,
            _getWorkstationsByRoomId,
            new SetWorkstationOccupantCommandHandler(_context),
            new GetRoomSummaryQueryHandler(_context, new MemoryCache(new MemoryCacheOptions())),
            syncScheduleLock,
            NullLogger<SyncCommandHandler>.Instance
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private static InGameDate MakeDate(int hour) =>
        new(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, hour);

    [Fact]
    public async Task Handle_MovesCreatureIntoRoom_WhenSleepJobActive()
    {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(WorldId);
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = creature },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    creature.Id,
                    action: CreatureJobAction.Sleep,
                    startHour: 22,
                    endHour: 6,
                    roomId: sleepRoomId,
                    priority: 100
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act — hour 23 falls inside the wraparound Sleep window
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                RoomId = sleepRoomId,
                DistrictId = null,
                CurrentDate = MakeDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(sleepRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task Handle_MovesCreatureOut_WhenHigherPriorityWorkJobActiveElsewhere()
    {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var workRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(WorldId, roomId: sleepRoomId);
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = creature },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    creature.Id,
                    action: CreatureJobAction.Sleep,
                    startHour: 22,
                    endHour: 6,
                    roomId: sleepRoomId,
                    priority: 100
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    creature.Id,
                    action: CreatureJobAction.Work,
                    startHour: 8,
                    endHour: 20,
                    roomId: workRoomId,
                    priority: 50
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act — hour 10 is inside Work, and the creature is discovered via their stale Sleep-room assignment
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                RoomId = sleepRoomId,
                DistrictId = null,
                CurrentDate = MakeDate(10),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(workRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoJobsTargetRoom()
    {
        // Arrange
        var creature = Builders.MakeCreature(WorldId);
        var originalRoomId = creature.RoomId;
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = creature },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                RoomId = Guid.NewGuid(),
                DistrictId = null,
                CurrentDate = MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task Handle_MovesCreatureOutdoors_WhenIdleJobActive()
    {
        // Arrange
        var districtId = Guid.NewGuid();
        var sleepRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(WorldId, districtId: districtId, roomId: sleepRoomId);
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = creature },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    creature.Id,
                    action: CreatureJobAction.Sleep,
                    startHour: 22,
                    endHour: 6,
                    roomId: sleepRoomId,
                    priority: 100
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    creature.Id,
                    action: CreatureJobAction.Idle,
                    startHour: 6,
                    endHour: 22,
                    roomId: null,
                    priority: 0
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act — hour 12 is inside Idle
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                RoomId = null,
                DistrictId = districtId,
                CurrentDate = MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(updated!.RoomId);
    }

    [Fact]
    public async Task Handle_AssignsBothWorkersToDifferentWorkstations_WhenTwoArePresent()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var counter = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = WorldId,
            Name = "Counter",
            Description = "A counter.",
            WorkstationType = WorkstationType.Trade,
        };
        var oven = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = WorldId,
            Name = "Oven",
            Description = "An oven.",
            WorkstationType = WorkstationType.Cooking,
        };
        _context.Props.AddRange(counter, oven);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var owner = Builders.MakeCreature(WorldId);
        var employee = Builders.MakeCreature(WorldId);
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = owner },
            TestContext.Current.CancellationToken
        );
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = employee },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    owner.Id,
                    action: CreatureJobAction.Work,
                    startHour: 8,
                    endHour: 20,
                    roomId: shopRoomId,
                    priority: 50
                ),
            },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    employee.Id,
                    action: CreatureJobAction.Work,
                    startHour: 8,
                    endHour: 20,
                    roomId: shopRoomId,
                    priority: 50
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                RoomId = shopRoomId,
                DistrictId = null,
                CurrentDate = MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert — both workstations get staffed, by two different people
        var workstations = await _getWorkstationsByRoomId.Handle(
            new GetWorkstationsByRoomIdQuery { RoomId = shopRoomId },
            TestContext.Current.CancellationToken
        );
        var updatedCounter = workstations.First(w => w.Id == counter.Id);
        var updatedOven = workstations.First(w => w.Id == oven.Id);
        Assert.NotNull(updatedCounter.OccupantId);
        Assert.NotNull(updatedOven.OccupantId);
        Assert.NotEqual(updatedCounter.OccupantId, updatedOven.OccupantId);
        Assert.Contains(updatedCounter.OccupantId!.Value, new[] { owner.Id, employee.Id });
        Assert.Contains(updatedOven.OccupantId!.Value, new[] { owner.Id, employee.Id });
    }

    [Fact]
    public async Task Handle_GivesCounterPriority_AndClearsUnstaffedStation_WhenOnlyOneWorkerPresent()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var counter = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = WorldId,
            Name = "Counter",
            Description = "A counter.",
            WorkstationType = WorkstationType.Trade,
        };
        var oven = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = WorldId,
            Name = "Oven",
            Description = "An oven.",
            WorkstationType = WorkstationType.Cooking,
            OccupantId = Guid.NewGuid(), // stale, from a prior day
        };
        _context.Props.AddRange(counter, oven);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var owner = Builders.MakeCreature(WorldId);
        await _addCreature.Handle(
            new AddCreatureCommand { Creature = owner },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    owner.Id,
                    action: CreatureJobAction.Work,
                    startHour: 8,
                    endHour: 20,
                    roomId: shopRoomId,
                    priority: 50
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                RoomId = shopRoomId,
                DistrictId = null,
                CurrentDate = MakeDate(12),
            },
            TestContext.Current.CancellationToken
        );

        // Assert — the lone worker always gets the counter, and the unstaffed production station is cleared
        var workstations = await _getWorkstationsByRoomId.Handle(
            new GetWorkstationsByRoomIdQuery { RoomId = shopRoomId },
            TestContext.Current.CancellationToken
        );
        var updatedCounter = workstations.First(w => w.Id == counter.Id);
        var updatedOven = workstations.First(w => w.Id == oven.Id);
        Assert.Equal(owner.Id, updatedCounter.OccupantId);
        Assert.Null(updatedOven.OccupantId);
    }

    [Fact]
    public async Task Handle_LocksFrontDoor_WhenOwnerHasActiveSleepJob()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _addJob.Handle(
            new AddCreatureJobCommand
            {
                CreatureJob = Builders.MakeCreatureJob(
                    owner.Id,
                    action: CreatureJobAction.Sleep,
                    startHour: 22,
                    endHour: 6,
                    priority: 100
                ),
            },
            TestContext.Current.CancellationToken
        );

        // Act — hour 23 falls inside the wraparound Sleep window
        await _handler.Handle(
            new SyncCommand
            {
                WorldId = WorldId,
                RoomId = frontDoor.RoomId,
                DistrictId = null,
                CurrentDate = MakeDate(23),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedDoor = await _context
            .Props.AsNoTracking()
            .OfType<RoomConnector>()
            .FirstAsync(c => c.Id == frontDoor.Id, TestContext.Current.CancellationToken);
        Assert.True(updatedDoor.IsLocked);
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

    private async Task<Building> SeedBuilding(Guid ownerId)
    {
        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: WorldId);
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
        var entranceRoom = Builders.MakeRoom(buildingId, worldId: WorldId);
        var frontDoor = new RoomConnector
        {
            RoomId = entranceRoom.Id,
            WorldId = WorldId,
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
