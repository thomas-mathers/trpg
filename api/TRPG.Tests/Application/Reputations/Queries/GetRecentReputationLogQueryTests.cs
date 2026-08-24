using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetRecentReputationLogQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetRecentReputationLogQueryHandler _handler = null!;
    private Guid _creatureId;
    private readonly Faction _faction = Builders.MakeFaction();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetRecentReputationLogQueryHandler(_context);

        var creature = Builders.MakeCreature();
        _context.Creatures.Add(creature);
        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _creatureId = creature.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private ReputationLogEntry AddEntry(int deltaScore, string detail, DateTime createdAt)
    {
        var entry = new ReputationLogEntry
        {
            CreatureId = _creatureId,
            TargetId = _faction.Id,
            TargetType = ReputationTargetType.Faction,
            DeltaScore = deltaScore,
            Reason = ReputationReason.QuestCompleted,
            Detail = detail,
            CreatedAt = createdAt,
            WorldId = _faction.WorldId,
        };
        _context.ReputationLogEntries.Add(entry);
        return entry;
    }

    [Fact]
    public async Task Handle_ReturnsMostRecentEntriesFirst_UpToTheLimit()
    {
        // Arrange
        var now = DateTime.UtcNow;
        AddEntry(5, "Oldest", now.AddHours(-3));
        AddEntry(-10, "Middle", now.AddHours(-2));
        AddEntry(3, "Newest", now.AddHours(-1));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = _creatureId,
                Targets = [new ReputationLogTarget(_faction.Id, ReputationTargetType.Faction)],
                Limit = 2,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(["Newest", "Middle"], result.Select(e => e.Detail));
    }

    [Fact]
    public async Task Handle_FiltersToNegativeDeltasOnly_WhenRequested()
    {
        // Arrange
        var now = DateTime.UtcNow;
        AddEntry(20, "Completed a quest", now.AddHours(-2));
        AddEntry(-30, "Killed a guard", now.AddHours(-1));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = _creatureId,
                Targets = [new ReputationLogTarget(_faction.Id, ReputationTargetType.Faction)],
                Limit = 10,
                NegativeOnly = true,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var entry = Assert.Single(result);
        Assert.Equal("Killed a guard", entry.Detail);
    }

    [Fact]
    public async Task Handle_ExcludesEntriesForOtherTargets()
    {
        // Arrange
        var otherFaction = Builders.MakeFaction();
        _context.Factions.Add(otherFaction);
        var now = DateTime.UtcNow;
        AddEntry(5, "Same faction", now);
        _context.ReputationLogEntries.Add(
            new ReputationLogEntry
            {
                CreatureId = _creatureId,
                TargetId = otherFaction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = -5,
                Reason = ReputationReason.QuestCompleted,
                Detail = "Other faction",
                CreatedAt = now,
                WorldId = otherFaction.WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = _creatureId,
                Targets = [new ReputationLogTarget(_faction.Id, ReputationTargetType.Faction)],
                Limit = 10,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var entry = Assert.Single(result);
        Assert.Equal("Same faction", entry.Detail);
    }

    [Fact]
    public async Task Handle_MergesEntriesAcrossMultipleTargets_SortedByCreatedAt()
    {
        // Arrange
        var creature = Builders.MakeCreature();
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var now = DateTime.UtcNow;
        AddEntry(5, "Faction entry", now.AddHours(-2));
        _context.ReputationLogEntries.Add(
            new ReputationLogEntry
            {
                CreatureId = _creatureId,
                TargetId = creature.Id,
                TargetType = ReputationTargetType.Creature,
                DeltaScore = -10,
                Reason = ReputationReason.WitnessedKilling,
                Detail = "Creature entry",
                CreatedAt = now.AddHours(-1),
                WorldId = creature.WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = _creatureId,
                Targets =
                [
                    new ReputationLogTarget(_faction.Id, ReputationTargetType.Faction),
                    new ReputationLogTarget(creature.Id, ReputationTargetType.Creature),
                ],
                Limit = 10,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(["Creature entry", "Faction entry"], result.Select(e => e.Detail));
    }
}
