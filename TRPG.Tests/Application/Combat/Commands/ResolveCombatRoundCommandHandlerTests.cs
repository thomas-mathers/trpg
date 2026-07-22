using Microsoft.EntityFrameworkCore;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class ResolveCombatRoundCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ResolveCombatRoundCommandHandler _handler = null!;
    private Guid _worldId;
    private Guid _sessionId;
    private Creature _player = null!;
    private Creature _enemy = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new ResolveCombatRoundCommandHandler(
            new PersistCombatantsCommandHandler(_context),
            new AdjustWeaponProficienciesCommandHandler(_context),
            new AdjustCreatureSkillsCommandHandler(
                _context,
                new TestOptionsSnapshot<CreatureGeneratorOptions>(new CreatureGeneratorOptions())
            ),
            new EndFightCommandHandler(
                _context,
                new ApplyCombatRewardsCommandHandler(_context),
                new UpdateCreaturesCommandHandler(_context),
                new GetPlaytimeQueryHandler(
                    _context,
                    Microsoft
                        .Extensions
                        .Logging
                        .Abstractions
                        .NullLogger<GetPlaytimeQueryHandler>
                        .Instance
                )
            )
        );

        _worldId = Guid.NewGuid();
        _player = Builders.MakeCreature(_worldId, currentHp: 50, currentAp: 10, currentMp: 5);
        _enemy = Builders.MakeCreature(_worldId, currentHp: 30, currentAp: 8, currentMp: 4);
        _context.Creatures.Add(_player);
        _context.Creatures.Add(_enemy);

        var session = new GameSession
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            Playtime = TimeSpan.FromHours(1),
        };
        _context.GameSessions.Add(session);
        _sessionId = session.Id;

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

    private Combatant MakePlayerCombatant(int currentHp = 30) =>
        Builders.MakeCombatant(_player.Id, name: _player.Name, currentHp: currentHp);

    private Combatant MakeEnemyCombatant(int currentHp = 0) =>
        Builders.MakeCombatant(_enemy.Id, name: _enemy.Name, isPlayer: false, currentHp: currentHp);

    [Fact]
    public async Task Handle_PersistsCombatantState_Always()
    {
        // Arrange
        await SeedActiveFight();
        var playerCombatant = MakePlayerCombatant(currentHp: 33);
        var state = new CombatState(
            Outcome: CombatOutcome.Ongoing,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ],
            Events: [],
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
                PlayerId = _player.Id,
                Combatants = [playerCombatant, MakeEnemyCombatant(currentHp: 12)],
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(33, updatedPlayer!.CurrentHp);
    }

    [Fact]
    public async Task Handle_AdjustsWeaponProficiencies_WhenSwingsOccurred()
    {
        // Arrange
        await SeedActiveFight();
        var state = new CombatState(
            Outcome: CombatOutcome.Ongoing,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ],
            Events: [],
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int> { [WeaponType.Sword] = 1 },
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
                PlayerId = _player.Id,
                Combatants = [MakePlayerCombatant(), MakeEnemyCombatant()],
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var proficiency = await verifyContext.CreatureWeaponProficiencies.SingleOrDefaultAsync(
            p =>
                p.WorldId == _worldId
                && p.CreatureId == _player.Id
                && p.WeaponType == WeaponType.Sword,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(proficiency);
        Assert.Equal(1, proficiency.Proficiency);
    }

    [Fact]
    public async Task Handle_EndsFight_OnVictory()
    {
        // Arrange
        var fight = await SeedActiveFight();
        var state = new CombatState(
            Outcome: CombatOutcome.Victory,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            Events: [],
            GoldLooted: 50,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
                PlayerId = _player.Id,
                Combatants = [MakePlayerCombatant(), MakeEnemyCombatant()],
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
        Assert.Equal(CombatOutcome.Victory, updatedFight.Outcome);
    }

    [Fact]
    public async Task Handle_LeavesFightOngoing_WhenOutcomeIsOngoing()
    {
        // Arrange
        var fight = await SeedActiveFight();
        var state = new CombatState(
            Outcome: CombatOutcome.Ongoing,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ],
            Events: [],
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
                PlayerId = _player.Id,
                Combatants = [MakePlayerCombatant(), MakeEnemyCombatant(currentHp: 12)],
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
        Assert.Null(updatedFight!.CompletedAt);
        Assert.Equal(CombatOutcome.Ongoing, updatedFight.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsCombatResult_MatchingState()
    {
        // Arrange
        await SeedActiveFight();
        var state = new CombatState(
            Outcome: CombatOutcome.Ongoing,
            Combatants:
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ],
            Events: [],
            GoldLooted: null,
            WeaponSwingCounts: new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

        // Act
        var result = await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = _worldId,
                PlayerId = _player.Id,
                Combatants =
                [
                    MakePlayerCombatant(currentHp: 33),
                    MakeEnemyCombatant(currentHp: 12),
                ],
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(33, result.Player.CurrentHp);
        Assert.Single(result.Enemies);
    }
}
