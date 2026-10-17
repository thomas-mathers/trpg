using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Fights;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters;

public sealed class HeldFightLifecycleCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Point PlayerPosition = new(0, 0);
    private static readonly Point InReach = new(0, -2);
    private static readonly Point OutOfReach = new(0, -30);
    private static readonly GameInstant Later = TestTime.Start + TimeSpan.FromSeconds(10);

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Creature _player = null!;
    private Creature _wolf = null!;
    private ActiveFight _fight = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CombatOptions>>(
                new TestOptionsSnapshot<CombatOptions>(
                    new CombatOptions
                    {
                        MinHitChance = 1.0f,
                        MaxHitChance = 1.0f,
                        CritChancePerDexterityPoint = 0f,
                    }
                )
            )
            .BuildServiceProvider();

        _player = await SeedKnight();
        _wolf = await SeedWolf();
        _fight = await OpenFight();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedKnight()
    {
        var generated = Builders
            .MakeCreatureGenerator()
            .Generate(
                new CreatureGeneratorInput(
                    CreatureType.Human,
                    CreatureArchetype.For(Profession.Knight),
                    _worldId,
                    _locationId,
                    MinLevel: 1,
                    MaxLevel: 1
                )
            );
        generated.Creature.LocationId = _locationId;
        _context.Creatures.Add(generated.Creature);
        _context.Items.AddRange(generated.Items);
        _context.CreatureSkills.AddRange(generated.Skills);
        _context.Worlds.Add(Builders.MakeWorld(_worldId));
        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: _worldId, id: _stateId));
        _context.Locations.Add(Builders.MakeLocation(_worldId, _stateId, id: _locationId));
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, generated.Creature.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
        return generated.Creature;
    }

    private async Task<Creature> SeedWolf()
    {
        var wolf = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: _locationId,
            name: "Wolf",
            currentHp: 1000
        );
        _context.Creatures.Add(wolf);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
        return wolf;
    }

    private async Task<ActiveFight> OpenFight()
    {
        var opened = await _serviceProvider
            .GetRequiredService<ICommandHandler<CastPlayerAbilityCommand, AbilityCastResult>>()
            .Handle(
                new CastPlayerAbilityCommand
                {
                    WorldId = _worldId,
                    PlayerId = _player.Id,
                    TargetId = _wolf.Id,
                    AbilityName = "Strike",
                    GameTime = TestTime.Start,
                    Fight = null,
                    PlayerPlacement = new PlayerPlacement(PlayerPosition, 0),
                    FindLiveCreaturePosition = _ => InReach,
                },
                TestContext.Current.CancellationToken
            );
        _context.ChangeTracker.Clear();
        return opened.Fight!;
    }

    private TickFightCommand TickAt(
        GameInstant at,
        Point wolfPosition,
        Guid? autoAttackTargetId = null
    ) =>
        new()
        {
            Fight = _fight,
            GameTime = at,
            PlayerPlacement = new PlayerPlacement(PlayerPosition, 0),
            FindLiveCreaturePosition = _ => wolfPosition,
            AutoAttackTargetId = autoAttackTargetId,
        };

    private int WolfHp() =>
        _fight.Combatants.Single(combatant => combatant.CreatureId == _wolf.Id).CurrentHp;

    private Task<FightEncounter> ReadFight() =>
        _context
            .Encounters.AsNoTracking()
            .OfType<FightEncounter>()
            .SingleAsync(
                fight => fight.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            );

    private Task<int> PlayerHp() =>
        _context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == _player.Id)
            .Select(creature => creature.CurrentHp)
            .SingleAsync(TestContext.Current.CancellationToken);

    private Task<bool> IsWolfEngaged() =>
        _context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == _wolf.Id)
            .Select(creature => creature.IsEngaged)
            .SingleAsync(TestContext.Current.CancellationToken);

    private Task<bool> HasActiveFight() =>
        _serviceProvider
            .GetRequiredService<IQueryHandler<HasActiveFightQuery, bool>>()
            .Handle(
                new HasActiveFightQuery { PlayerId = _player.Id },
                TestContext.Current.CancellationToken
            );

    private Task Abandon() =>
        _serviceProvider
            .GetRequiredService<ICommandHandler<AbandonFightsCommand>>()
            .Handle(
                new AbandonFightsCommand { WorldId = _worldId, GameTime = Later },
                TestContext.Current.CancellationToken
            );

    [Fact]
    public async Task Tick_LetsTheEnemyStrike_WhenItIsInReach()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<
            ICommandHandler<TickFightCommand, FightTickResult>
        >();

        // Act
        var result = await handler.Handle(
            TickAt(Later, InReach),
            TestContext.Current.CancellationToken
        );

        // Assert
        var wolf = _fight.Combatants.Single(combatant => combatant.CreatureId == _wolf.Id);
        Assert.True(result.Changed);
        Assert.True(wolf.NextSwingAt > Later);
        Assert.Empty(result.Chases);
    }

    [Fact]
    public async Task Tick_OrdersAChaseOnThePlayer_WhenAnEnemyIsOutOfReach()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<
            ICommandHandler<TickFightCommand, FightTickResult>
        >();

        // Act
        var result = await handler.Handle(
            TickAt(Later, OutOfReach),
            TestContext.Current.CancellationToken
        );

        // Assert
        var chase = Assert.Single(result.Chases);
        Assert.Equal((_wolf.Id, PlayerPosition), (chase.CreatureId, chase.Target));
    }

    [Fact]
    public async Task Tick_SwingsAtTheTarget_WhenTheSwingTimerIsReady()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<
            ICommandHandler<TickFightCommand, FightTickResult>
        >();
        var hpBefore = WolfHp();

        // Act
        await handler.Handle(
            TickAt(Later, InReach, _wolf.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(WolfHp() < hpBefore);
    }

    [Fact]
    public async Task Tick_DoesNotSwing_WhenThePlayerHasNoTarget()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<
            ICommandHandler<TickFightCommand, FightTickResult>
        >();
        var hpBefore = WolfHp();

        // Act
        await handler.Handle(TickAt(Later, InReach), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(hpBefore, WolfHp());
    }

    [Fact]
    public async Task Tick_DoesNotSwing_WhenTheTargetIsOutOfReach()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<
            ICommandHandler<TickFightCommand, FightTickResult>
        >();
        var hpBefore = WolfHp();

        // Act
        await handler.Handle(
            TickAt(Later, OutOfReach, _wolf.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(hpBefore, WolfHp());
    }

    [Fact]
    public async Task Leave_CompletesTheFightAsFled()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ICommandHandler<LeaveFightCommand>>();

        // Act
        await handler.Handle(
            new LeaveFightCommand { Fight = _fight, GameTime = Later },
            TestContext.Current.CancellationToken
        );

        // Assert
        var fight = await ReadFight();
        Assert.Equal((EncounterState.Completed, CombatOutcome.Fled), (fight.State, fight.Outcome));
    }

    [Fact]
    public async Task Leave_ReleasesTheEnemy()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ICommandHandler<LeaveFightCommand>>();

        // Act
        await handler.Handle(
            new LeaveFightCommand { Fight = _fight, GameTime = Later },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(await IsWolfEngaged());
    }

    [Fact]
    public async Task Abandon_CompletesTheFightAsFled()
    {
        // Act
        await Abandon();

        // Assert
        var fight = await ReadFight();
        Assert.Equal((EncounterState.Completed, CombatOutcome.Fled), (fight.State, fight.Outcome));
    }

    [Fact]
    public async Task Abandon_RecordsNoCrimes()
    {
        // Act
        await Abandon();

        // Assert
        var crimes = await _context.Crimes.CountAsync(
            crime => crime.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, crimes);
    }

    [Fact]
    public async Task Abandon_ReleasesTheEnemy()
    {
        // Act
        await Abandon();

        // Assert
        Assert.False(await IsWolfEngaged());
    }

    [Fact]
    public async Task HasActiveFight_IsTrue_WhileTheFightIsHeld()
    {
        // Act
        var held = await HasActiveFight();

        // Assert
        Assert.True(held);
    }

    [Fact]
    public async Task HasActiveFight_IsFalse_AfterTheFightIsAbandoned()
    {
        // Arrange
        await Abandon();

        // Act
        var held = await HasActiveFight();

        // Assert
        Assert.False(held);
    }
}
