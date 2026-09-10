using TRPG.Application.Common.Commands;
using TRPG.Application.Props.Commands;

namespace TRPG.Application.GameTurns;

internal class StreamPullLeverTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<PullLeverCommand, PullLeverResult> pullLever
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid leverId,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(leverId, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        Guid leverId,
        CancellationToken cancellationToken
    )
    {
        var result = await pullLever.Handle(
            new PullLeverCommand { LeverId = leverId },
            cancellationToken
        );

        if (result.AlreadyPulled)
        {
            return new GameTurnPrompt.Reply("This lever has already been pulled.");
        }

        return new GameTurnPrompt.Narrate(
            "The player just pulled a lever. Narrate only the pull itself — a mechanism grinding, "
                + "a chain going taut, something shifting somewhere out of sight — in one or two "
                + "sentences. Do not describe any door, gate, or passage opening or changing, since "
                + "that isn't known here; it will be discovered separately if the player goes to look."
        );
    }
}
