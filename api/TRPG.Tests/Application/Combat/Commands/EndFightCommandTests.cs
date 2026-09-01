using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Results;
using TRPG.Application.Crimes.Events;
using TRPG.Data;
using TRPG.Domain.Models;
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

    private async Task<FightEncounter> SeedFight()
    {
        var fight = new FightEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = _player.LocationId,
            CombatantIds = [_player.Id, _enemy.Id],
        };
        _context.Encounters.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return fight;
    }

    [Fact]
    public async Task Handle_RecordsLivingBystandersAsKillWitnesses_WhenPlayerKillsAnEnemy()
    {
        // Arrange
        var bystander = Builders.MakeCreature(WorldId, locationId: _player.LocationId);
        _context.Creatures.Add(bystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
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
        var crime = await verifyContext
            .Crimes.OfType<KillCrime>()
            .SingleAsync(item => item.VictimId == _enemy.Id, TestContext.Current.CancellationToken);
        var witness = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_enemy.Id, crime.VictimId);
        Assert.Equal(bystander.Id, witness.CreatureId);
        Assert.Equal(crime.Id, witness.CrimeId);
        Assert.Contains(
            _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents,
            gameEvent => gameEvent == new CrimeWitnessedEvent(CrimeKind.Killing)
        );
    }

    [Fact]
    public async Task Handle_SetsWitnessesToAlerted_WhenPlayerKillsAnEnemy()
    {
        // Arrange
        var bystander = Builders.MakeCreature(WorldId, locationId: _player.LocationId);
        _context.Creatures.Add(bystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
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
        var updatedBystander = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == bystander.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Alerted, updatedBystander.State);
    }

    [Fact]
    public async Task Handle_ExcludesNonHumanoidCreatures_FromKillWitnesses()
    {
        // Arrange
        var beastBystander = Builders.MakeCreature(
            WorldId,
            locationId: _player.LocationId,
            creatureType: CreatureType.Beast
        );
        _context.Creatures.Add(beastBystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
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
        Assert.Empty(
            verifyContext.CrimeWitnesses.Where(witness => witness.CreatureId == beastBystander.Id)
        );
    }

    [Fact]
    public async Task Handle_IncludesSurvivingHumanoidCombatants_AsKillWitnesses()
    {
        // Arrange - a second enemy in the same fight survives and should witness the other's death
        var survivor = Builders.MakeCreature(WorldId, locationId: _player.LocationId);
        _context.Creatures.Add(survivor);
        var fight = new FightEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = _player.LocationId,
            CombatantIds = [_player.Id, _enemy.Id, survivor.Id],
        };
        _context.Encounters.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
                MakeCombatantState(_enemy.Id, isPlayer: false, currentHp: 0, isAlive: false),
                MakeCombatantState(survivor.Id, isPlayer: false, currentHp: 10, isAlive: true),
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
        var crime = await verifyContext
            .Crimes.OfType<KillCrime>()
            .SingleAsync(item => item.VictimId == _enemy.Id, TestContext.Current.CancellationToken);
        var witnessIds = await verifyContext
            .CrimeWitnesses.Where(w => w.CrimeId == crime.Id)
            .Select(w => w.CreatureId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains(survivor.Id, witnessIds);
    }

    [Fact]
    public async Task Handle_ExcludesSleepingCreatures_FromKillWitnesses()
    {
        // Arrange
        var sleepingBystander = Builders.MakeCreature(
            WorldId,
            locationId: _player.LocationId,
            state: CreatureState.Sleeping
        );
        _context.Creatures.Add(sleepingBystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedFight();
        var state = Builders.MakeCombatState(
            CombatOutcome.Victory,
            [
                MakeCombatantState(_player.Id, isPlayer: true, currentHp: 35, isAlive: true),
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
        Assert.Empty(
            verifyContext.CrimeWitnesses.Where(witness =>
                witness.CreatureId == sleepingBystander.Id
            )
        );
    }

    private CombatantResult MakeCombatantState(
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

    [Theory]
    [InlineData(CombatOutcome.Victory)]
    [InlineData(CombatOutcome.Defeat)]
    public async Task Handle_NeverChangesPlayerGold(CombatOutcome outcome)
    {
        // Arrange — gold now stays on the corpse; looting it is a separate, explicit action
        await SeedFight();
        var state = Builders.MakeCombatState(
            outcome,
            [
                MakeCombatantState(
                    _player.Id,
                    isPlayer: true,
                    currentHp: outcome == CombatOutcome.Victory ? 35 : 0,
                    isAlive: outcome == CombatOutcome.Victory
                ),
                MakeCombatantState(
                    _enemy.Id,
                    isPlayer: false,
                    currentHp: outcome == CombatOutcome.Victory ? 0 : 20,
                    isAlive: outcome != CombatOutcome.Victory
                ),
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
        var playerGold = await verifyContext
            .Items.OfType<Gold>()
            .Where(i => i.Ownership.OwnerId == _player.Id)
            .Select(i => (int?)i.Quantity)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, playerGold ?? 0);
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
        var updatedFight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.Id == fight.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedFight.CompletedAt);
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
