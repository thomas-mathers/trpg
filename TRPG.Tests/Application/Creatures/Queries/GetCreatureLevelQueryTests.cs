using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureLevelQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureLevelQueryHandler _handler = null!;
    private readonly Creature _creature = MakeSeedCreature();

    private static Creature MakeSeedCreature()
    {
        var creature = Builders.MakeCreature();
        creature.Level = 7;
        return creature;
    }

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureLevelQueryHandler(_context);

        await _context.AddCreature(_creature, TestContext.Current.CancellationToken);
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
