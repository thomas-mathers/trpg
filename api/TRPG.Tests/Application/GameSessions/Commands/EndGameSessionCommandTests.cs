using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Narration.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameSessions.Commands;

[Collection("Database")]
public sealed class EndGameSessionCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly World _world = Builders.MakeWorld();
    private readonly Creature _player = Builders.MakeCreature();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EndGameSessionCommandHandler _handler = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EndGameSessionCommandHandler>();

        _session = Builders.MakeGameSession(_world.Id, _player.Id);
        _context.Worlds.Add(_world);
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ClearsTheLoreAnchorCaches_ForTheSessionsWorld()
    {
        // Arrange
        var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        var loreAnchorsKey = GetLoreAnchorsByWorldQueryHandler.CacheKey(_world.Id);
        var automatonKey = GetLoreAnchorAutomatonByWorldQueryHandler.CacheKey(_world.Id);
        cache.Set(loreAnchorsKey, Array.Empty<object>());
        cache.Set(automatonKey, new object());

        // Act
        await _handler.Handle(
            new EndGameSessionCommand { SessionId = _session.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(cache.TryGetValue(loreAnchorsKey, out _));
        Assert.False(cache.TryGetValue(automatonKey, out _));
    }

    [Fact]
    public async Task Handle_MarksActiveFightFled_WhenOneExistsForTheSessionsPlayer()
    {
        // Arrange
        var fight = Builders.MakeFight(_world.Id, _player.Id, [_player.Id]);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new EndGameSessionCommand { SessionId = _session.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedFight = await verifyContext.Fights.FindAsync(
            [fight.Id],
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(updatedFight!.CompletedAt);
        Assert.Equal(CombatOutcome.Fled, updatedFight.Outcome);
    }
}
