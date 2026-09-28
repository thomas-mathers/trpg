using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class PutCreaturesToSleepCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<PutCreaturesToSleepCommand> _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<PutCreaturesToSleepCommand>
        >();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_LaysTheCreatureDown_WhenItWasStanding()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature());

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Sleeping, updated.Condition);
        Assert.Equal(CreaturePosture.Lying, updated.Posture);
    }

    [Fact]
    public async Task Handle_StopsTheCreatureWalking_WhenItWasWalking()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(movement: CreatureMovement.Walking));

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureMovement.Stationary, updated.Movement);
    }

    [Fact]
    public async Task Handle_ClearsTheActivity_WhenTheCreatureWasWorking()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(activity: CreatureActivity.Working));

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Null(updated.Activity);
    }

    [Fact]
    public async Task Handle_CalmsAndUnsneaksTheCreature_WhenItWasAlertedAndSneaking()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(isAlerted: true, isSneaking: true));

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.False(updated.IsAlerted);
        Assert.False(updated.IsSneaking);
    }

    [Fact]
    public async Task Handle_LeavesTheCreatureDead_WhenItIsDead()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Dead));

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Dead, updated.Condition);
    }

    [Fact]
    public async Task Handle_ReleasesTheSeat_WhenTheCreatureWasSitting()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(posture: CreaturePosture.Sitting));
        var prop = Builders.MakeSeat(occupantId: creature.Id);
        _context.Props.Add(prop);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(prop.Id));
    }

    [Fact]
    public async Task Handle_ReleasesTheWorkstation_WhenTheCreatureWasWorking()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(activity: CreatureActivity.Working));
        var prop = Builders.MakeWorkstation(occupantId: creature.Id);
        _context.Props.Add(prop);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(prop.Id));
    }

    [Fact]
    public async Task Handle_KeepsTheBed_WhenTheCreatureGoesToSleepInIt()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature());
        var prop = Builders.MakeBed(occupantId: creature.Id);
        _context.Props.Add(prop);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PutCreaturesToSleepCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(creature.Id, await db.ReadPropOccupantId(prop.Id));
    }

    private async Task<Creature> Seed(Creature creature)
    {
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }
}
