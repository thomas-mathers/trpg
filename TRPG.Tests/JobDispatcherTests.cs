using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class JobDispatcherTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private JobDispatcher _dispatcher = null!;
    private Person _person = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        var personService = new PersonService(_context);
        _dispatcher = new JobDispatcher(
            new SleepJobHandler(personService), new WorkJobHandler(personService), new IdleJobHandler(personService),
            NullLogger<JobDispatcher>.Instance);

        _person = Builders.MakePerson();
        _context.Persons.Add(_person);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Dispatch_UpdatesPersonRoomIdAndState_ForSleepJob() {
        await AssertRoomIdUpdated(JobAction.Sleep, PersonState.Sleeping);
    }

    [Fact]
    public async Task Dispatch_UpdatesPersonRoomIdAndState_ForWorkJob() {
        await AssertRoomIdUpdated(JobAction.Work, PersonState.Busy);
    }

    [Fact]
    public async Task Dispatch_UpdatesPersonRoomIdAndState_ForIdleJob() {
        await AssertRoomIdUpdated(JobAction.Idle, PersonState.Idle);
    }

    [Fact]
    public async Task Dispatch_LeavesPersonUnchanged_ForPatrolJob() {
        await AssertRoomIdUnchanged(JobAction.Patrol);
    }

    [Fact]
    public async Task Dispatch_LeavesPersonUnchanged_ForSocializeJob() {
        await AssertRoomIdUnchanged(JobAction.Socialize);
    }

    private async Task AssertRoomIdUpdated(JobAction action, PersonState expectedState) {
        // Arrange
        var roomId = Guid.NewGuid();
        var job = Builders.MakeJob(_person.Id, action: action, roomId: roomId);

        // Act
        await _dispatcher.Dispatch(_person, job, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(roomId, _person.RoomId);
        Assert.Equal(expectedState, _person.State);
    }

    private async Task AssertRoomIdUnchanged(JobAction action) {
        // Arrange
        var originalRoomId = _person.RoomId;
        var originalState = _person.State;
        var job = Builders.MakeJob(_person.Id, action: action, roomId: Guid.NewGuid());

        // Act
        await _dispatcher.Dispatch(_person, job, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(originalRoomId, _person.RoomId);
        Assert.Equal(originalState, _person.State);
    }
}