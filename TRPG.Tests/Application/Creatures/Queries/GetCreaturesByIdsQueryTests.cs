using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreaturesByIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreaturesByIdsQueryHandler _handler = null!;
    private Creature _creatureA = null!;
    private Creature _creatureB = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreaturesByIdsQueryHandler(_context);

        _creatureA = Builders.MakeCreature();
        _creatureB = Builders.MakeCreature();
        _context.Creatures.AddRange(_creatureA, _creatureB);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoIdsMatch()
    {
        // Act
        var result = await _handler.Handle(
            new GetCreaturesByIdsQuery { Ids = [Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsAllMatchingCreatures()
    {
        // Act
        var result = await _handler.Handle(
            new GetCreaturesByIdsQuery { Ids = [_creatureA.Id, _creatureB.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Id == _creatureA.Id);
        Assert.Contains(result, c => c.Id == _creatureB.Id);
    }
}
