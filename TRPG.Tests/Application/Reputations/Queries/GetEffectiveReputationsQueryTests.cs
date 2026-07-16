using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetEffectiveReputationsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AdjustReputationCommandHandler _adjustReputation = null!;
    private TrpgDbContext _context = null!;
    private Guid _creatureId;
    private Faction _faction = null!;
    private GetEffectiveReputationsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _adjustReputation = new AdjustReputationCommandHandler(_context);
        _handler = new GetEffectiveReputationsQueryHandler(
            _context,
            NullLogger<GetEffectiveReputationsQueryHandler>.Instance
        );

        _faction = Builders.MakeFaction();
        var creature = Builders.MakeCreature();
        _creatureId = creature.Id;
        _context.Factions.Add(_faction);
        _context.Creatures.Add(creature);
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
    public async Task Handle_ReturnsEmptyDictionary_WhenNoTargetIds()
    {
        // Act
        var result = await _handler.Handle(
            new GetEffectiveReputationsQuery
            {
                ObserverCreatureId = _creatureId,
                TargetCreatureIds = [],
                FactionIdsByCreature = new Dictionary<Guid, Guid[]>(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsDistinctScorePerTarget_WhenTargetsBelongToDifferentFactions()
    {
        // Arrange — two NPCs in different factions, each with its own personal reputation row too
        var npcA = await SeedCreature();
        var npcB = await SeedCreature();
        var factionB = Builders.MakeFaction();
        _context.Factions.Add(factionB);
        _context.FactionMembers.AddRange(
            new FactionMember
            {
                FactionId = _faction.Id,
                CreatureId = npcA.Id,
                Role = FactionRole.Member,
            },
            new FactionMember
            {
                FactionId = factionB.Id,
                CreatureId = npcB.Id,
                Role = FactionRole.Member,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _adjustReputation.Handle(
            new AdjustReputationCommand
            {
                CreatureId = _creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 5,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputation.Handle(
            new AdjustReputationCommand
            {
                CreatureId = _creatureId,
                TargetId = factionB.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 20,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputation.Handle(
            new AdjustReputationCommand
            {
                CreatureId = _creatureId,
                TargetId = npcA.Id,
                TargetType = ReputationTargetType.Creature,
                DeltaScore = 3,
            },
            TestContext.Current.CancellationToken
        );

        var factionIdsByCreature = new Dictionary<Guid, Guid[]>
        {
            [npcA.Id] = [_faction.Id],
            [npcB.Id] = [factionB.Id],
        };

        // Act
        var result = await _handler.Handle(
            new GetEffectiveReputationsQuery
            {
                ObserverCreatureId = _creatureId,
                TargetCreatureIds = [npcA.Id, npcB.Id],
                FactionIdsByCreature = factionIdsByCreature,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(8, result[npcA.Id]);
        Assert.Equal(20, result[npcB.Id]);
    }
}
