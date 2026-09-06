using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes;
using TRPG.Application.Crimes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Commands;

[Collection("Database")]
public sealed class ApplyCrimeReputationPenaltyCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ApplyCrimeReputationPenaltyCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Faction _faction = Builders.MakeFaction(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ApplyCrimeReputationPenaltyCommandHandler>();

        _context.Creatures.Add(_player);
        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SumsThePenalty_WhenSeveralCrimesWrongTheSameFaction()
    {
        // Arrange — three offences against one faction, summing below the -100 reputation floor
        var reports = new CrimeReport[]
        {
            new([_faction.Id], [], -10),
            new([_faction.Id], [], -10),
            new([_faction.Id], [], -10),
        };

        // Act
        await _handler.Handle(MakeCommand(reports), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var reputation = await verifyContext.Reputations.SingleAsync(
            r => r.CreatureId == _player.Id && r.TargetId == _faction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(-30, reputation.Score);
    }

    [Fact]
    public async Task Handle_PenalizesEachReportedWitnessPersonally()
    {
        // Arrange
        var firstWitness = Builders.MakeCreature(WorldId);
        var secondWitness = Builders.MakeCreature(WorldId);
        _context.Creatures.AddRange(firstWitness, secondWitness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var reports = new CrimeReport[] { new([], [firstWitness.Id, secondWitness.Id], -40) };

        // Act
        await _handler.Handle(MakeCommand(reports), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var scoresByTarget = await verifyContext
            .Reputations.Where(r =>
                r.CreatureId == _player.Id && r.TargetType == ReputationTargetType.Creature
            )
            .ToDictionaryAsync(
                r => r.TargetId,
                r => r.Score,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(-40, scoresByTarget[firstWitness.Id]);
        Assert.Equal(-40, scoresByTarget[secondWitness.Id]);
    }

    [Fact]
    public async Task Handle_AppliesNoFactionPenalty_WhenNoCrimeNamesAFaction()
    {
        // Arrange
        var reports = new CrimeReport[] { new([], [], -40) };

        // Act
        await _handler.Handle(MakeCommand(reports), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var hasReputation = await verifyContext.Reputations.AnyAsync(
            r => r.CreatureId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(hasReputation);
    }

    private ApplyCrimeReputationPenaltyCommand MakeCommand(
        IReadOnlyCollection<CrimeReport> reports
    ) =>
        new()
        {
            PlayerId = _player.Id,
            WorldId = WorldId,
            Reports = reports,
            FactionReason = ReputationReason.KilledFactionMember,
            WitnessReason = ReputationReason.WitnessedKilling,
        };
}
