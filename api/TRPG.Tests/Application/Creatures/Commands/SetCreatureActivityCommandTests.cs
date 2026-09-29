using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class SetCreatureActivityCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<SetCreatureActivityCommand> _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<SetCreatureActivityCommand>
        >();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SetsTheActivity_WhenTheCreatureIsAwake()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature());

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [creature.Id],
                Activity = CreatureActivity.Studying,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureActivity.Studying, updated.Activity);
    }

    [Fact]
    public async Task Handle_ClearsTheActivity_WhenActivityIsNull()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(activity: CreatureActivity.Working));

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand { CreatureIds = [creature.Id], Activity = null },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Null(updated.Activity);
    }

    [Fact]
    public async Task Handle_StopsWalking_WhenTheCreatureWasWalking()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(movement: CreatureMovement.Walking));

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [creature.Id],
                Activity = CreatureActivity.Working,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureMovement.Stationary, updated.Movement);
        Assert.Equal(CreatureActivity.Working, updated.Activity);
    }

    [Fact]
    public async Task Handle_WakesAndStandsTheCreatureUp_WhenItWasSleeping()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Sleeping));

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [creature.Id],
                Activity = CreatureActivity.Praying,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Awake, updated.Condition);
        Assert.Equal(CreaturePosture.Standing, updated.Posture);
    }

    [Fact]
    public async Task Handle_KeepsTheCreatureSeated_WhenItWasSitting()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(posture: CreaturePosture.Sitting));

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [creature.Id],
                Activity = CreatureActivity.Eating,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreaturePosture.Sitting, updated.Posture);
    }

    [Fact]
    public async Task Handle_LeavesTheCreatureDead_WhenItIsDead()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Dead));

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [creature.Id],
                Activity = CreatureActivity.Working,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Dead, updated.Condition);
        Assert.Null(updated.Activity);
    }

    [Fact]
    public async Task Handle_ReleasesTheBed_WhenTheCreatureWasSleeping()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Sleeping));
        var prop = Builders.MakeBed(occupantId: creature.Id);
        _context.Props.Add(prop);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [creature.Id],
                Activity = CreatureActivity.Working,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(prop.Id));
    }

    [Fact]
    public async Task Handle_KeepsTheSeat_WhenTheCreatureWasSitting()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(posture: CreaturePosture.Sitting));
        var prop = Builders.MakeSeat(occupantId: creature.Id);
        _context.Props.Add(prop);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SetCreatureActivityCommand
            {
                CreatureIds = [creature.Id],
                Activity = CreatureActivity.Eating,
            },
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
