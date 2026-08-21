using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Commands;

[Collection("Database")]
public sealed class AdjustReputationsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Guid _creatureId;
    private GetAllReputationsByCreatureIdQueryHandler _getAllByCreatureId = null!;
    private AdjustReputationsCommandHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AdjustReputationsCommandHandler(_context);
        _getAllByCreatureId = new GetAllReputationsByCreatureIdQueryHandler(_context);

        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _creatureId = await SeedCreatureId();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedCreatureId()
    {
        var creature = Builders.MakeCreature();
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature.Id;
    }

    [Fact]
    public async Task Handle_CreatesReputation_WhenFirstCall()
    {
        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 10)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Single(reputations);
        Assert.Equal(10, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_AppliesEachAdjustment_InOneBatchedCall()
    {
        // Arrange
        var existingTargetFaction = Builders.MakeFaction();
        var newTargetFaction = Builders.MakeFaction();
        _context.Factions.AddRange(existingTargetFaction, newTargetFaction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments = [new ReputationAdjustment(existingTargetFaction.Id, 5)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments =
                [
                    new ReputationAdjustment(existingTargetFaction.Id, 10),
                    new ReputationAdjustment(newTargetFaction.Id, 12),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(15, reputations.Single(r => r.TargetId == existingTargetFaction.Id).Score);
        Assert.Equal(12, reputations.Single(r => r.TargetId == newTargetFaction.Id).Score);

        Assert.Contains(
            _context.ReputationLogEntries,
            entry => entry.TargetId == existingTargetFaction.Id && entry.DeltaScore == 10
        );
        Assert.Contains(
            _context.ReputationLogEntries,
            entry => entry.TargetId == newTargetFaction.Id && entry.DeltaScore == 12
        );
    }

    [Fact]
    public async Task Handle_AggregatesDuplicateTargetAdjustments()
    {
        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments =
                [
                    new ReputationAdjustment(_faction.Id, 7),
                    new ReputationAdjustment(_faction.Id, 5),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputation = Assert.Single(
            _context.Reputations.Where(item => item.TargetId == _faction.Id)
        );
        var log = Assert.Single(
            _context.ReputationLogEntries.Where(item => item.TargetId == _faction.Id)
        );

        Assert.Equal(12, reputation.Score);
        Assert.Equal(12, log.DeltaScore);
    }

    [Fact]
    public async Task Handle_IncrementsScore_WhenSubsequentCall()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 10)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 5)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Single(reputations);
        Assert.Equal(15, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_SupportsNegativeDelta()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 20)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, -8)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(12, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_ClampsScoreToOneHundred_WhenDeltaWouldExceedIt()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 90)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 90)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_ClampsScoreToNegativeOneHundred_WhenDeltaWouldExceedIt()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, -90)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, -90)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(-100, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_WritesALogEntry_WithTheGivenReason()
    {
        // Act
        await _handler.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, -30)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.KilledFactionMember,
                Detail = "Killed a guard",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var entry = Assert.Single(
            _context.ReputationLogEntries.Where(e => e.CreatureId == _creatureId)
        );
        Assert.Equal(_faction.Id, entry.TargetId);
        Assert.Equal(ReputationTargetType.Faction, entry.TargetType);
        Assert.Equal(-30, entry.DeltaScore);
        Assert.Equal(ReputationReason.KilledFactionMember, entry.Reason);
        Assert.Equal("Killed a guard", entry.Detail);
    }

    [Fact]
    public async Task Handle_Throws_WhenFactionTargetDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AdjustReputationsCommand
                {
                    CreatureId = _creatureId,
                    Adjustments = [new ReputationAdjustment(Guid.NewGuid(), 10)],
                    TargetType = ReputationTargetType.Faction,
                    Reason = ReputationReason.QuestCompleted,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenCreatureTargetDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AdjustReputationsCommand
                {
                    CreatureId = _creatureId,
                    Adjustments = [new ReputationAdjustment(Guid.NewGuid(), 10)],
                    TargetType = ReputationTargetType.Creature,
                    Reason = ReputationReason.QuestCompleted,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ListsEveryMissingTargetId_WhenMultipleTargetsDoNotExist()
    {
        // Arrange
        var firstMissingTargetId = Guid.NewGuid();
        var secondMissingTargetId = Guid.NewGuid();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AdjustReputationsCommand
                {
                    CreatureId = _creatureId,
                    Adjustments =
                    [
                        new ReputationAdjustment(firstMissingTargetId, 10),
                        new ReputationAdjustment(secondMissingTargetId, 20),
                    ],
                    TargetType = ReputationTargetType.Faction,
                    Reason = ReputationReason.QuestCompleted,
                },
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains(
            firstMissingTargetId.ToString(),
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            secondMissingTargetId.ToString(),
            exception.Message,
            StringComparison.Ordinal
        );
    }
}
