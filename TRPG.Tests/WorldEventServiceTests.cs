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
    public async Task GetAllByState_ReturnsEventsInState() {
        // Arrange
        var stateId = Guid.NewGuid();
        var inState = Builders.MakeWorldEvent(_worldId, stateId);
        var differentState = Builders.MakeWorldEvent(_worldId, Guid.NewGuid());
        var otherWorld = Builders.MakeWorldEvent(Guid.NewGuid(), stateId);
        await _service.Add(inState, TestContext.Current.CancellationToken);
        await _service.Add(differentState, TestContext.Current.CancellationToken);
        await _service.Add(otherWorld, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllByState(_worldId, stateId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result, e => e.Id == inState.Id);
        Assert.DoesNotContain(result, e => e.Id == differentState.Id);
        Assert.DoesNotContain(result, e => e.Id == otherWorld.Id);
    }
}