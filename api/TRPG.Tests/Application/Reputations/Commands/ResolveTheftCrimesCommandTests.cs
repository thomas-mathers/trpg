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
    public async Task Handle_AppliesNoPenalty_WhenAllWitnessesWereSilenced()
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
        Assert.Equal(CrimeWitnessResolution.Silenced, persistedWitness.Resolution);
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
