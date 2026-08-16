namespace TRPG.Application.Common.Events;

public abstract record GameClientEvent;

public interface IGameClientEventSink
{
    void Enqueue(GameClientEvent gameEvent);
}

public interface IGameClientEventDispatcher
{
    Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default);
}
