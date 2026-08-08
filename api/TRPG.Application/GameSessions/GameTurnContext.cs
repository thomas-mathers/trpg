namespace TRPG.Application.GameSessions;

public interface IGameClientEventPublisher
{
    void Publish(GameTurnEvent gameEvent);
}

public sealed class GameTurnContext
{
    public Guid SessionId { get; set; }
    public Guid WorldId { get; set; }
    public Guid PlayerId { get; set; }
    public bool PlayerMoved { get; set; }
}
