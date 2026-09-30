using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Events;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class AdvanceFightEffectsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant OneRoundIn = GameClock.Epoch + CombatTiming.Round;

    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AdvanceFightEffectsCommandHandler _handler = null!;
    private Creature _player = null!;
    private Creature _enemy = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AdvanceFightEffectsCommandHandler>();

        _player = Builders.MakeCreature(_worldId, currentHp: 50);
        _enemy = Builders.MakeCreature(_worldId, locationId: _player.LocationId, currentHp: 30);
        _context.Worlds.Add(Builders.MakeWorld(_worldId, OneRoundIn));
        _context.Creatures.AddRange(_player, _enemy);
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _player.Id));
        _context.Encounters.Add(
            new FightEncounter
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                LocationId = _player.LocationId,
                CombatantIds = [_player.Id, _enemy.Id],
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private static ActiveDot MakeDot(int amount) =>
        new()
        {
            AbilityName = "Ignite",
            Amount = amount,
            DamageType = nameof(DamageType.Fire),
            NextTickAt = OneRoundIn,
            ExpiresAt = OneRoundIn + CombatTiming.Round * 10,
        };

    private async Task SeedDot(Creature creature, int amount)
    {
        creature.ActiveDots = [MakeDot(amount)];
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task Advance(GameInstant gameTime) =>
        _handler.Handle(
            new AdvanceFightEffectsCommand
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                GameTime = gameTime,
            },
            TestContext.Current.CancellationToken
        );

    private async Task<Creature> Reload(Guid creatureId)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == creatureId,
                TestContext.Current.CancellationToken
            );
    }

    private async Task<FightEncounter> ReloadFight()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Encounters.OfType<FightEncounter>()
            .AsNoTracking()
            .SingleAsync(fight => fight.WorldId == _worldId, TestContext.Current.CancellationToken);
    }

    private CombatUpdatedEvent? PublishedCombatUpdate() =>
        _serviceProvider
            .GetRequiredService<TestGameClientEventSink>()
            .EnqueuedEvents.OfType<CombatUpdatedEvent>()
            .SingleOrDefault();

    [Fact]
    public async Task Handle_DamagesTheEnemyAndKeepsTheFightOngoing_WhenTheTickDoesNotKill()
    {
        // Arrange
        await SeedDot(_enemy, amount: 5);

        // Act
        await Advance(OneRoundIn);

        // Assert
        Assert.True((await Reload(_enemy.Id)).CurrentHp < 30);
        Assert.Equal(CombatOutcome.Ongoing, PublishedCombatUpdate()!.Outcome);
        Assert.Equal(EncounterState.Active, (await ReloadFight()).State);
    }

    [Fact]
    public async Task Handle_EndsTheFightInVictory_WhenTheTickKillsTheLastEnemy()
    {
        // Arrange
        await SeedDot(_enemy, amount: 1000);

        // Act
        await Advance(OneRoundIn);

        // Assert
        Assert.Equal(CreatureCondition.Dead, (await Reload(_enemy.Id)).Condition);
        Assert.Equal(CombatOutcome.Victory, PublishedCombatUpdate()!.Outcome);
        Assert.Equal(EncounterState.Completed, (await ReloadFight()).State);
    }

    [Fact]
    public async Task Handle_EndsTheFightInDefeat_WhenTheTickKillsThePlayer()
    {
        // Arrange
        await SeedDot(_player, amount: 1000);

        // Act
        await Advance(OneRoundIn);

        // Assert
        Assert.Equal(CreatureCondition.Dead, (await Reload(_player.Id)).Condition);
        Assert.Equal(CombatOutcome.Defeat, PublishedCombatUpdate()!.Outcome);
        Assert.Equal(EncounterState.Completed, (await ReloadFight()).State);
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenNoTickIsDue()
    {
        // Arrange
        await SeedDot(_enemy, amount: 5);

        // Act
        await Advance(GameClock.Epoch);

        // Assert
        Assert.Equal(30, (await Reload(_enemy.Id)).CurrentHp);
        Assert.Null(PublishedCombatUpdate());
    }

    [Fact]
    public async Task Handle_PersistsExpiredConditions_WhenNoDamageOrHealingTickIsDue()
    {
        // Arrange
        _enemy.ActiveConditions[nameof(ConditionType.Stunned)] = OneRoundIn;
        _enemy.CooldownReadyAtByAbility["Strike"] = OneRoundIn;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Advance(OneRoundIn);

        // Assert
        var enemy = await Reload(_enemy.Id);
        Assert.Empty(enemy.ActiveConditions);
        Assert.Empty(enemy.CooldownReadyAtByAbility);
        Assert.Equal(EncounterState.Active, (await ReloadFight()).State);
        Assert.NotNull(PublishedCombatUpdate());
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenThePlayerIsNotInAFight()
    {
        // Arrange
        var bystander = Builders.MakeCreature(_worldId);
        _context.Creatures.Add(bystander);
        await SeedDot(bystander, amount: 5);

        // Act
        await _handler.Handle(
            new AdvanceFightEffectsCommand
            {
                WorldId = _worldId,
                PlayerId = bystander.Id,
                GameTime = OneRoundIn,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(PublishedCombatUpdate());
    }
}
