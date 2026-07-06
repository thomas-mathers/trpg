using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class JobCatchUpServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private JobCatchUpService _catchUp = null!;
    private TrpgDbContext _context = null!;
    private CreatureService _creatureService = null!;
    private JobService _jobService = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _jobService = new JobService(_context);
        _creatureService = new CreatureService(_context);
        var dispatcher = new JobDispatcher(
            new SleepJobHandler(_creatureService), new WorkJobHandler(_creatureService),
            new IdleJobHandler(_creatureService), NullLogger<JobDispatcher>.Instance);
        _catchUp = new JobCatchUpService(_jobService, _creatureService, dispatcher,
            NullLogger<JobCatchUpService>.Instance);
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CatchUpRoom_MovesCreatureIntoRoom_WhenSleepJobActive() {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var creature = Builders.MakeCreature();
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(creature.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            roomId: sleepRoomId, priority: 100), TestContext.Current.CancellationToken);

        // Act — hour 23 falls inside the wraparound Sleep window
        await _catchUp.CatchUpRoom(sleepRoomId, 23, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Equal(sleepRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task CatchUpRoom_MovesCreatureOut_WhenHigherPriorityWorkJobActiveElsewhere() {
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

        // Act — hour 10 is inside Work, and the creature is discovered via their stale Sleep-room assignment
        await _catchUp.CatchUpRoom(sleepRoomId, 10, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Equal(workRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task CatchUpRoom_DoesNothing_WhenNoJobsTargetRoom() {
        // Arrange
        var creature = Builders.MakeCreature();
        var originalRoomId = creature.RoomId;
        await _creatureService.Add(creature, TestContext.Current.CancellationToken);

        // Act
        await _catchUp.CatchUpRoom(Guid.NewGuid(), 12, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Equal(originalRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task CatchUpDistrict_MovesCreatureOutdoors_WhenIdleJobActive() {
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

        // Act — hour 12 is inside Idle
        await _catchUp.CatchUpDistrict(worldId, districtId, 12, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Creatures.FindAsync([creature.Id], TestContext.Current.CancellationToken);
        Assert.Null(updated!.RoomId);
    }
}
