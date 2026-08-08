namespace TRPG.Application.GameSessions;

public interface IGameClientEventPublisher
{
    void Enqueue(GameTurnEvent gameEvent);
}

public interface IGameClientEventDispatcher
{
    bool HasPendingEvents { get; }
    bool TryDequeue(out GameTurnEvent? gameEvent);
}

public sealed class GameTurnContext : IGameClientEventPublisher, IGameClientEventDispatcher
{
    public Guid SessionId { get; set; }
    public Guid WorldId { get; set; }
    public Guid PlayerId { get; set; }
    private Queue<GameTurnEvent> PendingEvents { get; } = new();

    public IReadOnlyCollection<GameTurnEvent> Events => PendingEvents;

    public void Enqueue(GameTurnEvent gameEvent) => PendingEvents.Enqueue(gameEvent);

    bool IGameClientEventDispatcher.HasPendingEvents => PendingEvents.Count > 0;

    bool IGameClientEventDispatcher.TryDequeue(out GameTurnEvent? gameEvent)
    {
        if (PendingEvents.TryDequeue(out var pendingEvent))
        {
            gameEvent = pendingEvent;
            return true;
        }

        gameEvent = null;
        return false;
    }
}
