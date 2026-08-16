namespace TRPG.Application.Common.Events;

public abstract record GameClientEvent
{
    public abstract string MethodName { get; }
    public virtual object? Payload => null;
}

public record GameClientMessage(string MethodName, object? Payload);

public interface IGameClientEventFormatter
{
    Type EventType { get; }
    GameClientMessage Format(GameClientEvent gameEvent);
}

public abstract class GameClientEventFormatter<TEvent> : IGameClientEventFormatter
    where TEvent : GameClientEvent
{
    public Type EventType => typeof(TEvent);

    public GameClientMessage Format(GameClientEvent gameEvent) => Format((TEvent)gameEvent);

    protected abstract GameClientMessage Format(TEvent gameEvent);
}

public interface IGameClientEventSink
{
    void Enqueue(GameClientEvent gameEvent);
}

public interface IGameClientEventDispatcher
{
    Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default);
}
