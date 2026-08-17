using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal interface IGameClientEventFormatter
{
    Type EventType { get; }
    Task Dispatch(IGameClient client, GameClientEvent gameEvent);
}

internal abstract class GameClientEventFormatter<TEvent> : IGameClientEventFormatter
    where TEvent : GameClientEvent
{
    public Type EventType => typeof(TEvent);

    public Task Dispatch(IGameClient client, GameClientEvent gameEvent) =>
        Dispatch(client, (TEvent)gameEvent);

    protected abstract Task Dispatch(IGameClient client, TEvent gameEvent);
}
