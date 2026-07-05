using OllamaSharp;

namespace TRPG;

internal class GameSessionState(GameSession session, Chat chat) {
    public GameSession Session { get; } = session;
    public Chat Chat { get; } = chat;
}
