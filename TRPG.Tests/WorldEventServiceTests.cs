using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class WorldEventServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private WorldEventService _service = null!;
    private WorldEvent _event = null!;
    private readonly Guid _worldId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new WorldEventService(_context);

        _event = Builders.MakeWorldEvent(_worldId);
        await _service.Add(_event);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Add_PersistsWorldEvent()
    {
        // Arrange
        var worldEvent = Builders.MakeWorldEvent(_worldId);

        // Act
        await _service.Add(worldEvent);

        // Assert
        var found = await _service.GetById(worldEvent.Id);
        Assert.NotNull(found);
        Assert.Equal(worldEvent.Id, found.Id);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsEvent_WhenExists()
    {
        // Act
        var result = await _service.GetById(_event.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_event.Id, result.Id);
    }

    [Fact]
    public async Task GetAllByRegion_ReturnsEventsWithinRadius()
    {
        // Arrange
        var near = Builders.MakeWorldEvent(_worldId, at: new Point(1, 1));
        var far = Builders.MakeWorldEvent(_worldId, at: new Point(1000, 1000));
        var otherWorld = Builders.MakeWorldEvent(Guid.NewGuid(), at: new Point(0, 0));
        await _service.Add(near);
        await _service.Add(far);
        await _service.Add(otherWorld);

        var searchRegion = new Circle
        {
            Center = new Location { WorldId = _worldId, Coordinates = new Point(0, 0) },
            Radius = 50
        };

        // Act
        var result = await _service.GetAllByRegion(searchRegion);

        // Assert — _event (at 0,0) and near (at 1,1) are within radius 50; far and otherWorld are not
        Assert.Contains(result, e => e.Id == _event.Id);
        Assert.Contains(result, e => e.Id == near.Id);
        Assert.DoesNotContain(result, e => e.Id == far.Id);
        Assert.DoesNotContain(result, e => e.Id == otherWorld.Id);
    }
}
