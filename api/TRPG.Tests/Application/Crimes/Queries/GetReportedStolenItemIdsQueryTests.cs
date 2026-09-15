using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Queries;

public sealed class GetReportedStolenItemIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetReportedStolenItemIdsQueryHandler _handler = null!;
    private readonly Guid _playerId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetReportedStolenItemIdsQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private TheftCrime MakeTheftCrime(
        CrimeResolution resolution,
        Guid itemId,
        Guid? playerId = null
    ) =>
        new()
        {
            WorldId = WorldId,
            PlayerId = playerId ?? _playerId,
            OwnerCreatureId = Guid.NewGuid(),
            OwnerName = "Victim",
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Creature,
            Resolution = resolution,
            Items = [new TheftCrimeItem(itemId, "Recovered Item", 1)],
        };

    [Fact]
    public async Task Handle_ReturnsOnlyItemIds_FromReportedTheftsByThisPlayer()
    {
        // Arrange
        var reportedItemId = Guid.NewGuid();
        var unreportedItemId = Guid.NewGuid();
        var pendingItemId = Guid.NewGuid();
        var otherPlayerItemId = Guid.NewGuid();
        _context.Crimes.AddRange(
            MakeTheftCrime(CrimeResolution.Reported, reportedItemId),
            MakeTheftCrime(CrimeResolution.Unreported, unreportedItemId),
            MakeTheftCrime(CrimeResolution.Pending, pendingItemId),
            MakeTheftCrime(CrimeResolution.Reported, otherPlayerItemId, Guid.NewGuid())
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetReportedStolenItemIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
                ItemIds = [reportedItemId, unreportedItemId, pendingItemId, otherPlayerItemId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([reportedItemId], result);
    }

    [Fact]
    public async Task Handle_IgnoresReportedItems_NotInTheRequestedSet()
    {
        // Arrange
        var reportedItemId = Guid.NewGuid();
        _context.Crimes.Add(MakeTheftCrime(CrimeResolution.Reported, reportedItemId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetReportedStolenItemIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
                ItemIds = [Guid.NewGuid()],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
