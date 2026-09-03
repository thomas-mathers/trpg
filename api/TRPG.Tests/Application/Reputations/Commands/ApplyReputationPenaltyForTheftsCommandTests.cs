using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Commands;

[Collection("Database")]
public sealed class ApplyReputationPenaltyForTheftsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ApplyReputationPenaltyForTheftsCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ApplyReputationPenaltyForTheftsCommandHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private TheftCrime MakeCrime(Guid? factionId, TheftCrimeOutcome outcome) =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = Guid.NewGuid(),
            OwnerFactionId = factionId,
            OwnerCreatureId = Guid.NewGuid(),
            OwnerName = "Mara",
            Outcome = outcome,
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
        };

    [Fact]
    public async Task Handle_PenalizesTheOwnersFaction_WhenTheftWasTaken()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId);
        var crime = MakeCrime(faction.Id, TheftCrimeOutcome.Taken);
        _context.Factions.Add(faction);
        _context.Crimes.Add(crime);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForTheftsCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Thefts = [new TheftCrimeReport(crime.Id, [])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputation = _context.Reputations.Single(r =>
            r.CreatureId == _player.Id
            && r.TargetType == ReputationTargetType.Faction
            && r.TargetId == faction.Id
        );
        Assert.Equal(-25, reputation.Score);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoTheftsWereReported()
    {
        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForTheftsCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Thefts = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_context.Reputations.Where(r => r.CreatureId == _player.Id));
    }

    [Fact]
    public async Task Handle_AppliesNoFactionPenalty_WhenTheStolenSourceHasNoOwnerFaction()
    {
        // Arrange
        var crime = MakeCrime(factionId: null, TheftCrimeOutcome.Taken);
        _context.Crimes.Add(crime);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForTheftsCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Thefts = [new TheftCrimeReport(crime.Id, [])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_context.Reputations.Where(r => r.CreatureId == _player.Id));
    }

    [Fact]
    public async Task Handle_PenalizesEachReportedWitness_Personally()
    {
        // Arrange
        var witness = Builders.MakeCreature(WorldId);
        var crime = MakeCrime(factionId: null, TheftCrimeOutcome.Taken);
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForTheftsCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Thefts = [new TheftCrimeReport(crime.Id, [witness.Id])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputation = _context.Reputations.Single(r =>
            r.CreatureId == _player.Id
            && r.TargetType == ReputationTargetType.Creature
            && r.TargetId == witness.Id
        );
        Assert.Equal(-25, reputation.Score);
    }

    [Fact]
    public async Task Handle_AppliesReducedPenalty_WhenThePlayerApologized()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId);
        var crime = MakeCrime(faction.Id, TheftCrimeOutcome.Apologized);
        _context.Factions.Add(faction);
        _context.Crimes.Add(crime);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForTheftsCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Thefts = [new TheftCrimeReport(crime.Id, [])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputation = _context.Reputations.Single(r =>
            r.CreatureId == _player.Id
            && r.TargetType == ReputationTargetType.Faction
            && r.TargetId == faction.Id
        );
        Assert.Equal(-10, reputation.Score);
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
            MakeCrime(secondFaction.Id, TheftCrimeOutcome.Fled),
        };
        _context.Factions.AddRange(firstFaction, secondFaction);
        _context.Crimes.AddRange(crimes);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForTheftsCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                Thefts = crimes.Select(crime => new TheftCrimeReport(crime.Id, [])).ToArray(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = _context
            .Reputations.Where(item =>
                item.CreatureId == _player.Id && item.TargetType == ReputationTargetType.Faction
            )
            .ToDictionary(item => item.TargetId, item => item.Score);
        Assert.Equal(2, reputations.Count);
        Assert.Equal(-35, reputations[firstFaction.Id]);
        Assert.Equal(-25, reputations[secondFaction.Id]);
    }
}
