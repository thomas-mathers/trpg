using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class JobDispatcherTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private JobDispatcher _dispatcher = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        var creatureService = new CreatureService(_context);
        _dispatcher = new JobDispatcher(
            new SleepJobHandler(creatureService),
            new WorkJobHandler(creatureService),
            new IdleJobHandler(creatureService),
            new StudyJobHandler(creatureService),
            new PrayJobHandler(creatureService),
            new TrainJobHandler(creatureService),
            new SitJobHandler(creatureService),
            NullLogger<JobDispatcher>.Instance
        );

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Dispatch_UpdatesCreatureRoomIdAndState_ForSleepJob()
    {
        await AssertRoomIdUpdated(JobAction.Sleep, CreatureState.Sleeping);
    }

    [Fact]
    public async Task Dispatch_UpdatesCreatureRoomIdAndState_ForWorkJob()
    {
        await AssertRoomIdUpdated(JobAction.Work, CreatureState.Busy);
    }

    [Fact]
    public async Task Dispatch_UpdatesCreatureRoomIdAndState_ForIdleJob()
    {
        await AssertRoomIdUpdated(JobAction.Idle, CreatureState.Idle);
    }

    [Fact]
    public async Task Dispatch_UpdatesCreatureRoomIdAndState_ForStudyJob()
    {
        await AssertRoomIdUpdated(JobAction.Study, CreatureState.Studying);
    }

    [Fact]
    public async Task Dispatch_UpdatesCreatureRoomIdAndState_ForPrayJob()
    {
        await AssertRoomIdUpdated(JobAction.Pray, CreatureState.Praying);
    }

    [Fact]
    public async Task Dispatch_UpdatesCreatureRoomIdAndState_ForTrainJob()
    {
        await AssertRoomIdUpdated(JobAction.Train, CreatureState.Training);
    }

    [Fact]
    public async Task Dispatch_UpdatesCreatureRoomIdAndState_ForSitJob()
    {
        await AssertRoomIdUpdated(JobAction.Sit, CreatureState.Sitting);
    }

    [Fact]
    public async Task Dispatch_LeavesCreatureUnchanged_ForPatrolJob()
    {
        await AssertRoomIdUnchanged(JobAction.Patrol);
    }

    [Fact]
    public async Task Dispatch_LeavesCreatureUnchanged_ForSocializeJob()
    {
        await AssertRoomIdUnchanged(JobAction.Socialize);
    }

    private async Task AssertRoomIdUpdated(JobAction action, CreatureState expectedState)
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var job = Builders.MakeJob(_creature.Id, action: action, roomId: roomId);

        // Act
        await _dispatcher.Dispatch(_creature, job, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(roomId, _creature.RoomId);
        Assert.Equal(expectedState, _creature.State);
    }

    private async Task AssertRoomIdUnchanged(JobAction action)
    {
        // Arrange
        var originalRoomId = _creature.RoomId;
        var originalState = _creature.State;
        var job = Builders.MakeJob(_creature.Id, action: action, roomId: Guid.NewGuid());

        // Act
        await _dispatcher.Dispatch(_creature, job, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(originalRoomId, _creature.RoomId);
        Assert.Equal(originalState, _creature.State);
    }
}
