using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

[Collection("Database")]
public sealed class GetWorkstationsByLocationIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetWorkstationsByLocationIdQueryHandler _handler = null!;
    private readonly Guid _locationId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetWorkstationsByLocationIdQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyWorkstationsAtTheRequestedLocation()
    {
        // Arrange
        var matching = Builders.MakeWorkstation(WorldId, locationId: _locationId);
        var elsewhere = Builders.MakeWorkstation(WorldId);
        var nonWorkstationProp = new Container
        {
            WorldId = WorldId,
            LocationId = _locationId,
            Name = "A chest",
            Description = "A test container",
        };
        _context.Props.AddRange(matching, elsewhere, nonWorkstationProp);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetWorkstationsByLocationIdQuery { LocationId = _locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var workstation = Assert.Single(result);
        Assert.Equal(matching.Id, workstation.Id);
    }
}
