using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Queries;

public sealed class GetEffectiveReputationQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private Guid _creatureId;
    private static readonly Guid WorldId = Guid.NewGuid();
    private GetEffectiveReputationQueryHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
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
            Builders.MakeFactionMember(WorldId, _faction.Id, npc.Id),
            Builders.MakeFactionMember(WorldId, guildFaction.Id, npc.Id)
        );
        _context.Reputations.AddRange(
            Builders.MakeReputation(WorldId, _creatureId, _faction.Id, score: 5),
            Builders.MakeReputation(WorldId, _creatureId, guildFaction.Id, score: 10),
            Builders.MakeReputation(
                WorldId,
                _creatureId,
                npc.Id,
                ReputationTargetType.Creature,
                score: 3
            )
        );
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
        Assert.Equal(18, result);
    }
}
