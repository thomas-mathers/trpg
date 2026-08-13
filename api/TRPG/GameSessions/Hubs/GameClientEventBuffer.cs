using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal interface IGameClientEventBuffer
{
    IReadOnlyList<GameClientEvent> Drain();
}

internal sealed class GameClientEventBuffer : IGameClientEventSink, IGameClientEventBuffer
{
    private readonly Queue<GameClientEvent> _pendingEvents = new();

    public void Enqueue(GameClientEvent gameEvent) => _pendingEvents.Enqueue(gameEvent);

    public IReadOnlyList<GameClientEvent> Drain()
    {
        var pendingEvents = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        return pendingEvents;
    }
}
