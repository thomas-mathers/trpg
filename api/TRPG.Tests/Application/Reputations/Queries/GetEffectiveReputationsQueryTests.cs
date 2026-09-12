using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

public sealed class GetEffectiveReputationsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Guid _creatureId;
    private static readonly Guid WorldId = Guid.NewGuid();
    private Faction _faction = null!;
    private GetEffectiveReputationsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetEffectiveReputationsQueryHandler>();

        _faction = Builders.MakeFaction();
        var creature = Builders.MakeCreature();
        _creatureId = creature.Id;
        _context.Factions.Add(_faction);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
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
                FactionIdsByCreature = new Dictionary<Guid, IReadOnlyList<Guid>>(),
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
            Builders.MakeFactionMember(WorldId, _faction.Id, npcA.Id),
            Builders.MakeFactionMember(WorldId, factionB.Id, npcB.Id)
        );
        _context.Reputations.AddRange(
            Builders.MakeReputation(WorldId, _creatureId, _faction.Id, score: 5),
            Builders.MakeReputation(WorldId, _creatureId, factionB.Id, score: 20),
            Builders.MakeReputation(
                WorldId,
                _creatureId,
                npcA.Id,
                ReputationTargetType.Creature,
                score: 3
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var factionIdsByCreature = new Dictionary<Guid, IReadOnlyList<Guid>>
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
