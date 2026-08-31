using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetEffectiveReputationsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AdjustReputationsCommandHandler _adjustReputations = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Guid _creatureId;
    private Faction _faction = null!;
    private GetEffectiveReputationsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _adjustReputations = _serviceProvider.GetRequiredService<AdjustReputationsCommandHandler>();
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

        await _adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments = [new ReputationAdjustment(_faction.Id, 5)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments = [new ReputationAdjustment(factionB.Id, 20)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments = [new ReputationAdjustment(npcA.Id, 3)],
                TargetType = ReputationTargetType.Creature,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

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
