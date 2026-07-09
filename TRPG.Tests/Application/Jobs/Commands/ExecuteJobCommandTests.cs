using TRPG.Application.Creatures.Commands;
using TRPG.Application.Jobs.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Jobs.Commands;

[Collection("Database")]
public sealed class ExecuteJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private ExecuteJobCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new ExecuteJobCommandHandler(new UpdateCreatureCommandHandler(_context));

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForSleepJob()
    {
        await AssertRoomIdUpdated(JobAction.Sleep, CreatureState.Sleeping);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForWorkJob()
    {
        await AssertRoomIdUpdated(JobAction.Work, CreatureState.Busy);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForIdleJob()
    {
        await AssertRoomIdUpdated(JobAction.Idle, CreatureState.Idle);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForStudyJob()
    {
        await AssertRoomIdUpdated(JobAction.Study, CreatureState.Studying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForPrayJob()
    {
        await AssertRoomIdUpdated(JobAction.Pray, CreatureState.Praying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForTrainJob()
    {
        await AssertRoomIdUpdated(JobAction.Train, CreatureState.Training);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForSitJob()
    {
        await AssertRoomIdUpdated(JobAction.Sit, CreatureState.Sitting);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_ForPatrolJob()
    {
        await AssertRoomIdUnchanged(JobAction.Patrol);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_ForSocializeJob()
    {
        await AssertRoomIdUnchanged(JobAction.Socialize);
    }

    private async Task AssertRoomIdUpdated(JobAction action, CreatureState expectedState)
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var job = Builders.MakeJob(_creature.Id, action: action, roomId: roomId);

        // Act
        await _handler.Handle(
            new ExecuteJobCommand { Creature = _creature, Job = job },
            TestContext.Current.CancellationToken
        );

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
        await _handler.Handle(
            new ExecuteJobCommand { Creature = _creature, Job = job },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(originalRoomId, _creature.RoomId);
        Assert.Equal(originalState, _creature.State);
    }
}
