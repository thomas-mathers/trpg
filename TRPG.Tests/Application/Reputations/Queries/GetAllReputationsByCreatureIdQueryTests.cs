using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetAllReputationsByCreatureIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AdjustReputationCommandHandler _adjustReputation = null!;
    private TrpgDbContext _context = null!;
    private GetAllReputationsByCreatureIdQueryHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllReputationsByCreatureIdQueryHandler(_context);
        _adjustReputation = new AdjustReputationCommandHandler(_context);

        await _context.AddFaction(_faction, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedCreatureId()
    {
        var creature = await _context.AddCreature(
            Builders.MakeCreature(),
            TestContext.Current.CancellationToken
        );
        return creature.Id;
    }

    [Fact]
    public async Task Handle_ReturnsReputationsForCreature()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        var faction2 = await _context.AddFaction(
            Builders.MakeFaction(),
            TestContext.Current.CancellationToken
        );
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
