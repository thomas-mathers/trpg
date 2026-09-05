using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Commands;

[Collection("Database")]
public sealed class ApplyReputationPenaltyForKillsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ApplyReputationPenaltyForKillsCommandHandler _handler = null!;
    private readonly Creature _killer = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ApplyReputationPenaltyForKillsCommandHandler>();

        _context.Creatures.Add(_killer);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PenalizesEveryFaction_TheKilledCreatureBelongedTo()
    {
        // Arrange
        var victim = Builders.MakeCreature(WorldId);
        var factionOne = Builders.MakeFaction(WorldId);
        var factionTwo = Builders.MakeFaction(WorldId);
        _context.Creatures.Add(victim);
        _context.Factions.AddRange(factionOne, factionTwo);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForKillsCommand
            {
                KillerId = _killer.Id,
                WorldId = WorldId,
                Kills = [new KillCrimeReport(victim.Id, [factionOne.Id, factionTwo.Id], [])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = _context
            .Reputations.Where(r =>
                r.CreatureId == _killer.Id && r.TargetType == ReputationTargetType.Faction
            )
            .ToArray();
        Assert.Equal(2, reputations.Length);
        Assert.All(reputations, r => Assert.Equal(-100, r.Score));
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoCreaturesWereKilled()
    {
        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForKillsCommand
            {
                KillerId = _killer.Id,
                WorldId = WorldId,
                Kills = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_context.Reputations.Where(r => r.CreatureId == _killer.Id));
    }

    [Fact]
    public async Task Handle_AppliesNoFactionPenalty_WhenTheKilledCreatureHasNoFactions()
    {
        // Arrange
        var victim = Builders.MakeCreature(WorldId);
        _context.Creatures.Add(victim);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForKillsCommand
            {
                KillerId = _killer.Id,
                WorldId = WorldId,
                Kills = [new KillCrimeReport(victim.Id, [], [])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_context.Reputations.Where(r => r.CreatureId == _killer.Id));
    }

    [Fact]
    public async Task Handle_PenalizesEachReportedWitness_Personally()
    {
        // Arrange
        var victim = Builders.MakeCreature(WorldId);
        var witness = Builders.MakeCreature(WorldId);
        _context.Creatures.AddRange(victim, witness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForKillsCommand
            {
                KillerId = _killer.Id,
                WorldId = WorldId,
                Kills = [new KillCrimeReport(victim.Id, [], [witness.Id])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputation = _context.Reputations.Single(r =>
            r.CreatureId == _killer.Id
            && r.TargetType == ReputationTargetType.Creature
            && r.TargetId == witness.Id
        );
        Assert.Equal(-100, reputation.Score);
    }

    [Fact]
    public async Task Handle_StillPenalizesTheVictimsFactions_WhenTheVictimHasAlreadyBeenDeleted()
    {
        // Arrange — the corpse and its faction rows are gone, as they would be after cleanup
        var faction = Builders.MakeFaction(WorldId);
        _context.Factions.Add(faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForKillsCommand
            {
                KillerId = _killer.Id,
                WorldId = WorldId,
                Kills = [new KillCrimeReport(Guid.NewGuid(), [faction.Id], [])],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputation = _context.Reputations.Single(r =>
            r.CreatureId == _killer.Id
            && r.TargetType == ReputationTargetType.Faction
            && r.TargetId == faction.Id
        );
        Assert.Equal(-100, reputation.Score);
    }
}
