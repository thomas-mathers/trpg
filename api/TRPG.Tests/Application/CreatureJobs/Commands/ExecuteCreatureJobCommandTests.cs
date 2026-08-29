using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

[Collection("Database")]
public sealed class ExecuteCreatureJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ExecuteCreatureJobCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ExecuteCreatureJobCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesCreatureLocationAndState_ForSleepJob()
    {
        await AssertLocationIdUpdated(CreatureJobAction.Sleep, CreatureState.Sleeping);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureLocationAndState_ForWorkJob()
    {
        await AssertLocationIdUpdated(CreatureJobAction.Work, CreatureState.Busy);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureLocationAndState_ForIdleJob()
    {
        await AssertLocationIdUpdated(CreatureJobAction.Idle, CreatureState.Idle);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureLocationAndState_ForStudyJob()
    {
        await AssertLocationIdUpdated(CreatureJobAction.Study, CreatureState.Studying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureLocationAndState_ForPrayJob()
    {
        await AssertLocationIdUpdated(CreatureJobAction.Pray, CreatureState.Praying);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_WhenCurrentlyAlerted()
    {
        // Arrange
        var originalLocationId = _creature.LocationId;
        var originalState = _creature.State;

        // Act — a due Sleep job would normally move and re-state the creature
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = originalLocationId,
                CurrentState = CreatureState.Alerted,
                CreatureJobAction = CreatureJobAction.Sleep,
                JobLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalLocationId, updated!.LocationId);
        Assert.Equal(originalState, updated.State);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_WhenDead()
    {
        // Arrange
        var originalLocationId = _creature.LocationId;
        var originalState = _creature.State;

        // Act — a due Sleep job would normally move and re-state the creature
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = originalLocationId,
                CurrentState = CreatureState.Dead,
                CreatureJobAction = CreatureJobAction.Sleep,
                JobLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalLocationId, updated!.LocationId);
        Assert.Equal(originalState, updated.State);
    }

    private async Task AssertLocationIdUpdated(
        CreatureJobAction action,
        CreatureState expectedState
    )
    {
        // Arrange
        var locationId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = _creature.LocationId,
                CurrentState = _creature.State,
                CreatureJobAction = action,
                JobLocationId = locationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(locationId, updated!.LocationId);
        Assert.Equal(expectedState, updated.State);
    }
}
