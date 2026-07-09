using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetAllReputationsByCreatureIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AdjustReputationCommandHandler _adjustReputation = null!;
    private TrpgDbContext _context = null!;
    private Faction _faction = null!;
    private GetAllReputationsByCreatureIdQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllReputationsByCreatureIdQueryHandler(_context);
        _adjustReputation = new AdjustReputationCommandHandler(_context);

        _faction = Builders.MakeFaction();
        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedCreature()
    {
        var creature = Builders.MakeCreature();
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    [Fact]
    public async Task Handle_ReturnsReputationsForCreature()
    {
        // Arrange
        var creatureId = (await SeedCreature()).Id;
        var faction2 = Builders.MakeFaction();
        _context.Factions.Add(faction2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _adjustReputation.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 5,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputation.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = faction2.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 10,
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
