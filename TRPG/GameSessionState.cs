using OllamaSharp;
using TRPG.Services;

namespace TRPG;

internal class GameSessionState(GameSession session, Chat chat)
{
    public GameSession Session { get; } = session;
    public Chat Chat { get; } = chat;
    public SceneResult? LastScene { get; set; }
}
