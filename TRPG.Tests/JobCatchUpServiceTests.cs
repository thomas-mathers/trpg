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
    private JobService _jobService = null!;
    private PersonService _personService = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _jobService = new JobService(_context);
        _personService = new PersonService(_context);
        var dispatcher = new JobDispatcher(
            new SleepJobHandler(_personService), new WorkJobHandler(_personService), new IdleJobHandler(_personService),
            NullLogger<JobDispatcher>.Instance);
        _catchUp = new JobCatchUpService(_jobService, _personService, dispatcher, NullLogger<JobCatchUpService>.Instance);
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CatchUpRoom_MovesPersonIntoRoom_WhenSleepJobActive() {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var person = Builders.MakePerson();
        await _personService.Add(person, TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(person.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            roomId: sleepRoomId, priority: 100), TestContext.Current.CancellationToken);

        // Act — hour 23 falls inside the wraparound Sleep window
        await _catchUp.CatchUpRoom(sleepRoomId, 23, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Persons.FindAsync([person.Id], TestContext.Current.CancellationToken);
        Assert.Equal(sleepRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task CatchUpRoom_MovesPersonOut_WhenHigherPriorityWorkJobActiveElsewhere() {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var workRoomId = Guid.NewGuid();
        var person = Builders.MakePerson();
        person.RoomId = sleepRoomId;
        await _personService.Add(person, TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(person.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            roomId: sleepRoomId, priority: 100), TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(person.Id, action: JobAction.Work, startHour: 8, endHour: 20,
            roomId: workRoomId, priority: 50), TestContext.Current.CancellationToken);

        // Act — hour 10 is inside Work, and the person is discovered via their stale Sleep-room assignment
        await _catchUp.CatchUpRoom(sleepRoomId, 10, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Persons.FindAsync([person.Id], TestContext.Current.CancellationToken);
        Assert.Equal(workRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task CatchUpRoom_DoesNothing_WhenNoJobsTargetRoom() {
        // Arrange
        var person = Builders.MakePerson();
        var originalRoomId = person.RoomId;
        await _personService.Add(person, TestContext.Current.CancellationToken);

        // Act
        await _catchUp.CatchUpRoom(Guid.NewGuid(), 12, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Persons.FindAsync([person.Id], TestContext.Current.CancellationToken);
        Assert.Equal(originalRoomId, updated!.RoomId);
    }

    [Fact]
    public async Task CatchUpDistrict_MovesPersonOutdoors_WhenIdleJobActive() {
        // Arrange
        var worldId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var sleepRoomId = Guid.NewGuid();
        var person = Builders.MakePerson(worldId, districtId: districtId);
        person.RoomId = sleepRoomId;
        await _personService.Add(person, TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(person.Id, action: JobAction.Sleep, startHour: 22, endHour: 6,
            roomId: sleepRoomId, priority: 100), TestContext.Current.CancellationToken);
        await _jobService.Add(Builders.MakeJob(person.Id, action: JobAction.Idle, startHour: 6, endHour: 22,
            roomId: null, priority: 0), TestContext.Current.CancellationToken);

        // Act — hour 12 is inside Idle
        await _catchUp.CatchUpDistrict(worldId, districtId, 12, TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Persons.FindAsync([person.Id], TestContext.Current.CancellationToken);
        Assert.Null(updated!.RoomId);
    }
}