namespace TRPG.Application.GameTurns;

public sealed class GameTurnContext
{
    public Guid SessionId { get; set; }
    public Guid WorldId { get; set; }
    public Guid PlayerId { get; set; }
    public bool PlayerMoved { get; set; }
}
