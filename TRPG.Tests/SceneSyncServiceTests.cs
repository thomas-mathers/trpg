using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class SceneSyncServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private BuildingService _buildingService = null!;
    private TrpgDbContext _context = null!;
    private CreatureService _creatureService = null!;
    private JobService _jobService = null!;
    private SceneSyncService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        _jobService = new JobService(_context);
        _creatureService = new CreatureService(_context);
        _buildingService = new BuildingService(_context, cache);
        var dispatcher = new JobDispatcher(
            new SleepJobHandler(_creatureService), new WorkJobHandler(_creatureService),
            new IdleJobHandler(_creatureService), NullLogger<JobDispatcher>.Instance);
        _service = new SceneSyncService(_jobService, _creatureService, dispatcher, _buildingService,
            NullLogger<SceneSyncService>.Instance);
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task SyncIfNeeded_MovesCreatureIntoRoom_WhenSleepJobActive() {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature();
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(creature.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            roomId: sleepRoomId, priority: 100), TestContext.Current.CancellationToken);
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 23 falls inside the wraparound Sleep window
        await _service.SyncIfNeeded(session, Guid.NewGuid(), sleepRoomId, null,
            new InGameDate(975, "Thawmoon", 1, "Stormday", 23), TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Equal(sleepRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_MovesCreatureOut_WhenHigherPriorityWorkJobActiveElsewhere() {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var workRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature();
        creature.RoomId = sleepRoomId;
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(creature.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            roomId: sleepRoomId, priority: 100), TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(creature.Id, action: JobAction.Work, startHour: 8, endHour: 20,
            roomId: workRoomId, priority: 50), TestContext.Current.CancellationToken);
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 10 is inside Work, and the creature is discovered via their stale Sleep-room assignment
        await _service.SyncIfNeeded(session, Guid.NewGuid(), sleepRoomId, null,
            new InGameDate(975, "Thawmoon", 1, "Stormday", 10), TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Equal(workRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_DoesNothing_WhenNoJobsTargetRoom() {
        // Arrange
        var creature = Builders.MakeCreature();
        var originalRoomId = creature.RoomId;
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);

        // Act
        await _service.SyncIfNeeded(session, Guid.NewGuid(), Guid.NewGuid(), null,
            new InGameDate(975, "Thawmoon", 1, "Stormday", 12), TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Equal(originalRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_MovesCreatureOutdoors_WhenIdleJobActive() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var sleepRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, districtId: districtId);
        creature.RoomId = sleepRoomId;
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(creature.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            roomId: sleepRoomId, priority: 100), TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(creature.Id, action: JobAction.Idle, startHour: 6, endHour: 22,
            roomId: null, priority: 0), TestContext.Current.CancellationToken);
        var session = new GameSession(worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 12 is inside Idle
        await _service.SyncIfNeeded(session, worldId, null, districtId,
            new InGameDate(975, "Thawmoon", 1, "Stormday", 12), TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Null(updated!.RoomId);
    }

    [Fact]
    public async Task SyncIfNeeded_LocksFrontDoor_WhenOwnerHasActiveSleepJob() {
        // Arrange
        var worldId = Guid.NewGuid();
        var owner = await SeedOwner(worldId);
        var building = await SeedBuilding(worldId, owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id, worldId);
        await _jobService.Add(Builders.MakeJob(owner.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            priority: 100), TestContext.Current.CancellationToken);
        var session = new GameSession(worldId, Guid.NewGuid(), TimeSpan.Zero);

        // Act — hour 23 falls inside the wraparound Sleep window
        await _service.SyncIfNeeded(session, worldId, frontDoor.RoomId, null,
            new InGameDate(975, "Thawmoon", 1, "Stormday", 23), TestContext.Current.CancellationToken);

        // Assert
        var updatedDoor = await _context.Props.AsNoTracking().OfType<RoomConnector>()
            .FirstAsync(c => c.Id == frontDoor.Id, TestContext.Current.CancellationToken);
        Assert.True(updatedDoor.IsLocked);
    }

    [Fact]
    public async Task SyncScheduleLock_Locks_DuringSleepHours() {
        // Arrange
        var worldId = Guid.NewGuid();
        var owner = await SeedOwner(worldId);
        var building = await SeedBuilding(worldId, owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id, worldId);
        await _jobService.Add(Builders.MakeJob(owner.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            priority: 100), TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(owner.Id, action: JobAction.Work, startHour: 8, endHour: 20,
            priority: 50), TestContext.Current.CancellationToken);

        // Act
        await _service.SyncScheduleLock(building.Id, building.BuildingType, 23, TestContext.Current.CancellationToken);

        // Assert
        var door = await _buildingService.GetFrontDoor(frontDoor.RoomId, TestContext.Current.CancellationToken);
        Assert.True(door!.IsLocked);
    }

    [Fact]
    public async Task SyncScheduleLock_Unlocks_DuringWorkHours() {
        // Arrange
        var worldId = Guid.NewGuid();
        var owner = await SeedOwner(worldId);
        var building = await SeedBuilding(worldId, owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id, worldId);
        await _jobService.Add(Builders.MakeJob(owner.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            priority: 100), TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(owner.Id, action: JobAction.Work, startHour: 8, endHour: 20,
            priority: 50), TestContext.Current.CancellationToken);
        await _service.SyncScheduleLock(building.Id, building.BuildingType, 23, TestContext.Current.CancellationToken);

        // Act
        await _service.SyncScheduleLock(building.Id, building.BuildingType, 12, TestContext.Current.CancellationToken);

        // Assert
        var door = await _buildingService.GetFrontDoor(frontDoor.RoomId, TestContext.Current.CancellationToken);
        Assert.False(door!.IsLocked);
    }

    [Fact]
    public async Task SyncScheduleLock_NeverLocks_InnOrTavern() {
        // Arrange
        var worldId = Guid.NewGuid();
        var owner = await SeedOwner(worldId);
        var building = await SeedBuilding(worldId, owner.Id);
        var frontDoor = await SeedFrontDoor(building.Id, worldId);
        await _jobService.Add(Builders.MakeJob(owner.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            priority: 100), TestContext.Current.CancellationToken);

        // Act
        await _service.SyncScheduleLock(building.Id, BuildingType.Tavern, 23, TestContext.Current.CancellationToken);

        // Assert
        var door = await _buildingService.GetFrontDoor(frontDoor.RoomId, TestContext.Current.CancellationToken);
        Assert.False(door!.IsLocked);
    }

    private async Task<Creature> SeedOwner(Guid worldId) {
        var owner = Builders.MakeCreature(worldId);
        await _creatureService.Add(owner, TestContext.Current.CancellationToken);
        return owner;
    }

    private async Task<Building> SeedBuilding(Guid worldId, Guid ownerId) {
        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: worldId);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _buildingService.AddOwner(building.Id, ownerId, TestContext.Current.CancellationToken);
        return building;
    }

    private async Task<RoomConnector> SeedFrontDoor(Guid buildingId, Guid worldId) {
        var entranceRoom = Builders.MakeRoom(buildingId, worldId: worldId);
        var frontDoor = new RoomConnector {
            RoomId = entranceRoom.Id, WorldId = worldId, Name = "Front Door",
            Description = "The door leading outside.", DestinationRoomId = null, IsLocked = false
        };
        _context.Rooms.Add(entranceRoom);
        _context.Props.Add(frontDoor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return frontDoor;
    }

    [Fact]
    public async Task SyncIfNeeded_ReturnsFalse_WhenScopeAndDateUnchanged() {
        // Arrange
        var roomId = Guid.NewGuid();
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);
        var currentDate = new InGameDate(975, "Thawmoon", 1, "Stormday", 12);
        await _service.SyncIfNeeded(session, Guid.NewGuid(), roomId, null, currentDate,
            TestContext.Current.CancellationToken);

        // Act
        var syncedAgain = await _service.SyncIfNeeded(session, Guid.NewGuid(), roomId, null, currentDate,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(syncedAgain);
    }
}
