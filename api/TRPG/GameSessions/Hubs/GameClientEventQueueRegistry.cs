using System.Collections.Concurrent;
using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal interface IGameClientEventDrain
{
    IReadOnlyList<GameClientEvent> Drain(Guid worldId);
    SemaphoreSlim GetSendGate(Guid worldId);
}

internal sealed class GameClientEventQueueRegistry : IGameClientEventSink, IGameClientEventDrain
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<GameClientEvent>> _queues = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sendGates = new();

    public void Enqueue(GameClientEvent clientEvent)
    {
        var queue = _queues.GetOrAdd(
            clientEvent.WorldId,
            _ => new ConcurrentQueue<GameClientEvent>()
        );

        queue.Enqueue(clientEvent);
    }

    public IReadOnlyList<GameClientEvent> Drain(Guid worldId)
    {
        if (!_queues.TryGetValue(worldId, out var queue))
        {
            return [];
        }

        var events = new List<GameClientEvent>();

        while (queue.TryDequeue(out var clientEvent))
        {
            events.Add(clientEvent);
        }

        return events;
    }

    public SemaphoreSlim GetSendGate(Guid worldId) =>
        _sendGates.GetOrAdd(worldId, _ => new SemaphoreSlim(1, 1));
}
