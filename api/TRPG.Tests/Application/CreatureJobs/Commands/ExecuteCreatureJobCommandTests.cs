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
        await AssertStateUpdated(CreatureJobAction.Sleep, CreatureCondition.Sleeping, null);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForWorkJob()
    {
        await AssertStateUpdated(
            CreatureJobAction.Work,
            CreatureCondition.Awake,
            CreatureActivity.Working
        );
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForIdleJob()
    {
        await AssertStateUpdated(CreatureJobAction.Idle, CreatureCondition.Awake, null);
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
                CurrentCondition = CreatureCondition.Awake,
                CurrentPosture = CreaturePosture.Standing,
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
        Assert.Equal(CreaturePosture.Sitting, updatedCreature.Posture);
        Assert.Equal(_creature.Id, updatedSeat.OccupantId);
    }

    [Fact]
    public async Task Handle_RestoresThePreSitPose_WhenASeatedCreatureLeavesToWork()
    {
        var seat = Builders.MakeSeat(
            _creature.WorldId,
            _creature.LocationId,
            _creature.Id,
            x: 4,
            y: 5,
            angle: 1
        );
        _creature.Posture = CreaturePosture.Sitting;
        _creature.X = 4;
        _creature.Y = 5;
        _creature.Angle = 1;
        _creature.StandingX = 8;
        _creature.StandingY = 9;
        _creature.StandingAngle = 2;
        _context.Props.Add(seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = _creature.LocationId,
                CurrentCondition = CreatureCondition.Awake,
                CurrentPosture = CreaturePosture.Sitting,
                CreatureJobAction = CreatureJobAction.Work,
                JobLocationId = _creature.LocationId,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal((8d, 9d, 2d), (updatedCreature.X, updatedCreature.Y, updatedCreature.Angle));
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForStudyJob()
    {
        await AssertStateUpdated(
            CreatureJobAction.Study,
            CreatureCondition.Awake,
            CreatureActivity.Studying
        );
    }

    [Fact]
    public async Task Handle_UpdatesCreatureState_ForPrayJob()
    {
        await AssertStateUpdated(
            CreatureJobAction.Pray,
            CreatureCondition.Awake,
            CreatureActivity.Praying
        );
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
                CurrentCondition = _creature.Condition,
                CurrentPosture = _creature.Posture,
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
    public async Task Handle_LeavesCreatureUnchanged_WhenDead()
    {
        // Arrange
        var originalLocationId = _creature.LocationId;
        var originalCondition = _creature.Condition;

        // Act — a due Sleep job would normally move and re-state the creature
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = originalLocationId,
                CurrentCondition = CreatureCondition.Dead,
                CurrentPosture = _creature.Posture,
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
        Assert.Equal(originalCondition, updated.Condition);
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
                CurrentCondition = _creature.Condition,
                CurrentPosture = _creature.Posture,
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
                CurrentCondition = _creature.Condition,
                CurrentPosture = _creature.Posture,
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
                CurrentCondition = CreatureCondition.Sleeping,
                CurrentPosture = CreaturePosture.Lying,
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

    private async Task AssertStateUpdated(
        CreatureJobAction action,
        CreatureCondition expectedCondition,
        CreatureActivity? expectedActivity
    )
    {
        // Arrange
        var locationId = _creature.LocationId;

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentLocationId = _creature.LocationId,
                CurrentCondition = _creature.Condition,
                CurrentPosture = _creature.Posture,
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
        Assert.Equal(expectedCondition, updated.Condition);
        Assert.Equal(expectedActivity, updated.Activity);
    }
}
