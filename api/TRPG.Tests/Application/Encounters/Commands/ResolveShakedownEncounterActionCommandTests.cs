using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class ResolveShakedownEncounterActionCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _previousLocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveShakedownEncounterActionCommandHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction(
        WorldId,
        creatureType: CreatureType.Human
    );
    private readonly GameSession _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
    private Creature _player = null!;
    private Creature _bandit = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ResolveShakedownEncounterActionCommandHandler>();

        _player = Builders.MakeCreature(WorldId, previousLocationId: _previousLocationId);
        _bandit = Builders.MakeCreature(WorldId, name: "Bandit");
        _context.Creatures.AddRange(_player, _bandit);
        _context.Factions.Add(_faction);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private ResolveShakedownEncounterActionCommand MakeCommand(
        ShakedownEncounterAction action,
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

    private async Task<ShakedownEncounter> SeedActiveEncounter(
        int tollAmount = 25,
        Guid? departureDestinationLocationId = null
    )
    {
        var encounter = new ShakedownEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = _player.LocationId,
            DepartureDestinationLocationId = departureDestinationLocationId,
            LocationName = "The Old Road",
            FactionId = _faction.Id,
            FactionName = _faction.Name,
            TollAmount = tollAmount,
            Members =
            [
                new HostileEncounterMemberSnapshot(
                    _bandit.Id,
                    _bandit.Name,
                    _bandit.CreatureType,
                    _bandit.Level
                ),
            ],
        };
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return encounter;
    }

    private ResolveShakedownEncounterActionCommandHandler BuildHandlerWithOptions(
        float minimumCatchChance = 0.05f,
        float maximumCatchChance = 0.95f,
        float minimumIntimidationSuccessChance = 0.05f,
        float maximumIntimidationSuccessChance = 0.95f
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
            .AddSingleton<IOptionsSnapshot<IntimidationOptions>>(
                new TestOptionsSnapshot<IntimidationOptions>(
                    new IntimidationOptions
                    {
                        MinimumSuccessChance = minimumIntimidationSuccessChance,
                        MaximumSuccessChance = maximumIntimidationSuccessChance,
                    }
                )
            )
            .BuildServiceProvider()
            .GetRequiredService<ResolveShakedownEncounterActionCommandHandler>();

    [Fact]
    public async Task Handle_PayToll_RemovesGoldEqualToTheTollAmount()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(tollAmount: 40);
        _context.Items.Add(
            new Gold
            {
                WorldId = WorldId,
                Name = "Gold",
                Quantity = 100,
                Ownership = new ItemOwnership
                {
                    OwnerId = _player.Id,
                    OwnerType = OwnerType.Creature,
                },
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            MakeCommand(new PayTollEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ShakedownEncounterResolutionOutcome.PaidToll, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var updatedGold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                g => g.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(60, updatedGold.Quantity);
        var persistedEncounter = await verifyContext.Encounters.SingleAsync(
            e => e.Id == encounter.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);
    }

    [Fact]
    public async Task Handle_PayToll_ResumesDeparture_WhenMovementWasInterrupted()
    {
        // Arrange
        var destination = Builders.MakeLocation(WorldId, Guid.NewGuid());
        _context.Locations.Add(destination);
        _context.LocationConnectors.Add(
            Builders.MakeLocationConnector(
                _player.LocationId,
                destinationLocationId: destination.Id,
                worldId: WorldId
            )
        );
        _context.Items.Add(
            new Gold
            {
                WorldId = WorldId,
                Name = "Gold",
                Quantity = 100,
                Ownership = new ItemOwnership
                {
                    OwnerId = _player.Id,
                    OwnerType = OwnerType.Creature,
                },
            }
        );
        var encounter = await SeedActiveEncounter(
            tollAmount: 40,
            departureDestinationLocationId: destination.Id
        );

        // Act
        var result = await _handler.Handle(
            MakeCommand(new PayTollEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ShakedownEncounterResolutionOutcome.PaidToll, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(destination.Id, player.LocationId);
        var gold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                item => item.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(60, gold.Quantity);
    }

    [Fact]
    public async Task Handle_PayToll_StaysAtEncounterLocation_WhenEncounterStartedOnArrival()
    {
        // Arrange
        _context.Items.Add(
            new Gold
            {
                WorldId = WorldId,
                Name = "Gold",
                Quantity = 100,
                Ownership = new ItemOwnership
                {
                    OwnerId = _player.Id,
                    OwnerType = OwnerType.Creature,
                },
            }
        );
        var encounter = await SeedActiveEncounter();

        // Act
        await _handler.Handle(
            MakeCommand(new PayTollEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(_player.LocationId, player.LocationId);
    }

    [Fact]
    public async Task Handle_Fight_AlertsTheBanditsAndStartsAFight()
    {
        // Arrange
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await _handler.Handle(
            MakeCommand(new FightEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ShakedownEncounterResolutionOutcome.Fought, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var updatedBandit = await verifyContext.Creatures.FindAsync(
            [_bandit.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Alerted, updatedBandit!.State);
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Contains(_bandit.Id, fight.CombatantIds);
    }

    [Fact]
    public async Task Handle_Intimidate_DoesNotStartAFight_WhenTheChanceIsGuaranteed()
    {
        // Arrange
        var handler = BuildHandlerWithOptions(
            minimumIntimidationSuccessChance: 1f,
            maximumIntimidationSuccessChance: 1f
        );
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new IntimidateEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ShakedownEncounterResolutionOutcome.Intimidated, result.Outcome);
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .AnyAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_Intimidate_StartsAFight_WhenTheChanceFails()
    {
        // Arrange
        var handler = BuildHandlerWithOptions(
            minimumIntimidationSuccessChance: 0f,
            maximumIntimidationSuccessChance: 0f
        );
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new IntimidateEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ShakedownEncounterResolutionOutcome.IntimidateFailed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Contains(_bandit.Id, fight.CombatantIds);
    }

    [Fact]
    public async Task Handle_Flee_MovesToPreviousLocationAndDoesNotStartAFight_WhenEscapeSucceeds()
    {
        // Arrange
        var handler = BuildHandlerWithOptions(minimumCatchChance: 0f, maximumCatchChance: 0f);
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new FleeShakedownEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ShakedownEncounterResolutionOutcome.Fled, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(_previousLocationId, player.LocationId);
        Assert.False(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .AnyAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_Flee_StartsAFight_WhenCaught()
    {
        // Arrange
        var handler = BuildHandlerWithOptions(minimumCatchChance: 1f, maximumCatchChance: 1f);
        var encounter = await SeedActiveEncounter();

        // Act
        var result = await handler.Handle(
            MakeCommand(new FleeShakedownEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ShakedownEncounterResolutionOutcome.FleeFailed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Contains(_bandit.Id, fight.CombatantIds);
    }
}
