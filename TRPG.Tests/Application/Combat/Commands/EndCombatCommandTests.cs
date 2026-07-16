using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Game.Queries;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class EndCombatCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private EndCombatCommandHandler _handler = null!;
    private Guid _sessionId;
    private Guid _worldId;
    private Creature _player = null!;
    private Creature _enemy = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new EndCombatCommandHandler(
            _context,
            new AdjustWeaponProficienciesCommandHandler(_context),
            new ApplyCombatRewardsCommandHandler(_context),
            new GetPlaytimeQueryHandler(_context, NullLogger<GetPlaytimeQueryHandler>.Instance),
            new PersistCombatantResourcesCommandHandler(_context),
            new ClearCombatantsCommandHandler(_context)
        );

        _worldId = Guid.NewGuid();
        _player = Builders.MakeCreature(_worldId, currentHp: 50, currentAp: 10, currentMp: 5);
        _enemy = Builders.MakeCreature(_worldId, currentHp: 30, currentAp: 8, currentMp: 4);
        _context.Creatures.Add(_player);
        _context.Creatures.Add(_enemy);

        _sessionId = Guid.NewGuid();
        _context.GameSessions.Add(
            new GameSession
            {
                Id = _sessionId,
                WorldId = _worldId,
                PlayerId = _player.Id,
                Playtime = TimeSpan.FromHours(2),
            }
        );

        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private CombatantState MakeCombatantState(Guid id, bool isPlayer, int currentHp, bool isAlive) =>
        new(
            Id: id,
            Name: isPlayer ? _player.Name : _enemy.Name,
            IsPlayer: isPlayer,
            CurrentHp: currentHp,
            MaximumHp: 100,
            CurrentAp: 7,
            CurrentMp: 2,
            IsAlive: isAlive,
            Abilities: [],
            ActiveConditions: new Dictionary<ConditionType, int>()
        );

    [Fact]
    public async Task Handle_PersistsPlayerDeadAtZeroHp_OnDefeat()
    {
        // Arrange
        var state = new CombatState(
            Outcome: CombatOutcome.Defeat,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 0, isAlive: false),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 20, isAlive: true),
            ],
            Events: [],
            XpGained: null,
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int>()
        );

        // Act
        await _handler.Handle(
            new EndCombatCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
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
        Assert.Equal(0, player!.CurrentHp);
        Assert.Equal(CreatureState.Dead, player.State);
        Assert.Equal(20, enemy!.CurrentHp);
        Assert.NotEqual(CreatureState.Dead, enemy.State);
    }

    [Fact]
    public async Task Handle_MarksEnemyDead_OnVictory()
    {
        // Arrange
        var state = new CombatState(
            Outcome: CombatOutcome.Victory,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            Events: [],
            XpGained: 100,
            GoldLooted: 50,
            WeaponSwingCounts: new Dictionary<WeaponType, int>()
        );

        // Act
        await _handler.Handle(
            new EndCombatCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
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
        Assert.Equal(35, player!.CurrentHp);
        Assert.Equal(100, player.Experience);
        Assert.Equal(50, player.Gold);
        Assert.Equal(0, enemy!.CurrentHp);
        Assert.Equal(CreatureState.Dead, enemy.State);
    }

    [Fact]
    public async Task Handle_MarksPartiallyKilledEnemyDead_OnFlee()
    {
        // Arrange — the player killed the enemy before fleeing
        var state = new CombatState(
            Outcome: CombatOutcome.Fled,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 12, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            Events: [],
            XpGained: null,
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int>()
        );

        // Act
        await _handler.Handle(
            new EndCombatCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
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
        Assert.Equal(12, player!.CurrentHp);
        Assert.Equal(CreatureState.Dead, enemy!.State);
    }
}
