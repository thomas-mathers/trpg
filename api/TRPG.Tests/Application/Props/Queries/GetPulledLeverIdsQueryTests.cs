using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

[Collection("Database")]
public sealed class GetPulledLeverIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetPulledLeverIdsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetPulledLeverIdsQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyThePulledLevers_ForTheGivenWorld()
    {
        // Arrange
        var pulledLever = Builders.MakeLever(worldId: WorldId, isPulled: true);
        var unpulledLever = Builders.MakeLever(worldId: WorldId);
        var pulledLeverInAnotherWorld = Builders.MakeLever(isPulled: true);
        _context.Props.AddRange(pulledLever, unpulledLever, pulledLeverInAnotherWorld);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetPulledLeverIdsQuery { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([pulledLever.Id], result);
    }
}
