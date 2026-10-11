using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Events;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Encounters.Events;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Commands;

public sealed class SyncActiveFightEffectsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant OneRoundIn = GameClock.Epoch + CombatTiming.Round;

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncActiveFightEffectsCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncActiveFightEffectsCommandHandler>();

        _player = Builders.MakeCreature(_worldId, locationId: _locationId, currentHp: 50);
        _context.Worlds.Add(Builders.MakeWorld(_worldId, OneRoundIn));
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _player.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private IReadOnlyList<T> Published<T>()
        where T : GameClientEvent =>
        _serviceProvider
            .GetRequiredService<TestGameClientEventSink>()
            .EnqueuedEvents.OfType<T>()
            .ToArray();

    private async Task<Creature> SeedBurningEnemy(int amount)
    {
        var enemy = Builders.MakeCreature(_worldId, locationId: _locationId, currentHp: 50);
        enemy.ActiveDots =
        [
            new ActiveDot
            {
                AbilityName = "Ignite",
                Amount = amount,
                DamageType = nameof(DamageType.Fire),
                NextTickAt = OneRoundIn,
                ExpiresAt = OneRoundIn + CombatTiming.Round * 10,
            },
        ];
        _context.Creatures.Add(enemy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return enemy;
    }

    private Task Sync() =>
        _handler.Handle(
            new SyncActiveFightEffectsCommand
            {
                WorldId = _worldId,
                GameTime = OneRoundIn,
                Players = [new ActiveLocationPlayer(_locationId, _player.Id, PlayerLevel: 1)],
            },
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task Handle_TicksAFightThroughTheFightCommand_NotTheOutOfCombatPath()
    {
        // Arrange
        var enemy = await SeedBurningEnemy(amount: 1000);
        _context.Encounters.Add(
            new FightEncounter
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                LocationId = _locationId,
                CombatantIds = [_player.Id, enemy.Id],
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Sync();

        // Assert
        var update = Assert.Single(Published<CombatUpdatedEvent>());
        Assert.Equal(CombatOutcome.Victory, update.Outcome);
        Assert.Empty(Published<CreatureVitalsChangedEvent>());
    }

    [Fact]
    public async Task Handle_LeavesCreaturesOutsideAFightAlone()
    {
        // Arrange
        var bystander = await SeedBurningEnemy(amount: 5);

        // Act
        await Sync();

        // Assert
        await using var verifyContext = db.CreateContext();
        var reloaded = await verifyContext.Creatures.FindAsync(
            [bystander.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(50, reloaded!.CurrentHp);
    }
}
