using TRPG.Application.GameSessions;

namespace TRPG.GameSessions.Hubs;

internal interface IGameClientEventBuffer
{
    IReadOnlyList<GameTurnEvent> Drain();
}

internal sealed class GameClientEventBuffer : IGameClientEventPublisher, IGameClientEventBuffer
{
    private readonly Queue<GameTurnEvent> _pendingEvents = new();

    public void Publish(GameTurnEvent gameEvent) => _pendingEvents.Enqueue(gameEvent);

    public IReadOnlyList<GameTurnEvent> Drain()
    {
        var pendingEvents = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        return pendingEvents;
    }
}
