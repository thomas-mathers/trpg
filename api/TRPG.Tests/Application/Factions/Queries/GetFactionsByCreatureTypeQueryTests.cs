using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Factions.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Factions.Queries;

[Collection("Database")]
public sealed class GetFactionsByCreatureTypeQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetFactionsByCreatureTypeQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetFactionsByCreatureTypeQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsFactionsKeyedByCreatureType_ExcludingFactionsWithNoCreatureType()
    {
        // Arrange
        var beastFaction = Builders.MakeFaction(WorldId, creatureType: CreatureType.Beast);
        var civicFaction = Builders.MakeFaction(WorldId, creatureType: null);
        var otherWorldFaction = Builders.MakeFaction(creatureType: CreatureType.Beast);
        _context.Factions.AddRange(beastFaction, civicFaction, otherWorldFaction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetFactionsByCreatureTypeQuery { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var faction = Assert.Single(result);
        Assert.Equal(beastFaction.Id, result[CreatureType.Beast].Id);
    }
}
