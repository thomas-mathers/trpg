using TRPG.GameSessions.Hubs;

namespace TRPG.Tests.Hubs;

public class WorldConnectionRegistryTests
{
    private readonly WorldConnectionRegistry _registry = new();
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void TryAdd_ReturnsTrue_WhenNoConnectionRegisteredForWorld()
    {
        // Act
        var added = _registry.TryAdd(_worldId, "connection-1");

        // Assert
        Assert.True(added);
    }

    [Fact]
    public void TryAdd_ReturnsFalse_WhenAnotherConnectionAlreadyRegisteredForSameWorld()
    {
        // Arrange
        _registry.TryAdd(_worldId, "connection-1");

        // Act
        var added = _registry.TryAdd(_worldId, "connection-2");

        // Assert
        Assert.False(added);
    }

    [Fact]
    public void TryAdd_ReturnsTrue_ForDifferentWorlds()
    {
        // Arrange
        var otherWorldId = Guid.NewGuid();
        _registry.TryAdd(_worldId, "connection-1");

        // Act
        var added = _registry.TryAdd(otherWorldId, "connection-2");

        // Assert
        Assert.True(added);
    }

    [Fact]
    public void TryAdd_ReturnsTrue_AfterPriorConnectionRemoved()
    {
        // Arrange
        _registry.TryAdd(_worldId, "connection-1");
        _registry.Remove(_worldId, "connection-1");

        // Act
        var added = _registry.TryAdd(_worldId, "connection-2");

        // Assert
        Assert.True(added);
    }

    [Fact]
    public void Remove_DoesNotAffectRegistration_WhenConnectionIdDoesNotMatch()
    {
        // Arrange
        _registry.TryAdd(_worldId, "connection-1");

        // Act — removing with the wrong connection id must not evict the real one
        _registry.Remove(_worldId, "connection-2");
        var added = _registry.TryAdd(_worldId, "connection-3");

        // Assert
        Assert.False(added);
    }
}
