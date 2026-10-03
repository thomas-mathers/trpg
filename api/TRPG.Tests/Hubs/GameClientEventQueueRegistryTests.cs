using System.Collections.Concurrent;
using System.Reflection;
using TRPG.Application.Common.Events;
using TRPG.GameSessions.Hubs;

namespace TRPG.Tests.Hubs;

public sealed class GameClientEventQueueRegistryTests
{
    [Fact]
    public void Drain_PreservesAnEnqueueUsingAnExistingWorldQueue()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var registry = new GameClientEventQueueRegistry();
        registry.Enqueue(new TestEvent(worldId, 1));
        var queuesField = typeof(GameClientEventQueueRegistry).GetField(
            "_queues",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        var queues =
            (ConcurrentDictionary<Guid, ConcurrentQueue<GameClientEvent>>)
                queuesField.GetValue(registry)!;
        var queueHeldByProducer = queues[worldId];

        // Act
        registry.Drain(worldId);
        queueHeldByProducer.Enqueue(new TestEvent(worldId, 2));

        // Assert
        Assert.Equal(2, Assert.IsType<TestEvent>(Assert.Single(registry.Drain(worldId))).Sequence);
    }

    private sealed record TestEvent(Guid WorldId, int Sequence) : GameClientEvent(WorldId);
}
