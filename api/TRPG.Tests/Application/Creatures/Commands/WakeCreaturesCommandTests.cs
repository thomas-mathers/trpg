using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class WakeCreaturesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<WakeCreaturesCommand> _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ICommandHandler<WakeCreaturesCommand>>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_StandsTheCreatureUp_WhenItWasSleeping()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Sleeping));

        // Act
        await _handler.Handle(
            new WakeCreaturesCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Awake, updated.Condition);
        Assert.Equal(CreaturePosture.Standing, updated.Posture);
    }

    [Fact]
    public async Task Handle_LeavesTheCreatureSeated_WhenItWasAlreadyAwake()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(posture: CreaturePosture.Sitting));

        // Act
        await _handler.Handle(
            new WakeCreaturesCommand { CreatureIds = [creature.Id] },
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
            new WakeCreaturesCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.Equal(CreatureCondition.Dead, updated.Condition);
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
            new WakeCreaturesCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(await db.ReadPropOccupantId(bed.Id));
    }

    [Fact]
    public async Task Handle_KeepsTheBed_WhenTheCreatureWasNotSleeping()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature());
        var bed = Builders.MakeBed(occupantId: creature.Id);
        _context.Props.Add(bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new WakeCreaturesCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(creature.Id, await db.ReadPropOccupantId(bed.Id));
    }

    private async Task<Creature> Seed(Creature creature)
    {
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }
}
