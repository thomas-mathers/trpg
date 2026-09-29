using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class AlertCreaturesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private AlertCreaturesCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AlertCreaturesCommandHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AlertsTheCreature_WhenItIsAwake()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature());

        // Act
        await _handler.Handle(
            new AlertCreaturesCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.True(updated.IsAlerted);
    }

    [Fact]
    public async Task Handle_DoesNotAlertTheCreature_WhenItIsSleeping()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Sleeping));

        // Act
        await _handler.Handle(
            new AlertCreaturesCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.False(updated.IsAlerted);
    }

    [Fact]
    public async Task Handle_DoesNotAlertTheCreature_WhenItIsDead()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(condition: CreatureCondition.Dead));

        // Act
        await _handler.Handle(
            new AlertCreaturesCommand { CreatureIds = [creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await db.ReadCreature(creature.Id);
        Assert.False(updated.IsAlerted);
    }

    private async Task<Creature> Seed(Creature creature)
    {
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }
}
