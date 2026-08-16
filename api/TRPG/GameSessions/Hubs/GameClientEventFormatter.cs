using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal record GameClientMessage(string MethodName, object? Payload);

internal interface IGameClientEventFormatter
{
    Type EventType { get; }
    GameClientMessage Format(GameClientEvent gameEvent);
}

internal abstract class GameClientEventFormatter<TEvent> : IGameClientEventFormatter
    where TEvent : GameClientEvent
{
    public Type EventType => typeof(TEvent);

    public GameClientMessage Format(GameClientEvent gameEvent) => Format((TEvent)gameEvent);

    protected abstract GameClientMessage Format(TEvent gameEvent);
}
