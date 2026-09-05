using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class ResolveSuspicionEncounterActionCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveSuspicionEncounterActionCommandHandler _handler = null!;
    private readonly Faction _cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);
    private readonly GameSession _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
    private Creature _player = null!;
    private Creature _guard = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<SuspicionOptions>(new ConfigurationBuilder().Build())
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ResolveSuspicionEncounterActionCommandHandler>();

        _player = Builders.MakeCreature(WorldId);
        _guard = Builders.MakeCreature(WorldId, profession: Profession.Guard);
        _context.Creatures.AddRange(_player, _guard);
        _context.Factions.Add(_cityFaction);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private ResolveSuspicionEncounterActionCommand MakeCommand(
        SuspicionEncounterAction action,
        Guid encounterId,
        Guid encounterLocationId
    ) =>
        new()
        {
            SessionId = _session.Id,
            WorldId = WorldId,
            PlayerId = _player.Id,
            Action = action,
            EncounterId = encounterId,
            GuardCreatureId = _guard.Id,
            GuardName = _guard.Name,
            CityFactionId = _cityFaction.Id,
            EncounterLocationId = encounterLocationId,
            LocationName = "Market Square",
        };

    private async Task<SuspicionEncounter> SeedActiveEncounter(Guid locationId)
    {
        var encounter = Builders.MakeSuspicionEncounter(
            WorldId,
            _player.Id,
            locationId,
            _guard.Id,
            _cityFaction.Id,
            _guard.Name
        );
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return encounter;
    }

    private ResolveSuspicionEncounterActionCommandHandler BuildHandlerWithFleeOptions(
        float minimumCatchChance,
        float maximumCatchChance
    ) =>
        new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<SuspicionOptions>(new ConfigurationBuilder().Build())
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
            .GetRequiredService<ResolveSuspicionEncounterActionCommandHandler>();

    [Fact]
    public async Task Handle_Comply_AppliesAReputationPenalty()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());

        // Act
        await _handler.Handle(
            MakeCommand(new ComplySuspicionAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var reputation = await verifyContext.Reputations.SingleAsync(
            r => r.CreatureId == _player.Id && r.TargetId == _cityFaction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(reputation.Score < 0);
    }

    [Fact]
    public async Task Handle_Comply_CompletesTheEncounter()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());

        // Act
        await _handler.Handle(
            MakeCommand(new ComplySuspicionAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Encounters.SingleAsync(
            e => e.Id == encounter.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EncounterState.Completed, persisted.State);
    }

    [Fact]
    public async Task Handle_Flee_ReturnsFledAndAppliesNoPenalty_WhenTheEscapeSucceeds()
    {
        // Arrange
        var handler = BuildHandlerWithFleeOptions(minimumCatchChance: 0f, maximumCatchChance: 0f);
        var encounter = await SeedActiveEncounter(Guid.NewGuid());

        // Act
        var fact = await handler.Handle(
            MakeCommand(new FleeSuspicionAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SuspicionEncounterResolutionOutcome.Fled, fact.Outcome);
        Assert.Null(fact.EscalatedGuardEncounterId);
        await using var verifyContext = db.CreateContext();
        Assert.Empty(
            await verifyContext
                .Reputations.Where(r => r.CreatureId == _player.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_Flee_EscalatesIntoAGuardEncounter_WhenTheEscapeFails()
    {
        // Arrange
        var handler = BuildHandlerWithFleeOptions(minimumCatchChance: 1f, maximumCatchChance: 1f);
        var encounter = await SeedActiveEncounter(Guid.NewGuid());

        // Act
        var fact = await handler.Handle(
            MakeCommand(new FleeSuspicionAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SuspicionEncounterResolutionOutcome.FleeFailed, fact.Outcome);
        Assert.NotNull(fact.EscalatedGuardEncounterId);

        await using var verifyContext = db.CreateContext();
        var guardEncounter = await verifyContext
            .Encounters.OfType<GuardEncounter>()
            .SingleAsync(
                e => e.Id == fact.EscalatedGuardEncounterId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(_guard.Id, guardEncounter.GuardCreatureId);
        Assert.Equal(EncounterState.Active, guardEncounter.State);

        var reputation = await verifyContext.Reputations.SingleAsync(
            r => r.CreatureId == _player.Id && r.TargetId == _cityFaction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(reputation.Score < 0);
    }
}
