using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

public sealed class GetActivatedTriggerIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetActivatedTriggerIdsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetActivatedTriggerIdsQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheActivatedTriggers_ForTheGivenWorld()
    {
        // Arrange
        var activatedTrigger = Builders.MakeTrigger(worldId: WorldId, isActivated: true);
        var unactivatedTrigger = Builders.MakeTrigger(worldId: WorldId);
        var activatedTriggerInAnotherWorld = Builders.MakeTrigger(isActivated: true);
        _context.Props.AddRange(
            activatedTrigger,
            unactivatedTrigger,
            activatedTriggerInAnotherWorld
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetActivatedTriggerIdsQuery { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([activatedTrigger.Id], result);
    }
}
