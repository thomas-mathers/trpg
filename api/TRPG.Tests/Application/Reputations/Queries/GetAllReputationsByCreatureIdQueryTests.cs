using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetAllReputationsByCreatureIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AdjustReputationsCommandHandler _adjustReputations = null!;
    private TrpgDbContext _context = null!;
    private GetAllReputationsByCreatureIdQueryHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllReputationsByCreatureIdQueryHandler(_context);
        _adjustReputations = new AdjustReputationsCommandHandler(_context);

        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsReputationsForCreature()
    {
        // Arrange
        var creature = Builders.MakeCreature();
        var faction2 = Builders.MakeFaction();
        _context.Creatures.Add(creature);
        _context.Factions.Add(faction2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var creatureId = creature.Id;

        await _adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 5)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = creatureId,
                Adjustments = [new ReputationAdjustment(faction2.Id, 10)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _handler.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(
            result,
            r =>
                r.TargetId == _faction.Id
                && r.TargetType == ReputationTargetType.Faction
                && r.Score == 5
        );
        Assert.Contains(
            result,
            r =>
                r.TargetId == faction2.Id
                && r.TargetType == ReputationTargetType.Faction
                && r.Score == 10
        );
    }
}
