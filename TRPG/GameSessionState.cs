using Microsoft.Extensions.AI;
using TRPG.Application.Game;

namespace TRPG;

internal class GameSessionState(GameSession session, List<ChatMessage> messages)
{
    public GameSession Session { get; } = session;
    public List<ChatMessage> Messages { get; } = messages;
}
