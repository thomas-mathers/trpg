namespace TRPG.Application.Common.Events;

public abstract record GameClientEvent
{
    public abstract string MethodName { get; }
    public virtual object? Payload => null;
}

public interface IGameClientEventSink
{
    void Enqueue(GameClientEvent gameEvent);
}

public interface IGameClientEventDispatcher
{
    Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default);
}
