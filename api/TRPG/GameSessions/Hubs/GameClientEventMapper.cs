using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal interface IGameClientEventMapper
{
    Type EventType { get; }
    IGameClientCall Map(GameClientEvent gameEvent);
}

internal abstract class GameClientEventMapper<TEvent> : IGameClientEventMapper
    where TEvent : GameClientEvent
{
    public Type EventType => typeof(TEvent);

    public IGameClientCall Map(GameClientEvent gameEvent) => Map((TEvent)gameEvent);

    protected abstract IGameClientCall Map(TEvent gameEvent);
}
