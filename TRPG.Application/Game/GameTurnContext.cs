namespace TRPG.Application.Game;

public class GameTurnContext
{
    public Guid SessionId { get; set; }
    public GameSessionLock? Lock { get; set; }
    public Guid WorldId { get; set; }
    public Guid PlayerId { get; set; }
    public bool DidMoveThisTurn { get; set; }
    public bool DidSceneRefreshThisTurn { get; set; }
}
