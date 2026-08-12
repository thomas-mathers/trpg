using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class GetConnectorsByLocationIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetConnectorsByLocationIdQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetConnectorsByLocationIdQueryHandler(_context);

        _context.Buildings.Add(_building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyConnectors()
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
            new GetConnectorsByLocationIdQuery { LocationId = room.LocationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(result);
        Assert.Equal(connector.Id, result.First().Id);
    }
}
