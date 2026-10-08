using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class ApplyCreaturePoseUpdatesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<ApplyCreaturePoseUpdatesCommand> _handler = null!;
    private Location _origin = null!;
    private Location _destination = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<ApplyCreaturePoseUpdatesCommand>
        >();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId);
        _origin = Builders.MakeLocation(WorldId, state.Id);
        _destination = Builders.MakeLocation(WorldId, state.Id);
        _context.States.Add(state);
        _context.Locations.AddRange(_origin, _destination);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WritesLocationAndWalkColumns_WhenTheUpdateCarriesAWalk()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(WorldId, locationId: _origin.Id));
        var update = new CreaturePoseUpdate(
            creature.Id,
            _destination.Id,
            _origin.Id,
            CreatureMovement.Walking,
            null,
            new WalkColumns(new Point(6, 7), Now, new Point(8, 9), Now)
        );

        // Act
        await _handler.Handle(
            new ApplyCreaturePoseUpdatesCommand { Updates = [update] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(_destination.Id, updated.LocationId);
        Assert.Equal(_origin.Id, updated.PreviousLocationId);
        Assert.Equal(CreatureMovement.Walking, updated.Movement);
        Assert.Equal((6, 7, Now), (updated.EntryX, updated.EntryY, updated.EnteredAt));
        Assert.Equal((8, 9, Now), (updated.ExitX, updated.ExitY, updated.DepartedAt));
    }

    [Fact]
    public async Task Handle_KeepsTheWalkColumns_WhenTheCreatureIsPlacedAtASizedDestination()
    {
        // Arrange
        var sized = Builders.MakeLocation(WorldId, _destination.StateId, width: 40, depth: 40);
        _context.Locations.Add(sized);
        var creature = await Seed(Builders.MakeCreature(WorldId, locationId: _origin.Id));
        var update = new CreaturePoseUpdate(
            creature.Id,
            sized.Id,
            _origin.Id,
            CreatureMovement.Walking,
            null,
            new WalkColumns(new Point(6, 7), Now, new Point(8, 9), Now)
        );

        // Act
        await _handler.Handle(
            new ApplyCreaturePoseUpdatesCommand { Updates = [update] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal((6, 7, Now), (updated.EntryX, updated.EntryY, updated.EnteredAt));
        Assert.Equal((8, 9, Now), (updated.ExitX, updated.ExitY, updated.DepartedAt));
    }

    [Fact]
    public async Task Handle_StandsTheCreatureAtThePointAndClearsTheWalk_WhenTheUpdateCarriesAStandPoint()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(WorldId, locationId: _origin.Id));
        var update = new CreaturePoseUpdate(
            creature.Id,
            _origin.Id,
            null,
            CreatureMovement.Stationary,
            null,
            new WalkColumns(null, null, null, null),
            new Point(12, 34)
        );

        // Act
        await _handler.Handle(
            new ApplyCreaturePoseUpdatesCommand { Updates = [update] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal((12d, 34d), (updated.X, updated.Y));
        Assert.Null(updated.DepartedAt);
    }

    [Fact]
    public async Task Handle_LeavesTheWalkColumns_WhenTheUpdateHasNoWalk()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(WorldId, locationId: _origin.Id));
        var update = new CreaturePoseUpdate(
            creature.Id,
            _origin.Id,
            null,
            CreatureMovement.Stationary,
            CreatureActivity.Working,
            null
        );

        // Act
        await _handler.Handle(
            new ApplyCreaturePoseUpdatesCommand { Updates = [update] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureActivity.Working, updated.Activity);
        Assert.Null(updated.PreviousLocationId);
    }

    [Fact]
    public async Task Handle_WakesTheSleeperAndReleasesTheBed_WhenASleepingCreatureIsUpdated()
    {
        // Arrange
        var creature = await Seed(
            Builders.MakeCreature(
                WorldId,
                locationId: _origin.Id,
                condition: CreatureCondition.Sleeping
            )
        );
        var bed = Builders.MakeBed(occupantId: creature.Id);
        _context.Props.Add(bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var update = new CreaturePoseUpdate(
            creature.Id,
            _origin.Id,
            null,
            CreatureMovement.Walking,
            null,
            null
        );

        // Act
        await _handler.Handle(
            new ApplyCreaturePoseUpdatesCommand { Updates = [update] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Awake, updated.Condition);
        Assert.Null(await db.ReadPropOccupantId(bed.Id));
    }

    [Fact]
    public async Task Handle_LeavesTheCreatureUntouched_WhenItIsDead()
    {
        // Arrange
        var creature = await Seed(
            Builders.MakeCreature(
                WorldId,
                locationId: _origin.Id,
                condition: CreatureCondition.Dead
            )
        );
        var update = new CreaturePoseUpdate(
            creature.Id,
            _destination.Id,
            _origin.Id,
            CreatureMovement.Walking,
            null,
            null
        );

        // Act
        await _handler.Handle(
            new ApplyCreaturePoseUpdatesCommand { Updates = [update] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal((_origin.Id, CreatureCondition.Dead), (updated.LocationId, updated.Condition));
    }

    private async Task<Creature> Seed(Creature creature)
    {
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }
}
