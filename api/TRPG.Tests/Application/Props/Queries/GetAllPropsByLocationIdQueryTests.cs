using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

[Collection("Database")]
public sealed class GetAllPropsByLocationIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetAllPropsByLocationIdQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetAllPropsByLocationIdQueryHandler>();

        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNonConnectorProps()
    {
        // Arrange
        var room = Builders.MakeRoom(_building.Id);
        _context.Rooms.Add(room);

        var prop = new Seat
        {
            LocationId = room.LocationId,
            Name = $"Prop-{Guid.NewGuid():N}",
            Description = "A test prop",
        };
        var connector = Builders.MakeLocationConnector(room.LocationId);
        _context.Props.Add(prop);
        _context.LocationConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetAllPropsByLocationIdQuery { LocationId = room.LocationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(result);
        Assert.Equal(prop.Id, result.First().Id);
    }
}
