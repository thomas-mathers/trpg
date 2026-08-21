using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetEffectiveReputationQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AdjustReputationsCommandHandler _adjustReputations = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Guid _creatureId;
    private GetEffectiveReputationQueryHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _adjustReputations = _serviceProvider.GetRequiredService<AdjustReputationsCommandHandler>();
        _handler = _serviceProvider.GetRequiredService<GetEffectiveReputationQueryHandler>();

        var creature = Builders.MakeCreature();
        _context.Factions.Add(_faction);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _creatureId = creature.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsZero_WhenNoReputationHistory()
    {
        // Arrange
        var npc = Builders.MakeCreature();
        _context.Creatures.Add(npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetEffectiveReputationQuery
            {
                ObserverCreatureId = _creatureId,
                TargetCreatureId = npc.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Handle_SumsFactionAndCreaturePersonalReputation()
    {
        // Arrange — an NPC belonging to two factions, plus a personal reputation row
        var npc = Builders.MakeCreature();
        var guildFaction = Builders.MakeFaction();
        _context.Creatures.Add(npc);
        _context.Factions.Add(guildFaction);
        _context.FactionMembers.AddRange(
            new FactionMember
            {
                FactionId = _faction.Id,
                CreatureId = npc.Id,
                Role = FactionRole.Member,
            },
            new FactionMember
            {
                FactionId = guildFaction.Id,
                CreatureId = npc.Id,
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
                Adjustments = [new ReputationAdjustment(guildFaction.Id, 10)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = _creatureId,
                Adjustments = [new ReputationAdjustment(npc.Id, 3)],
                TargetType = ReputationTargetType.Creature,
                Reason = ReputationReason.QuestCompleted,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _handler.Handle(
            new GetEffectiveReputationQuery
            {
                ObserverCreatureId = _creatureId,
                TargetCreatureId = npc.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(18, result);
    }
}
