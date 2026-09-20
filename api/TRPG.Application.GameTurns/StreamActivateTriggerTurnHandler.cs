using TRPG.Application.Common.Commands;
using TRPG.Application.Props.Commands;

namespace TRPG.Application.GameTurns;

internal class StreamActivateTriggerTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<ActivateTriggerCommand, ActivateTriggerResult> activateTrigger
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid triggerId,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => ResolveTurn(session.PlayerId, triggerId, ct),
            cancellationToken
        );

    private async Task<GameTurnPrompt> ResolveTurn(
        Guid playerId,
        Guid triggerId,
        CancellationToken cancellationToken
    )
    {
        var result = await activateTrigger.Handle(
            new ActivateTriggerCommand { TriggerId = triggerId, PlayerId = playerId },
            cancellationToken
        );

        if (result.AlreadyActivated)
        {
            return new GameTurnPrompt.Reply("This has already been activated.");
        }

        return new GameTurnPrompt.Narrate(
            "The player just activated a mechanism. Narrate only the activation itself — a lever "
                + "grinding, a chain going taut, something shifting somewhere out of sight — in one "
                + "or two sentences. Do not describe any door, gate, or passage opening or changing, "
                + "since that isn't known here; it will be discovered separately if the player goes "
                + "to look."
        );
    }
}
