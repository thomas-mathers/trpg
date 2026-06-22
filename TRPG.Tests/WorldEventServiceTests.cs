using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class WorldEventServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private WorldEvent _event = null!;
    private WorldEventService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new WorldEventService(_context);

        _event = Builders.MakeWorldEvent(_worldId);
        await _service.Add(_event);
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Add_PersistsWorldEvent() {
        // Arrange
        var worldEvent = Builders.MakeWorldEvent(_worldId);

        // Act
        await _service.Add(worldEvent, TestContext.Current.CancellationToken);

        // Assert
        var found = await _service.GetById(worldEvent.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal(worldEvent.Id, found.Id);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsEvent_WhenExists() {
        // Act
        var result = await _service.GetById(_event.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_event.Id, result.Id);
    }

    [Fact]
    public async Task GetAllByRegion_ReturnsEventsWithinRadius() {
        // Arrange
        var near = Builders.MakeWorldEvent(_worldId, new Point(1, 1));
        var far = Builders.MakeWorldEvent(_worldId, new Point(1000, 1000));
        var otherWorld = Builders.MakeWorldEvent(Guid.NewGuid(), new Point(0, 0));
        await _service.Add(near, TestContext.Current.CancellationToken);
        await _service.Add(far, TestContext.Current.CancellationToken);
        await _service.Add(otherWorld, TestContext.Current.CancellationToken);

        var searchRegion = new Circle {
            Center = new Location { Coordinates = new Point(0, 0) },
            Radius = 50
        };

        // Act
        var result = await _service.GetAllByRegion(_worldId, searchRegion, TestContext.Current.CancellationToken);

        // Assert — _event (at 0,0) and near (at 1,1) are within radius 50; far and otherWorld are not
        Assert.Contains(result, e => e.Id == _event.Id);
        Assert.Contains(result, e => e.Id == near.Id);
        Assert.DoesNotContain(result, e => e.Id == far.Id);
        Assert.DoesNotContain(result, e => e.Id == otherWorld.Id);
    }
}