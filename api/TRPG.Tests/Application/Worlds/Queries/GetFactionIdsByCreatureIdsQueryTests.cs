using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetFactionIdsByCreatureIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetFactionIdsByCreatureIdsQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(WorldId);
    private readonly Faction _factionA = Builders.MakeFaction(WorldId);
    private readonly Faction _factionB = Builders.MakeFaction(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetFactionIdsByCreatureIdsQueryHandler>();

        _context.Creatures.Add(_creature);
        _context.Factions.AddRange(_factionA, _factionB);
        _context.FactionMembers.AddRange(
            Builders.MakeFactionMember(WorldId, _factionA.Id, _creature.Id),
            Builders.MakeFactionMember(WorldId, _factionB.Id, _creature.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsAllFactionIdsForTheCreature()
    {
        // Act
        var result = await _handler.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { _factionA.Id, _factionB.Id }.OrderBy(id => id),
            result[_creature.Id].OrderBy(id => id)
        );
    }

    [Fact]
    public async Task Handle_OmitsCreaturesWithNoFactionMemberships()
    {
        // Act
        var result = await _handler.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = [Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
