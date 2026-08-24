using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class EvaluateGuardEncounterCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateGuardEncounterCommandHandler _handler = null!;
    private readonly Location _location = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Creature _player = Builders.MakeCreature(WorldId, level: 1);
    private readonly Faction _cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);

    private ServiceProvider BuildServiceProvider(float encounterChance)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["EncounterChance"] = encounterChance.ToString(CultureInfo.InvariantCulture),
                }
            )
            .Build();

        return new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<GuardEncounterOptions>(configuration)
            .BuildServiceProvider();
    }

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player.LocationId = _location.Id;
        _context.Locations.Add(_location);
        _context.Creatures.Add(_player);
        _context.Factions.Add(_cityFaction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedGuard(int reputationScore)
    {
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: _location.Id
        );
        _context.Creatures.Add(guard);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, _cityFaction.Id, guard.Id));
        _context.Reputations.Add(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                Score = reputationScore,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return guard;
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoGuardIsAtTheLocation()
    {
        // Arrange
        _serviceProvider = BuildServiceProvider(encounterChance: 1f);
        _handler = _serviceProvider.GetRequiredService<EvaluateGuardEncounterCommandHandler>();

        // Act
        var result = await _handler.Handle(
            new EvaluateGuardEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenReputationIsAboveTheThreshold()
    {
        // Arrange
        await SeedGuard(reputationScore: 0);
        _serviceProvider = BuildServiceProvider(encounterChance: 1f);
        _handler = _serviceProvider.GetRequiredService<EvaluateGuardEncounterCommandHandler>();

        // Act
        var result = await _handler.Handle(
            new EvaluateGuardEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheEncounterChanceRollFails()
    {
        // Arrange
        await SeedGuard(reputationScore: -50);
        _serviceProvider = BuildServiceProvider(encounterChance: 0f);
        _handler = _serviceProvider.GetRequiredService<EvaluateGuardEncounterCommandHandler>();

        // Act
        var result = await _handler.Handle(
            new EvaluateGuardEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CreatesAndReturnsAGuardEncounter_WhenReputationIsLowAndTheRollSucceeds()
    {
        // Arrange
        var guard = await SeedGuard(reputationScore: -50);
        _serviceProvider = BuildServiceProvider(encounterChance: 1f);
        _handler = _serviceProvider.GetRequiredService<EvaluateGuardEncounterCommandHandler>();

        // Act
        var result = await _handler.Handle(
            new EvaluateGuardEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(guard.Id, result.GuardCreatureId);
        Assert.Equal(_cityFaction.Id, result.CityFactionId);
        Assert.Equal(-50, result.ReputationScore);
        Assert.Equal(250, result.FineAmount);
        Assert.Equal(24, result.JailHours);

        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext
            .Encounters.OfType<GuardEncounter>()
            .SingleAsync(e => e.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EncounterState.Active, persisted.State);
    }

    [Fact]
    public async Task Handle_ExcludesQuestCompletionEntries_FromRecentOffenses()
    {
        // Arrange — a positive-delta reputation gain should never surface as an "offense"
        await SeedGuard(reputationScore: -50);
        _context.ReputationLogEntries.AddRange(
            new ReputationLogEntry
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = -100,
                Reason = ReputationReason.KilledFactionMember,
                Detail = "Killed a guard",
            },
            new ReputationLogEntry
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 50,
                Reason = ReputationReason.QuestCompleted,
                Detail = "Completed quest: Clean up the docks",
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _serviceProvider = BuildServiceProvider(encounterChance: 1f);
        _handler = _serviceProvider.GetRequiredService<EvaluateGuardEncounterCommandHandler>();

        // Act
        var result = await _handler.Handle(
            new EvaluateGuardEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Killed a guard", Assert.Single(result.RecentOffenses));
    }

    [Fact]
    public async Task Handle_MapsReasonToOffenseText_WhenNoDetailWasRecorded()
    {
        // Arrange — a real kill penalty never sets Detail; the offense text must not fall back
        // to the raw enum name
        await SeedGuard(reputationScore: -50);
        _context.ReputationLogEntries.Add(
            new ReputationLogEntry
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = -100,
                Reason = ReputationReason.KilledFactionMember,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _serviceProvider = BuildServiceProvider(encounterChance: 1f);
        _handler = _serviceProvider.GetRequiredService<EvaluateGuardEncounterCommandHandler>();

        // Act
        var result = await _handler.Handle(
            new EvaluateGuardEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Killed a local", Assert.Single(result.RecentOffenses));
    }
}
