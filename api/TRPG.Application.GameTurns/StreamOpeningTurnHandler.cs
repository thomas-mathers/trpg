namespace TRPG.Application.GameTurns;

internal class StreamOpeningTurnHandler(GameTurnStreamer streamer)
{
    private const string OpeningPrompt =
        "This is the start of the session. Your first response must be only a call to look, with no text or narration. After it returns, begin directly with the in-world opening scene it describes. Never announce or describe looking around.";

    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, _ => ResolveTurn(), cancellationToken);

    private static Task<GameTurnPrompt> ResolveTurn() =>
        Task.FromResult<GameTurnPrompt>(new GameTurnPrompt.Narrate(OpeningPrompt));
}
