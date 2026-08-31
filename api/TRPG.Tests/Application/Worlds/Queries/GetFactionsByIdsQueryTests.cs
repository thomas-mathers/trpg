using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetFactionsByIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetFactionsByIdsQueryHandler _handler = null!;
    private readonly Faction _first = Builders.MakeFaction(WorldId);
    private readonly Faction _second = Builders.MakeFaction(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetFactionsByIdsQueryHandler>();

        _context.Factions.AddRange(_first, _second);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsRequestedFactionsKeyedById()
    {
        // Act
        var result = await _handler.Handle(
            new GetFactionsByIdsQuery { Ids = [_first.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var faction = Assert.Single(result);
        Assert.Equal(_first.Id, faction.Key);
        Assert.Equal(_first.Name, faction.Value.Name);
    }
}
