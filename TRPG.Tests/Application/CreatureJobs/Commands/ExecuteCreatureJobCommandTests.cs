using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

[Collection("Database")]
public sealed class ExecuteCreatureJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ExecuteCreatureJobCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new ExecuteCreatureJobCommandHandler(
            new UpdateCreaturesCommandHandler(_context)
        );

        await _context.AddCreature(_creature, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForSleepJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Sleep, CreatureState.Sleeping);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForWorkJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Work, CreatureState.Busy);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForIdleJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Idle, CreatureState.Idle);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForStudyJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Study, CreatureState.Studying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForPrayJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Pray, CreatureState.Praying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForTrainJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Train, CreatureState.Training);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForSitJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Sit, CreatureState.Sitting);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_ForPatrolJob()
    {
        await AssertRoomIdUnchanged(CreatureJobAction.Patrol);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_ForSocializeJob()
    {
        await AssertRoomIdUnchanged(CreatureJobAction.Socialize);
    }

    private async Task AssertRoomIdUpdated(CreatureJobAction action, CreatureState expectedState)
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentRoomId = _creature.RoomId,
                CurrentState = _creature.State,
                CreatureJobAction = action,
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

    private async Task AssertRoomIdUnchanged(CreatureJobAction action)
    {
        // Arrange
        var originalRoomId = _creature.RoomId;
        var originalState = _creature.State;

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentRoomId = _creature.RoomId,
                CurrentState = _creature.State,
                CreatureJobAction = action,
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
