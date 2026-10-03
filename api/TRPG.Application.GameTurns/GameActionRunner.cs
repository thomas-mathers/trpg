using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Events;

namespace TRPG.Application.GameTurns;

internal class GameActionRunner(
    TurnSceneDiffer sceneDiffer,
    IGameClientEventDispatcher eventDispatcher,
    IWorldMutationGate mutationGate
)
{
    public async Task<ActionOutcome> Run(
        GameTurnSession session,
        Func<CancellationToken, Task<ActionOutcome>> action,
        CancellationToken cancellationToken
    )
    {
        var outcome = await RunUnderGate(session, action, cancellationToken);

        await eventDispatcher.FlushAsync(session.WorldId, cancellationToken);

        return outcome;
    }

    private async Task<ActionOutcome> RunUnderGate(
        GameTurnSession session,
        Func<CancellationToken, Task<ActionOutcome>> action,
        CancellationToken cancellationToken
    )
    {
        await using var lease = await mutationGate.Acquire(session.WorldId, cancellationToken);

        // Captured before the action runs so the diff catches every mutation it makes.
        var before = await sceneDiffer.Capture(session, cancellationToken);

        var outcome = await action(cancellationToken);

        if (outcome.Succeeded)
        {
            await sceneDiffer.EnqueueChange(before, session, cancellationToken);
        }

        return outcome;
    }
}
