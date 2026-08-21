using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Reputations.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Commands;

[Collection("Database")]
public sealed class ResolveTheftCrimesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly ReputationOptions ConfiguredReputationOptions = new()
    {
        TheftReputationPenalty = -37,
        ApologizedTheftReputationPenalty = -9,
    };

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveTheftCrimesCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<ReputationOptions>>(
                new TestOptionsMonitor<ReputationOptions>(ConfiguredReputationOptions)
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveTheftCrimesCommandHandler>();

        _player = Builders.MakeCreature(WorldId, locationId: LocationId);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AppliesConfiguredTheftPenalty_WhenWitnessSurvivesAtTheScene()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId);
        var witness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = MakeCrime(faction.Id, TheftCrimeOutcome.Taken);
        _context.Factions.Add(faction);
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(MakeWitness(crime.Id, witness.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Resolve();

        // Assert
        await using var verifyContext = db.CreateContext();
        var reputation = await verifyContext.Reputations.SingleAsync(
            item =>
                item.CreatureId == _player.Id
                && item.TargetId == faction.Id
                && item.TargetType == ReputationTargetType.Faction,
            TestContext.Current.CancellationToken
        );
        var log = await verifyContext.ReputationLogEntries.SingleAsync(
            item => item.CreatureId == _player.Id && item.TargetId == faction.Id,
            TestContext.Current.CancellationToken
        );
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var persistedWitness = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ConfiguredReputationOptions.TheftReputationPenalty, reputation.Score);
        Assert.Equal(ConfiguredReputationOptions.TheftReputationPenalty, log.DeltaScore);
        Assert.Equal(ReputationReason.StoleFromFactionMember, log.Reason);
        Assert.Equal(CrimeResolution.Reported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Reported, persistedWitness.Resolution);
    }

    [Fact]
    public async Task Handle_AppliesNoPenalty_WhenAllWitnessesAreDead()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId);
        var witness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = MakeCrime(faction.Id, TheftCrimeOutcome.Taken);
        _context.Factions.Add(faction);
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(MakeWitness(crime.Id, witness.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Resolve();

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var persistedWitness = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(
            await verifyContext
                .Reputations.Where(item => item.CreatureId == _player.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.Empty(
            await verifyContext
                .ReputationLogEntries.Where(item => item.CreatureId == _player.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(CrimeResolution.Unreported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Dead, persistedWitness.Resolution);
    }

    [Fact]
    public async Task Handle_ReportsLivingWitnessesElsewhereAndAppliesOnePenaltyPerCrime()
    {
        var faction = Builders.MakeFaction(WorldId);
        var movedWitness = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var deadWitness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = MakeCrime(faction.Id, TheftCrimeOutcome.Taken);
        _context.Factions.Add(faction);
        _context.Creatures.AddRange(movedWitness, deadWitness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.AddRange(
            MakeWitness(crime.Id, movedWitness.Id),
            MakeWitness(crime.Id, deadWitness.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Resolve();

        await using var verifyContext = db.CreateContext();
        var log = await verifyContext.ReputationLogEntries.SingleAsync(
            entry => entry.CreatureId == _player.Id && entry.TargetId == faction.Id,
            TestContext.Current.CancellationToken
        );
        var witnesses = await verifyContext
            .CrimeWitnesses.Where(witness => witness.CrimeId == crime.Id)
            .ToDictionaryAsync(
                witness => witness.CreatureId,
                witness => witness.Resolution,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(ConfiguredReputationOptions.TheftReputationPenalty, log.DeltaScore);
        Assert.Equal(CrimeWitnessResolution.Reported, witnesses[movedWitness.Id]);
        Assert.Equal(CrimeWitnessResolution.Dead, witnesses[deadWitness.Id]);
    }

    [Fact]
    public async Task Handle_AppliesConfiguredReducedPenalty_WhenThePlayerApologized()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId);
        var witness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = MakeCrime(faction.Id, TheftCrimeOutcome.Apologized);
        _context.Factions.Add(faction);
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(MakeWitness(crime.Id, witness.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Resolve();

        // Assert
        await using var verifyContext = db.CreateContext();
        var reputation = await verifyContext.Reputations.SingleAsync(
            item => item.CreatureId == _player.Id && item.TargetId == faction.Id,
            TestContext.Current.CancellationToken
        );
        var log = await verifyContext.ReputationLogEntries.SingleAsync(
            item => item.CreatureId == _player.Id && item.TargetId == faction.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            ConfiguredReputationOptions.ApologizedTheftReputationPenalty,
            reputation.Score
        );
        Assert.Equal(ConfiguredReputationOptions.ApologizedTheftReputationPenalty, log.DeltaScore);
    }

    [Fact]
    public async Task Handle_AggregatesPenaltiesAndLogsOncePerFaction_WhenMultipleCrimesAreReported()
    {
        // Arrange
        var firstFaction = Builders.MakeFaction(WorldId);
        var secondFaction = Builders.MakeFaction(WorldId);
        var crimes = new[]
        {
            MakeCrime(firstFaction.Id, TheftCrimeOutcome.Taken),
            MakeCrime(firstFaction.Id, TheftCrimeOutcome.Apologized),
            MakeCrime(secondFaction.Id, TheftCrimeOutcome.Resisted),
        };
        var witnesses = crimes
            .Select(crime => Builders.MakeCreature(WorldId, locationId: LocationId))
            .ToArray();

        _context.Factions.AddRange(firstFaction, secondFaction);
        _context.Crimes.AddRange(crimes);
        _context.Creatures.AddRange(witnesses);
        _context.CrimeWitnesses.AddRange(
            crimes.Select((crime, index) => MakeWitness(crime.Id, witnesses[index].Id))
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Resolve();

        // Assert
        await using var verifyContext = db.CreateContext();
        var reputations = await verifyContext
            .Reputations.Where(item => item.CreatureId == _player.Id)
            .ToDictionaryAsync(
                item => item.TargetId,
                item => item.Score,
                TestContext.Current.CancellationToken
            );
        var logs = await verifyContext
            .ReputationLogEntries.Where(item => item.CreatureId == _player.Id)
            .ToDictionaryAsync(
                item => item.TargetId,
                item => item,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(2, reputations.Count);
        Assert.Equal(
            ConfiguredReputationOptions.TheftReputationPenalty
                + ConfiguredReputationOptions.ApologizedTheftReputationPenalty,
            reputations[firstFaction.Id]
        );
        Assert.Equal(
            ConfiguredReputationOptions.TheftReputationPenalty,
            reputations[secondFaction.Id]
        );
        Assert.Equal(2, logs.Count);
        Assert.Equal(
            ConfiguredReputationOptions.TheftReputationPenalty
                + ConfiguredReputationOptions.ApologizedTheftReputationPenalty,
            logs[firstFaction.Id].DeltaScore
        );
        Assert.Equal(
            ConfiguredReputationOptions.TheftReputationPenalty,
            logs[secondFaction.Id].DeltaScore
        );
        Assert.All(
            logs.Values,
            log => Assert.Equal(ReputationReason.StoleFromFactionMember, log.Reason)
        );
    }

    private TheftCrime MakeCrime(Guid factionId, TheftCrimeOutcome outcome) =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            OwnerFactionId = factionId,
            OwnerCreatureId = Guid.NewGuid(),
            OwnerName = "Mara",
            Outcome = outcome,
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
        };

    private static CrimeWitness MakeWitness(Guid crimeId, Guid creatureId) =>
        new()
        {
            WorldId = WorldId,
            CrimeId = crimeId,
            CreatureId = creatureId,
        };

    private Task Resolve() =>
        _handler.Handle(
            new ResolveTheftCrimesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = LocationId,
            },
            TestContext.Current.CancellationToken
        );

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
