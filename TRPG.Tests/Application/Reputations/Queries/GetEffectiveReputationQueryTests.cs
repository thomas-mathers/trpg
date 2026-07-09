using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

[Collection("Database")]
public sealed class GetEffectiveReputationQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AdjustReputationCommandHandler _adjustReputation = null!;
    private TrpgDbContext _context = null!;
    private Guid _creatureId;
    private Faction _faction = null!;
    private GetEffectiveReputationQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _adjustReputation = new AdjustReputationCommandHandler(_context);
        _handler = new GetEffectiveReputationQueryHandler(
            _context,
            new GetEffectiveReputationsQueryHandler(_context)
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
                TargetId = guildFaction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 10,
            },
            TestContext.Current.CancellationToken
        );
        await _adjustReputation.Handle(
            new AdjustReputationCommand
            {
                CreatureId = _creatureId,
                TargetId = npc.Id,
                TargetType = ReputationTargetType.Creature,
                DeltaScore = 3,
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
