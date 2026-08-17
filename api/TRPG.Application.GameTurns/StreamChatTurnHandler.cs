namespace TRPG.Application.GameTurns;

internal class StreamChatTurnHandler(GameTurnStreamer streamer)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        string input,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, _ => ResolveTurn(input), cancellationToken);

    private static Task<GameTurnPrompt> ResolveTurn(string input) =>
        Task.FromResult<GameTurnPrompt>(new GameTurnPrompt.Narrate(input));
}
