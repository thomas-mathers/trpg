using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class StartWalkingCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<StartWalkingCommand> _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ICommandHandler<StartWalkingCommand>>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_StandsTheCreatureUp_WhenItWasSitting()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(posture: CreaturePosture.Sitting));

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreaturePosture.Standing, updated.Posture);
        Assert.Equal(CreatureMovement.Walking, updated.Movement);
    }

    [Fact]
    public async Task Handle_WakesAndStandsTheCreatureUp_WhenItWasSleeping()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Sleeping));

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Awake, updated.Condition);
        Assert.Equal(CreaturePosture.Standing, updated.Posture);
        Assert.Equal(CreatureMovement.Walking, updated.Movement);
    }

    [Fact]
    public async Task Handle_ClearsTheActivity_WhenTheCreatureWasWorking()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(activity: CreatureActivity.Working));

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Null(updated.Activity);
        Assert.Equal(CreatureMovement.Walking, updated.Movement);
    }

    [Fact]
    public async Task Handle_LeavesTheCreatureDead_WhenItIsDead()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Dead));

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Dead, updated.Condition);
        Assert.Equal(CreatureMovement.Stationary, updated.Movement);
    }

    [Fact]
    public async Task Handle_ReleasesTheSeat_WhenTheCreatureWasSitting()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(posture: CreaturePosture.Sitting));
        var seat = Builders.MakeSeat(occupantId: creature.Id);
        _context.Props.Add(seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(seat.Id));
    }

    [Fact]
    public async Task Handle_ReleasesTheBed_WhenTheCreatureWasSleeping()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Sleeping));
        var bed = Builders.MakeBed(occupantId: creature.Id);
        _context.Props.Add(bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(bed.Id));
    }

    [Fact]
    public async Task Handle_ReleasesTheWorkstation_WhenTheCreatureWasWorking()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(activity: CreatureActivity.Working));
        var workstation = Builders.MakeWorkstation(occupantId: creature.Id);
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(workstation.Id));
    }

    [Fact]
    public async Task Handle_KeepsTheSeat_WhenTheCreatureIsDead()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Dead));
        var seat = Builders.MakeSeat(occupantId: creature.Id);
        _context.Props.Add(seat);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(creature.Id, await db.ReadPropOccupantId(seat.Id));
    }

    [Fact]
    public async Task Handle_RecordsTheExitPoint_WhenTheCreatureLeavesAfterADwell()
    {
        // Arrange
        var (here, previous, exit) = await SeedLocations();
        var creature = await Seed(
            Builders.MakeCreature(locationId: here.Id, previousLocationId: previous.Id)
        );

        // Act
        await _handler.Handle(
            new StartWalkingCommand
            {
                CreatureIds = [creature.Id],
                Exit = new WalkExit(exit.Id, Departed, ArrivedAt: Departed - TimeSpan.FromHours(1)),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal((9d, 8d, Departed), (updated.ExitX, updated.ExitY, updated.DepartedAt));
        Assert.Null(updated.EnteredAt);
    }

    [Fact]
    public async Task Handle_RecordsTheEntryPointFromThePreviousLocation_WhenTheCreaturePassesThrough()
    {
        // Arrange
        var (here, previous, exit) = await SeedLocations();
        var creature = await Seed(
            Builders.MakeCreature(locationId: here.Id, previousLocationId: previous.Id)
        );

        // Act
        await _handler.Handle(
            new StartWalkingCommand
            {
                CreatureIds = [creature.Id],
                Exit = new WalkExit(exit.Id, Departed, ArrivedAt: Departed),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal((2d, 3d, Departed), (updated.EntryX, updated.EntryY, updated.EnteredAt));
        Assert.Equal((9d, 8d, Departed), (updated.ExitX, updated.ExitY, updated.DepartedAt));
    }

    [Fact]
    public async Task Handle_ClearsAStaleEntryWalk_WhenTheCreatureLeavesAfterADwell()
    {
        // Arrange
        var (here, previous, exit) = await SeedLocations();
        var creature = Builders.MakeCreature(locationId: here.Id, previousLocationId: previous.Id);
        creature.EntryX = 1;
        creature.EntryY = 1;
        creature.EnteredAt = Departed - TimeSpan.FromHours(1);
        await Seed(creature);

        // Act
        await _handler.Handle(
            new StartWalkingCommand
            {
                CreatureIds = [creature.Id],
                Exit = new WalkExit(exit.Id, Departed, ArrivedAt: null),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal((null, null, null), (updated.EntryX, updated.EntryY, updated.EnteredAt));
    }

    [Fact]
    public async Task Handle_ClearsTheExitWalk_WhenNoExitIsGiven()
    {
        // Arrange
        var creature = Builders.MakeCreature();
        creature.ExitX = 4;
        creature.ExitY = 4;
        creature.DepartedAt = Departed;
        await Seed(creature);

        // Act
        await _handler.Handle(
            new StartWalkingCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal((null, null, null), (updated.ExitX, updated.ExitY, updated.DepartedAt));
    }

    private static readonly GameInstant Departed = new(
        new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified)
    );

    private async Task<(Location Here, Location Previous, LocationConnector Exit)> SeedLocations()
    {
        var worldId = Guid.NewGuid();
        var here = Builders.MakeLocation(worldId: worldId, width: 20, depth: 20);
        var previous = Builders.MakeLocation(worldId: worldId, width: 20, depth: 20);
        var next = Builders.MakeLocation(worldId: worldId, width: 20, depth: 20);
        var entering = Builders.MakeLocationConnector(previous.Id, here.Id, worldId: worldId);
        var exit = Builders.MakeLocationConnector(here.Id, next.Id, worldId: worldId);
        _context.Locations.AddRange(here, previous, next);
        _context.LocationConnectors.AddRange(entering, exit);
        _context.TravelNodes.AddRange(
            Builders.MakeExitNode(entering),
            Builders.MakeArrivalNode(entering, 2, 3),
            Builders.MakeExitNode(exit, 9, 8),
            Builders.MakeArrivalNode(exit)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (here, previous, exit);
    }

    private async Task<Creature> Seed(Creature creature)
    {
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }
}
