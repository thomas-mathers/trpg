namespace TRPG.Application.GameSessions;

public class GameTurnContext
{
    public Guid SessionId { get; set; }
    public Guid WorldId { get; set; }
    public Guid PlayerId { get; set; }
    public Queue<GameTurnEvent> PendingEvents { get; } = new();
}
