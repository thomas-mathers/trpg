using TRPG.Application.Buildings.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Queries;

[Collection("Database")]
public sealed class GetConnectorsByRoomIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid StateId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetConnectorsByRoomIdQueryHandler _handler = null!;
    private readonly Building _building = Builders.MakeBuilding(StateId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetConnectorsByRoomIdQueryHandler(_context);

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
            RoomId = room.Id,
            Name = $"Prop-{Guid.NewGuid():N}",
            Description = "A test prop",
        };
        var connector = new RoomConnector
        {
            RoomId = room.Id,
            Name = "Door",
            Description = "A door.",
            DestinationRoomId = null,
        };
        _context.Props.AddRange(prop, connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetConnectorsByRoomIdQuery { RoomId = room.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(result);
        Assert.Equal(connector.Id, result.First().Id);
    }
}
