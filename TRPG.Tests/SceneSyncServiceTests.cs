using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class SceneSyncServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly Guid _worldId = Guid.NewGuid();
    private BuildingService _buildingService = null!;
    private TrpgDbContext _context = null!;
    private CreatureService _creatureService = null!;
    private JobService _jobService = null!;
    private SceneSyncService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        _jobService = new JobService(_context);
        _creatureService = new CreatureService(_context);
        _buildingService = new BuildingService(_context, cache);
        var dispatcher = new JobDispatcher(
            new SleepJobHandler(_creatureService),
            new WorkJobHandler(_creatureService),
            new IdleJobHandler(_creatureService),
            new StudyJobHandler(_creatureService),
            new PrayJobHandler(_creatureService),
            new TrainJobHandler(_creatureService),
            new SitJobHandler(_creatureService),
            NullLogger<JobDispatcher>.Instance
        );
        _service = new SceneSyncService(
            _jobService,
            _creatureService,
            dispatcher,
            _buildingService,
            NullLogger<SceneSyncService>.Instance
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private static InGameDate MakeDate(int hour) =>
        new(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, hour);

    [Fact]
    public async Task SyncIfNeeded_MovesCreatureIntoRoom_WhenSleepJobActive()
    {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(_worldId);
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        await _jobService.Add(
            Builders.MakeJob(
                creature.Id,
                action: JobAction.Sleep,
                startHour: 22,
                endHour: 6,
                roomId: sleepRoomId,
                priority: 100
            ),
            TestContext.Current.CancellationToken
        );
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 23 falls inside the wraparound Sleep window
        await _service.SyncIfNeeded(
            session,
            _worldId,
            sleepRoomId,
            null,
            MakeDate(23),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _context.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(sleepRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_MovesCreatureOut_WhenHigherPriorityWorkJobActiveElsewhere()
    {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var workRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(_worldId);
        creature.RoomId = sleepRoomId;
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        await _jobService.Add(
            Builders.MakeJob(
                creature.Id,
                action: JobAction.Sleep,
                startHour: 22,
                endHour: 6,
                roomId: sleepRoomId,
                priority: 100
            ),
            TestContext.Current.CancellationToken
        );
        await _jobService.Add(
            Builders.MakeJob(
                creature.Id,
                action: JobAction.Work,
                startHour: 8,
                endHour: 20,
                roomId: workRoomId,
                priority: 50
            ),
            TestContext.Current.CancellationToken
        );
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 10 is inside Work, and the creature is discovered via their stale Sleep-room assignment
        await _service.SyncIfNeeded(
            session,
            _worldId,
            sleepRoomId,
            null,
            MakeDate(10),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _context.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(workRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_DoesNothing_WhenNoJobsTargetRoom()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);
        var originalRoomId = creature.RoomId;
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act
        await _service.SyncIfNeeded(
            session,
            _worldId,
            Guid.NewGuid(),
            null,
            MakeDate(12),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _context.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_MovesCreatureOutdoors_WhenIdleJobActive()
    {
        // Arrange
        var districtId = Guid.NewGuid();
        var sleepRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(_worldId, districtId: districtId);
        creature.RoomId = sleepRoomId;
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        await _jobService.Add(
            Builders.MakeJob(
                creature.Id,
                action: JobAction.Sleep,
                startHour: 22,
                endHour: 6,
                roomId: sleepRoomId,
                priority: 100
            ),
            TestContext.Current.CancellationToken
        );
        await _jobService.Add(
            Builders.MakeJob(
                creature.Id,
                action: JobAction.Idle,
                startHour: 6,
                endHour: 22,
                roomId: null,
                priority: 0
            ),
            TestContext.Current.CancellationToken
        );
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 12 is inside Idle
        await _service.SyncIfNeeded(
            session,
            _worldId,
            null,
            districtId,
            MakeDate(12),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await _context.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_AssignsBothWorkersToDifferentWorkstations_WhenTwoArePresent()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var counter = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = _worldId,
            Name = "Counter",
            Description = "A counter.",
            WorkstationType = WorkstationType.Trade,
        };
        var oven = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = _worldId,
            Name = "Oven",
            Description = "An oven.",
            WorkstationType = WorkstationType.Cooking,
        };
        _context.Props.Add(counter);
        _context.Props.Add(oven);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var owner = Builders.MakeCreature(_worldId);
        var employee = Builders.MakeCreature(_worldId);
        await _creatureService.Add(owner, TestContext.Current.CancellationToken);
        await _creatureService.Add(employee, TestContext.Current.CancellationToken);
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Work,
                startHour: 8,
                endHour: 20,
                roomId: shopRoomId,
                priority: 50
            ),
            TestContext.Current.CancellationToken
        );
        await _jobService.Add(
            Builders.MakeJob(
                employee.Id,
                action: JobAction.Work,
                startHour: 8,
                endHour: 20,
                roomId: shopRoomId,
                priority: 50
            ),
            TestContext.Current.CancellationToken
        );
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act
        await _service.SyncIfNeeded(
            session,
            _worldId,
            shopRoomId,
            null,
            MakeDate(12),
            TestContext.Current.CancellationToken
        );

        // Assert — both workstations get staffed, by two different people
        var workstations = await _buildingService.GetWorkstationsByRoomId(
            shopRoomId,
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
    public async Task SyncIfNeeded_GivesCounterPriority_AndClearsUnstaffedStation_WhenOnlyOneWorkerPresent()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var counter = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = _worldId,
            Name = "Counter",
            Description = "A counter.",
            WorkstationType = WorkstationType.Trade,
        };
        var oven = new Workstation
        {
            RoomId = shopRoomId,
            WorldId = _worldId,
            Name = "Oven",
            Description = "An oven.",
            WorkstationType = WorkstationType.Cooking,
            OccupantId = Guid.NewGuid(), // stale, from a prior day
        };
        _context.Props.Add(counter);
        _context.Props.Add(oven);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var owner = Builders.MakeCreature(_worldId);
        await _creatureService.Add(owner, TestContext.Current.CancellationToken);
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Work,
                startHour: 8,
                endHour: 20,
                roomId: shopRoomId,
                priority: 50
            ),
            TestContext.Current.CancellationToken
        );
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act
        await _service.SyncIfNeeded(
            session,
            _worldId,
            shopRoomId,
            null,
            MakeDate(12),
            TestContext.Current.CancellationToken
        );

        // Assert — the lone worker always gets the counter, and the unstaffed production station is cleared
        var workstations = await _buildingService.GetWorkstationsByRoomId(
            shopRoomId,
            TestContext.Current.CancellationToken
        );
        var updatedCounter = workstations.First(w => w.Id == counter.Id);
        var updatedOven = workstations.First(w => w.Id == oven.Id);
        Assert.Equal(owner.Id, updatedCounter.OccupantId);
        Assert.Null(updatedOven.OccupantId);
    }

    [Fact]
    public async Task SyncIfNeeded_LocksFrontDoor_WhenOwnerHasActiveSleepJob()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            ),
            TestContext.Current.CancellationToken
        );
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 23 falls inside the wraparound Sleep window
        await _service.SyncIfNeeded(
            session,
            _worldId,
            frontDoor.RoomId,
            null,
            MakeDate(23),
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedDoor = await _context
            .Props.AsNoTracking()
            .OfType<RoomConnector>()
            .FirstAsync(c => c.Id == frontDoor.Id, TestContext.Current.CancellationToken);
        Assert.True(updatedDoor.IsLocked);
    }

    [Fact]
    public async Task SyncScheduleLock_Locks_DuringSleepHours()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            ),
            TestContext.Current.CancellationToken
        );
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Work,
                startHour: 8,
                endHour: 20,
                priority: 50
            ),
            TestContext.Current.CancellationToken
        );

        // Act
        await _service.SyncScheduleLock(
            building.Id,
            building.BuildingType,
            MakeDate(23),
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _buildingService.GetFrontDoor(
            frontDoor.RoomId,
            TestContext.Current.CancellationToken
        );
        Assert.True(door!.IsLocked);
    }

    [Fact]
    public async Task SyncScheduleLock_Unlocks_DuringWorkHours()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            ),
            TestContext.Current.CancellationToken
        );
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Work,
                startHour: 8,
                endHour: 20,
                priority: 50
            ),
            TestContext.Current.CancellationToken
        );
        await _service.SyncScheduleLock(
            building.Id,
            building.BuildingType,
            MakeDate(23),
            TestContext.Current.CancellationToken
        );

        // Act
        await _service.SyncScheduleLock(
            building.Id,
            building.BuildingType,
            MakeDate(12),
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _buildingService.GetFrontDoor(
            frontDoor.RoomId,
            TestContext.Current.CancellationToken
        );
        Assert.False(door!.IsLocked);
    }

    [Fact]
    public async Task SyncScheduleLock_NeverLocks_InnOrTavern()
    {
        // Arrange
        var owner = await SeedOwner();
        var building = await SeedBuilding(owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id);
        await _jobService.Add(
            Builders.MakeJob(
                owner.Id,
                action: JobAction.Sleep,
                startHour: 22,
                endHour: 6,
                priority: 100
            ),
            TestContext.Current.CancellationToken
        );

        // Act
        await _service.SyncScheduleLock(
            building.Id,
            BuildingType.Tavern,
            MakeDate(23),
            TestContext.Current.CancellationToken
        );

        // Assert
        var door = await _buildingService.GetFrontDoor(
            frontDoor.RoomId,
            TestContext.Current.CancellationToken
        );
        Assert.False(door!.IsLocked);
    }

    private async Task<Creature> SeedOwner()
    {
        var owner = Builders.MakeCreature(_worldId);
        await _creatureService.Add(owner, TestContext.Current.CancellationToken);
        return owner;
    }

    private async Task<Building> SeedBuilding(Guid ownerId)
    {
        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: _worldId);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _buildingService.AddOwner(
            building.Id,
            ownerId,
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

    [Fact]
    public async Task SyncIfNeeded_ReturnsFalse_WhenScopeAndDateUnchanged()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var session = new GameSession(_worldId, Guid.NewGuid(), TimeSpan.Zero);
        var currentDate = MakeDate(12);
        await _service.SyncIfNeeded(
            session,
            _worldId,
            roomId,
            null,
            currentDate,
            TestContext.Current.CancellationToken
        );

        // Act
        var syncedAgain = await _service.SyncIfNeeded(
            session,
            _worldId,
            roomId,
            null,
            currentDate,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(syncedAgain);
    }
}
