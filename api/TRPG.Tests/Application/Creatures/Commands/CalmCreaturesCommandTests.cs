using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class CalmCreaturesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private CalmCreaturesCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new CalmCreaturesCommandHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CalmsTheCreature_WhenItIsAlerted()
    {
        // Arrange
        var creature = await Seed(Builders.MakeCreature(isAlerted: true));

        // Act
        await _handler.Handle(
            new CalmCreaturesCommand { CreatureIds = [creature.Id] },
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
