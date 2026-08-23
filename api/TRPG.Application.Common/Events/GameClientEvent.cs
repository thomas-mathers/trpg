namespace TRPG.Application.Common.Events;

public abstract record GameClientEvent;

public interface IGameClientEventSink
{
    void Enqueue(GameClientEvent gameEvent);
}

public interface IGameClientEventDispatcher
{
    Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default);

    // Like FlushAsync, but waits for the client to acknowledge receipt before returning.
    Task FlushAndAwaitAckAsync(Guid worldId, CancellationToken cancellationToken = default);
}
