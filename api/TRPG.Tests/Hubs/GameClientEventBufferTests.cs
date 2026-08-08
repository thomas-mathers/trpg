using TRPG.Application.GameSessions;
using TRPG.GameSessions.Hubs;

namespace TRPG.Tests.Hubs;

public class GameClientEventBufferTests
{
    [Fact]
    public void Drain_ReturnsPublishedEventsInOrder_AndClearsTheBuffer()
    {
        // Arrange
        var buffer = new GameClientEventBuffer();
        var first = new TestGameEvent("first");
        var second = new TestGameEvent("second");
        buffer.Publish(first);
        buffer.Publish(second);

        // Act
        var events = buffer.Drain();

        // Assert
        Assert.Equal([first, second], events);
        Assert.Empty(buffer.Drain());
    }

    private sealed record TestGameEvent(string Name) : GameTurnEvent
    {
        public override string MethodName => Name;
    }
}
