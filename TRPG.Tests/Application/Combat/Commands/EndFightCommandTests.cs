using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class EndFightCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
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
        _handler = new EndFightCommandHandler(
            _context,
            new ApplyCombatRewardsCommandHandler(_context),
            new UpdateCreaturesCommandHandler(_context),
            new GetPlaytimeQueryHandler(_context, NullLogger<GetPlaytimeQueryHandler>.Instance)
        );

        _context.Creatures.AddRange(_player, _enemy);
        _session = await _context.AddGameSession(
            Builders.MakeGameSession(WorldId, _player.Id, TimeSpan.FromHours(1)),
            TestContext.Current.CancellationToken
        );
        _sessionId = _session.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Fight> SeedActiveFight() =>
        await _context.AddFight(
            Builders.MakeFight(WorldId, _player.Id, [_player.Id, _enemy.Id]),
            TestContext.Current.CancellationToken
        );

    private CombatantState MakeCombatantState(
        Guid id,
        bool isPlayer,
        int currentHp,
        bool isAlive
    ) =>
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
