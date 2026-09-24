using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

public sealed class ExecuteCreatureJobCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
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
    public async Task Handle_UpdatesCreatureState_ForSleepJob()
    {
        await AssertStateUpdated(CreatureJobAction.Sleep, CreatureState.Sleeping);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForWorkJob()
    {
        await AssertStateUpdated(CreatureJobAction.Work, CreatureState.Busy);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForIdleJob()
    {
        await AssertStateUpdated(CreatureJobAction.Idle, CreatureState.Idle);
    }

    [Fact]
    public async Task Handle_OccupiesAvailableSeat_ForIdleJob()
    {
        var seat = Builders.MakeSeat(_creature.WorldId, _creature.LocationId);
        _context.Props.Add(seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = _creature.LocationId,
                CurrentState = CreatureState.Idle,
                CreatureJobAction = CreatureJobAction.Idle,
                JobLocationId = _creature.LocationId,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        var updatedSeat = await verifyContext
            .Props.OfType<Seat>()
            .SingleAsync(prop => prop.Id == seat.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CreatureState.Sitting, updatedCreature.State);
        Assert.Equal(_creature.Id, updatedSeat.OccupantId);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForStudyJob()
    {
        await AssertStateUpdated(CreatureJobAction.Study, CreatureState.Studying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForPrayJob()
    {
        await AssertStateUpdated(CreatureJobAction.Pray, CreatureState.Praying);
    }

    [Fact]
    public async Task Handle_DoesNotMoveCreature_WhenJobLocationDiffers()
    {
        // Arrange
        var originalLocationId = _creature.LocationId;
        var newLocationId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = originalLocationId,
                CurrentState = _creature.State,
                CreatureJobAction = CreatureJobAction.Work,
                JobLocationId = newLocationId,
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
        Assert.Null(updated.PreviousLocationId);
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

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_WhenRestrained()
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
                CurrentState = CreatureState.Restrained,
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
    public async Task Handle_SetsBedOccupant_WhenTransitioningIntoSleepAtALocationWithTheirAssignedBed()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var bed = Builders.MakeBed(
            _creature.WorldId,
            locationId: locationId,
            assignedCreatureId: _creature.Id
        );
        _context.Props.Add(bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = locationId,
                CurrentState = _creature.State,
                CreatureJobAction = CreatureJobAction.Sleep,
                JobLocationId = locationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == bed.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_creature.Id, updatedBed.OccupantId);
    }

    [Fact]
    public async Task Handle_LeavesNoBedOccupied_WhenTheCreatureHasNoAssignedBedAtTheLocation()
    {
        // Arrange
        var locationId = _creature.LocationId;
        var bed = Builders.MakeBed(
            _creature.WorldId,
            locationId: locationId,
            assignedCreatureId: Guid.NewGuid()
        );
        _context.Props.Add(bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = _creature.LocationId,
                CurrentState = _creature.State,
                CreatureJobAction = CreatureJobAction.Sleep,
                JobLocationId = locationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == bed.Id, TestContext.Current.CancellationToken);
        Assert.Null(updatedBed.OccupantId);
    }

    [Fact]
    public async Task Handle_ClearsBedOccupant_WhenTransitioningOutOfSleep()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var bed = Builders.MakeBed(
            _creature.WorldId,
            locationId: locationId,
            assignedCreatureId: _creature.Id,
            occupantId: _creature.Id
        );
        _context.Props.Add(bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = locationId,
                CurrentState = CreatureState.Sleeping,
                CreatureJobAction = CreatureJobAction.Work,
                JobLocationId = locationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == bed.Id, TestContext.Current.CancellationToken);
        Assert.Null(updatedBed.OccupantId);
    }

    private async Task AssertStateUpdated(CreatureJobAction action, CreatureState expectedState)
    {
        // Arrange
        var locationId = _creature.LocationId;

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
        Assert.Equal(_creature.LocationId, updated!.LocationId);
        Assert.Equal(expectedState, updated.State);
    }
}
