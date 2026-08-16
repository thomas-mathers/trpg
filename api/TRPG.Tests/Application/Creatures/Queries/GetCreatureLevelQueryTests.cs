using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureLevelQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureLevelQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(level: 7);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureLevelQueryHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreatureLevel()
    {
        // Act
        var level = await _handler.Handle(
            new GetCreatureLevelQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(7, level);
    }
}
