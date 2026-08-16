using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

[Collection("Database")]
public sealed class ResolveCombatRoundCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveCombatRoundCommandHandler _handler = null!;
    private Guid _sessionId;
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

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveCombatRoundCommandHandler>();

        var session = Builders.MakeGameSession(WorldId, _player.Id, TimeSpan.FromHours(1));
        _context.Creatures.AddRange(_player, _enemy);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _sessionId = session.Id;
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
        bool isAlive,
        IReadOnlyDictionary<Guid, int>? itemsUsedCounts = null
    ) =>
        Builders.MakeCombatantState(
            id,
            isPlayer ? _player.Name : _enemy.Name,
            isPlayer,
            currentHp,
            isAlive,
            itemsUsedCounts
        );

    private Combatant MakePlayerCombatant(int currentHp = 30) =>
        Builders.MakeCombatant(_player.Id, name: _player.Name, currentHp: currentHp);

    private Combatant MakeEnemyCombatant(int currentHp = 0) =>
        Builders.MakeCombatant(_enemy.Id, name: _enemy.Name, isPlayer: false, currentHp: currentHp);

    [Fact]
    public async Task Handle_PersistsCombatantState_Always()
    {
        // Arrange
        await SeedFight();
        var playerCombatant = MakePlayerCombatant(currentHp: 33);
        var state = Builders.MakeCombatState(
            CombatOutcome.Ongoing,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ]
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
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
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Ongoing,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ],
            weaponSwingCounts: new Dictionary<WeaponType, int> { [WeaponType.Sword] = 1 }
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
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
                p.WorldId == WorldId
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
        var fight = await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ],
            goldLooted: 50
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
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
    public async Task Handle_EnqueuesCombatUpdatedEvent_WithTheOutcome_WhenFightEnds()
    {
        // Arrange
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
            ]
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                PlayerId = _player.Id,
                Combatants = [MakePlayerCombatant(), MakeEnemyCombatant()],
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        var combatUpdated = Assert.Single(gameEvents.EnqueuedEvents.OfType<CombatUpdatedEvent>());
        Assert.Equal(CombatOutcome.Victory, combatUpdated.Outcome);
    }

    [Fact]
    public async Task Handle_LeavesFightOngoing_WhenOutcomeIsOngoing()
    {
        // Arrange
        var fight = await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Ongoing,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ]
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
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
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Ongoing,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ]
        );

        // Act
        var result = await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
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

    [Fact]
    public async Task Handle_EnqueuesCombatUpdatedEvent_WithCurrentCombatantState()
    {
        // Arrange
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Ongoing,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ]
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
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
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        var turnEvent = Assert.Single(gameEvents.EnqueuedEvents);
        var combatUpdated = Assert.IsType<CombatUpdatedEvent>(turnEvent);
        Assert.Equal(33, combatUpdated.Combatants.Single(c => c.IsPlayer).CurrentHp);
    }

    [Fact]
    public async Task Handle_EnqueuesCombatUpdatedEvent_WithRoundEventsMappedFromCombatState()
    {
        // Arrange
        await SeedFight();
        var hit = new Hit(
            AttackerId: _player.Id,
            AttackerName: _player.Name,
            AbilityName: "Slash",
            TargetId: _enemy.Id,
            TargetName: _enemy.Name,
            TargetRemainingHp: 12,
            TargetMaximumHp: 30,
            Killed: false,
            IsCritical: true,
            Damage: 18,
            DamageType: TRPG.Domain.Models.DamageType.Physical,
            AppliedConditions: []
        );
        var state = Builders.MakeCombatState(
            CombatOutcome.Ongoing,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 33, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ],
            events: [hit]
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
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
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        var turnEvent = Assert.Single(gameEvents.EnqueuedEvents);
        var combatUpdated = Assert.IsType<CombatUpdatedEvent>(turnEvent);
        var mappedHit = Assert.IsType<TRPG.Contracts.Combat.Responses.CombatHitEvent>(
            Assert.Single(combatUpdated.Events)
        );
        Assert.Equal(_player.Id, mappedHit.AttackerId);
        Assert.Equal(_enemy.Id, mappedHit.TargetId);
        Assert.True(mappedHit.IsCritical);
    }

    [Fact]
    public async Task Handle_DepletesInventoryItem_WhenACombatantUsedOne()
    {
        // Arrange
        await SeedFight();
        var potion = Builders.MakeConsumableItem(WorldId);
        potion.Quantity = 2;
        potion.Ownership.OwnerId = _player.Id;
        potion.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(potion);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var state = Builders.MakeCombatState(
            CombatOutcome.Ongoing,
            [
                MakeCombatantState(
                    _player.Id,
                    isPlayer: true,
                    currentHp: 33,
                    isAlive: true,
                    itemsUsedCounts: new Dictionary<Guid, int> { [potion.Id] = 1 }
                ),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 12, isAlive: true),
            ]
        );

        // Act
        await _handler.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = _sessionId,
                WorldId = WorldId,
                PlayerId = _player.Id,
                Combatants = [MakePlayerCombatant(), MakeEnemyCombatant(currentHp: 12)],
                State = state,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedPotion = await verifyContext.Items.SingleAsync(
            i => i.Id == potion.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, updatedPotion.Quantity);
    }
}
