using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class EndFightCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EndFightCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(
        WorldId,
        currentHp: 50,
        currentAp: 10,
        currentMp: 5
    );
    private readonly Creature _enemy = Builders.MakeCreature(
        WorldId,
        currentHp: 30,
        currentAp: 8,
        currentMp: 4
    );

    private GameSession _session = null!;
    private Guid _sessionId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EndFightCommandHandler>();

        _session = Builders.MakeGameSession(WorldId, _player.Id, TimeSpan.FromHours(1));
        _context.Creatures.AddRange(_player, _enemy);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _sessionId = _session.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Fight> SeedFight()
    {
        var fight = Builders.MakeFight(WorldId, _player.Id, [_player.Id, _enemy.Id]);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return fight;
    }

    private CombatantState MakeCombatantState(
        Guid id,
        bool isPlayer,
        int currentHp,
        bool isAlive
    ) =>
        Builders.MakeCombatantState(
            id,
            isPlayer ? _player.Name : _enemy.Name,
            isPlayer,
            currentHp,
            isAlive
        );

    [Fact]
    public async Task Handle_GrantsRewards_OnVictory()
    {
        // Arrange
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            goldLooted: 50
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(50, player!.Gold);
    }

    [Fact]
    public async Task Handle_DoesNotGrantRewards_OnDefeat()
    {
        // Arrange
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Defeat,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 0, isAlive: false),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 20, isAlive: true),
            ]
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, player!.Gold);
    }

    [Fact]
    public async Task Handle_MarksActiveFightCompleted()
    {
        // Arrange
        var fight = await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Fled,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 12, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ]
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                State = state,
            },
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

    [Fact]
    public async Task Handle_AdvancesLastRegenPlaytime_ForSurvivingCombatants()
    {
        // Arrange — the player survives, the enemy doesn't
        _session.Playtime = TimeSpan.FromHours(3);
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            goldLooted: 50
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        var enemy = await verifyContext.Creatures.FindAsync(
            [_enemy.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeSpan.FromHours(3), player!.LastRegenPlaytime);
        Assert.Equal(TimeSpan.Zero, enemy!.LastRegenPlaytime);
    }
}
