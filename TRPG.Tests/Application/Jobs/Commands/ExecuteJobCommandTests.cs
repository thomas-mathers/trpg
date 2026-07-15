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
        _handler = new ExecuteJobCommandHandler(new UpdateCreaturesCommandHandler(_context));

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

        // Act
        await _handler.Handle(
            new ExecuteJobCommand
            {
                CreatureId = _creature.Id,
                CurrentRoomId = _creature.RoomId,
                CurrentState = _creature.State,
                JobAction = action,
                JobRoomId = roomId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(roomId, updated!.RoomId);
        Assert.Equal(expectedState, updated.State);
    }

    private async Task AssertRoomIdUnchanged(JobAction action)
    {
        // Arrange
        var originalRoomId = _creature.RoomId;
        var originalState = _creature.State;

        // Act
        await _handler.Handle(
            new ExecuteJobCommand
            {
                CreatureId = _creature.Id,
                CurrentRoomId = _creature.RoomId,
                CurrentState = _creature.State,
                JobAction = action,
                JobRoomId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalRoomId, updated!.RoomId);
        Assert.Equal(originalState, updated.State);
    }
}
