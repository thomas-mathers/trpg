namespace TRPG.Application.GameTurns;

internal class StreamChatTurnHandler(GameTurnStreamer streamer)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        string input,
        CancellationToken cancellationToken = default
    ) => streamer.StreamChat(session, input, cancellationToken);
}
