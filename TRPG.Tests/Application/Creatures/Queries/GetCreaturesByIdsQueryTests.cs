using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreaturesByIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreaturesByIdsQueryHandler _handler = null!;
    private readonly Creature _creatureA = Builders.MakeCreature();
    private readonly Creature _creatureB = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreaturesByIdsQueryHandler(_context);

        await _context.AddCreature([_creatureA, _creatureB], TestContext.Current.CancellationToken);
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
        Assert.True(result.ContainsKey(_creatureA.Id));
        Assert.True(result.ContainsKey(_creatureB.Id));
    }
}
