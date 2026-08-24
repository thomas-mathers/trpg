using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Reputations.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Commands;

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
        var membershipOne = Builders.MakeFactionMember(WorldId, factionOne.Id, victim.Id);
        var membershipTwo = Builders.MakeFactionMember(WorldId, factionTwo.Id, victim.Id);
        _context.Creatures.Add(victim);
        _context.Factions.AddRange(factionOne, factionTwo);
        _context.FactionMembers.AddRange(membershipOne, membershipTwo);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyReputationPenaltyForKillsCommand
            {
                KillerId = _killer.Id,
                Kills = [new KillCrimeReport(victim.Id, [])],
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
            new ApplyReputationPenaltyForKillsCommand { KillerId = _killer.Id, Kills = [] },
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
                Kills = [new KillCrimeReport(victim.Id, [])],
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
                Kills = [new KillCrimeReport(victim.Id, [witness.Id])],
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
}
