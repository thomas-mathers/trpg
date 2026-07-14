using Microsoft.Extensions.AI;

namespace TRPG.Application.Game;

public class GameSessionState(GameSession session, List<ChatMessage> messages)
{
    public GameSession Session { get; } = session;
    public List<ChatMessage> Messages { get; } = messages;
    public SemaphoreSlim TurnLock { get; } = new(1, 1);
}
