using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class EndFightCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private EndFightCommandHandler _handler = null!;
    private Guid _worldId;
    private Guid _sessionId;
    private Creature _player = null!;
    private Creature _enemy = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new EndFightCommandHandler(
            _context,
            new ApplyCombatRewardsCommandHandler(_context),
            new UpdateCreaturesCommandHandler(_context),
            new GetPlaytimeQueryHandler(_context, NullLogger<GetPlaytimeQueryHandler>.Instance)
        );

        _worldId = Guid.NewGuid();
        _player = Builders.MakeCreature(_worldId, currentHp: 50, currentAp: 10, currentMp: 5);
        _enemy = Builders.MakeCreature(_worldId, currentHp: 30, currentAp: 8, currentMp: 4);
        _context.Creatures.Add(_player);
        _context.Creatures.Add(_enemy);

        _session = new GameSession
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            Playtime = TimeSpan.FromHours(1),
        };
        _context.GameSessions.Add(_session);
        _sessionId = _session.Id;

        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Fight> SeedActiveFight()
    {
        var fight = new Fight
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            CombatantIds = [_player.Id, _enemy.Id],
            StartedAt = DateTime.UtcNow,
        };
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return fight;
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
    public async Task Handle_GrantsRewards_OnVictory()
    {
        // Arrange
        await SeedActiveFight();
        var state = new CombatState(
            Outcome: CombatOutcome.Victory,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            Events: [],
            GoldLooted: 50,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
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
        Assert.Equal(50, player!.Gold);
    }

    [Fact]
    public async Task Handle_DoesNotGrantRewards_OnDefeat()
    {
        // Arrange
        await SeedActiveFight();
        var state = new CombatState(
            Outcome: CombatOutcome.Defeat,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 0, isAlive: false),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 20, isAlive: true),
            ],
            Events: [],
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
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
        Assert.Equal(0, player!.Gold);
    }

    [Fact]
    public async Task Handle_MarksActiveFightCompleted()
    {
        // Arrange
        var fight = await SeedActiveFight();
        var state = new CombatState(
            Outcome: CombatOutcome.Fled,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 12, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            Events: [],
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
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
        await SeedActiveFight();
        _session.Playtime = TimeSpan.FromHours(3);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var state = new CombatState(
            Outcome: CombatOutcome.Victory,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            Events: [],
            GoldLooted: 50,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new EndFightCommand
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
        Assert.Equal(TimeSpan.FromHours(3), player!.LastRegenPlaytime);
        Assert.Equal(TimeSpan.Zero, enemy!.LastRegenPlaytime);
    }
}
