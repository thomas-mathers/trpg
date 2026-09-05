using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class ResolveHostileEncounterActionCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _previousLocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveHostileEncounterActionCommandHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction(WorldId);
    private readonly GameSession _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
    private Creature _player = null!;
    private Creature _enemy = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ResolveHostileEncounterActionCommandHandler>();

        _player = Builders.MakeCreature(WorldId, previousLocationId: _previousLocationId);
        _enemy = Builders.MakeCreature(WorldId, name: "Bandit");
        _context.Creatures.AddRange(_player, _enemy);
        _context.Factions.Add(_faction);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private ResolveHostileEncounterActionCommand MakeCommand(
        HostileEncounterAction action,
        Guid encounterId
    ) =>
        new()
        {
            SessionId = _session.Id,
            WorldId = WorldId,
            PlayerId = _player.Id,
            Action = action,
            EncounterId = encounterId,
        };

    private async Task<HostileEncounter> SeedActiveEncounter()
    {
        var encounter = new HostileEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = Guid.NewGuid(),
            LocationName = "Market Square",
            FactionId = _faction.Id,
            FactionName = _faction.Name,
            Members =
            [
                new HostileEncounterMemberSnapshot(
                    _enemy.Id,
                    _enemy.Name,
                    _enemy.CreatureType,
                    _enemy.Level
                ),
            ],
        };
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return encounter;
    }

    private ResolveHostileEncounterActionCommandHandler BuildHandlerWithFleeOptions(
        float minimumCatchChance,
        float maximumCatchChance
    ) =>
        new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<FleeOptions>>(
                new TestOptionsSnapshot<FleeOptions>(
                    new FleeOptions
                    {
                        MinimumCatchChance = minimumCatchChance,
                        MaximumCatchChance = maximumCatchChance,
                    }
                )
            )
            .BuildServiceProvider()
            .GetRequiredService<ResolveHostileEncounterActionCommandHandler>();

    [Fact]
    public async Task Handle_Attack_AlertsTheEnemyAndStartsAFight()
    {
        // Arrange
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await _handler.Handle(
            MakeCommand(new AttackEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HostileEncounterResolutionOutcome.Attacked, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var updatedEnemy = await verifyContext.Creatures.FindAsync(
            [_enemy.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Alerted, updatedEnemy!.State);
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Contains(_enemy.Id, fight.CombatantIds);
        var persistedEncounter = await verifyContext.Encounters.SingleAsync(
            e => e.Id == encounter.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);
    }

    [Fact]
    public async Task Handle_Evade_ReturnsEvadedAndDoesNotStartAFight_WhenTheEscapeSucceeds()
    {
        // Arrange
        var handler = BuildHandlerWithFleeOptions(minimumCatchChance: 0f, maximumCatchChance: 0f);
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new EvadeEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HostileEncounterResolutionOutcome.Evaded, result.Outcome);
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .AnyAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_Evade_StartsAFight_WhenTheEscapeFails()
    {
        // Arrange
        var handler = BuildHandlerWithFleeOptions(minimumCatchChance: 1f, maximumCatchChance: 1f);
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new EvadeEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HostileEncounterResolutionOutcome.EvadeFailed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .AnyAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_Retreat_MovesThePlayerToThePreviousLocation_WhenTheEscapeSucceeds()
    {
        // Arrange
        var handler = BuildHandlerWithFleeOptions(minimumCatchChance: 0f, maximumCatchChance: 0f);
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new RetreatEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HostileEncounterResolutionOutcome.Retreated, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_previousLocationId, updatedPlayer!.LocationId);
    }

    [Fact]
    public async Task Handle_Retreat_StartsAFight_WhenTheEscapeFails()
    {
        // Arrange
        var handler = BuildHandlerWithFleeOptions(minimumCatchChance: 1f, maximumCatchChance: 1f);
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new RetreatEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HostileEncounterResolutionOutcome.RetreatFailed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .AnyAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(
                MakeCommand(new AttackEncounterAction(), Guid.NewGuid()),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterBelongsToAnotherWorld()
    {
        // Arrange
        var encounter = await SeedActiveEncounter();
        var command = new ResolveHostileEncounterActionCommand
        {
            SessionId = _session.Id,
            WorldId = Guid.NewGuid(),
            PlayerId = _player.Id,
            Action = new AttackEncounterAction(),
            EncounterId = encounter.Id,
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterBelongsToAnotherPlayer()
    {
        // Arrange
        var encounter = await SeedActiveEncounter();
        var command = new ResolveHostileEncounterActionCommand
        {
            SessionId = _session.Id,
            WorldId = WorldId,
            PlayerId = Guid.NewGuid(),
            Action = new AttackEncounterAction(),
            EncounterId = encounter.Id,
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ThrowsInvalidOperation_WhenTheEncounterIsAlreadyCompleted()
    {
        // Arrange
        var encounter = await SeedActiveEncounter();
        await _handler.Handle(
            MakeCommand(new AttackEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                MakeCommand(new AttackEncounterAction(), encounter.Id),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ResolvesExactlyOnce_WhenTwoConcurrentRequestsTargetTheSameEncounter()
    {
        // Arrange — two independent DbContexts/handlers simulate two concurrent hub requests
        // racing to resolve the same encounter.
        var encounter = await SeedActiveEncounter();

        await using var contextA = db.CreateContext();
        await using var contextB = db.CreateContext();
        await using var providerA = new ServiceCollection()
            .AddTrpgTestServices(contextA)
            .BuildServiceProvider();
        await using var providerB = new ServiceCollection()
            .AddTrpgTestServices(contextB)
            .BuildServiceProvider();
        var handlerA = providerA.GetRequiredService<ResolveHostileEncounterActionCommandHandler>();
        var handlerB = providerB.GetRequiredService<ResolveHostileEncounterActionCommandHandler>();
        var command = MakeCommand(new AttackEncounterAction(), encounter.Id);

        // Act
        var taskA = RunCatchingExceptions(() => handlerA.Handle(command, CancellationToken.None));
        var taskB = RunCatchingExceptions(() => handlerB.Handle(command, CancellationToken.None));
        var outcomes = await Task.WhenAll(taskA, taskB);

        // Assert — exactly one request's resolution actually took effect
        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is not null);

        await using var verifyContext = db.CreateContext();
        var fights = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .Where(f => f.PlayerId == _player.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Single(fights);
    }

    private static async Task<Exception?> RunCatchingExceptions(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    [Fact]
    public async Task Handle_RollsBackEntirely_WhenStartingTheFightFails()
    {
        // Arrange — an unseeded SessionId makes StartFightCommand's internal playtime lookup
        // throw after the enemy has already been alerted, proving the whole resolution (alerting
        // the enemy, and completing the encounter) is atomic rather than partially applied.
        var encounter = await SeedActiveEncounter();
        var command = new ResolveHostileEncounterActionCommand
        {
            SessionId = Guid.NewGuid(),
            WorldId = WorldId,
            PlayerId = _player.Id,
            Action = new AttackEncounterAction(),
            EncounterId = encounter.Id,
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        await using var verifyContext = db.CreateContext();
        var persistedEncounter = await verifyContext.Encounters.SingleAsync(
            e => e.Id == encounter.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EncounterState.Active, persistedEncounter.State);
        var enemy = await verifyContext.Creatures.FindAsync(
            [_enemy.Id],
            TestContext.Current.CancellationToken
        );
        Assert.NotEqual(CreatureState.Alerted, enemy!.State);
        Assert.False(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .AnyAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }
}
