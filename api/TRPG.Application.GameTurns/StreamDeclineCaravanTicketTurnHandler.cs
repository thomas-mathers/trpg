namespace TRPG.Application.GameTurns;

internal class StreamDeclineCaravanTicketTurnHandler(GameTurnStreamer streamer)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, _ => ResolveTurn(), cancellationToken);

    private static Task<GameTurnPrompt> ResolveTurn() =>
        Task.FromResult<GameTurnPrompt>(
            new GameTurnPrompt.Narrate(
                "The player just declined to buy a caravan ticket. Narrate a brief, low-key "
                    + "reaction from the caravan driver in one sentence."
            )
        );
}
