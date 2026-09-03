using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

[Collection("Database")]
public sealed class GetBedByLocationIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetBedByLocationIdQueryHandler _handler = null!;
    private readonly Guid _locationId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetBedByLocationIdQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheBedAtTheRequestedLocation()
    {
        // Arrange
        var matching = Builders.MakeBed(WorldId, locationId: _locationId);
        var elsewhere = Builders.MakeBed(WorldId);
        var nonBedProp = new Container
        {
            WorldId = WorldId,
            LocationId = _locationId,
            Name = "A chest",
            Description = "A test container",
        };
        _context.Props.AddRange(matching, elsewhere, nonBedProp);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetBedByLocationIdQuery { LocationId = _locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(matching.Id, result!.Id);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoBedExistsAtTheLocation()
    {
        // Act
        var result = await _handler.Handle(
            new GetBedByLocationIdQuery { LocationId = _locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
