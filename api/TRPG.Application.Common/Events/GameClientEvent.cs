namespace TRPG.Application.Common.Events;

public abstract record GameClientEvent(Guid WorldId);

public interface IGameClientEventSink
{
    void Enqueue(GameClientEvent gameEvent);
}

public interface IGameClientEventDispatcher
{
    // Returns whether anything was actually sent.
    Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default);
}
