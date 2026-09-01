using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreaturesInWorldQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCreaturesInWorldQueryHandler _handler = null!;
    private readonly Creature _inWorld = Builders.MakeCreature(WorldId);
    private readonly Creature _inOtherWorld = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCreaturesInWorldQueryHandler>();

        _context.Creatures.AddRange(_inWorld, _inOtherWorld);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyCreaturesInTheRequestedWorld()
    {
        // Act
        var result = await _handler.Handle(
            new GetCreaturesInWorldQuery { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = Assert.Single(result);
        Assert.Equal(_inWorld.Id, summary.Id);
        Assert.Equal(_inWorld.Name, summary.Name);
        Assert.Equal(_inWorld.Biography, summary.Biography);
    }
}
