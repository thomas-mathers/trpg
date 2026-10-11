using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Poses;

namespace TRPG.Application.GameTurns;

internal class GameActionRunner(
    TurnSceneDiffer sceneDiffer,
    IGameClientEventDispatcher eventDispatcher,
    WorldSimulationCoordinator simulation
)
{
    public Task<ActionOutcome> Run(
        GameTurnSession session,
        Func<CancellationToken, Task<ActionOutcome>> action,
        CancellationToken cancellationToken
    ) => Run(session, (_, token) => action(token), cancellationToken);

    public async Task<ActionOutcome> Run(
        GameTurnSession session,
        Func<LivePlayerPoses, CancellationToken, Task<ActionOutcome>> action,
        CancellationToken cancellationToken
    )
    {
        var outcome = await simulation.Request(
            session.WorldId,
            (poses, token) => Execute(session, poses, action, token),
            cancellationToken
        );

        await eventDispatcher.FlushAsync(session.WorldId, cancellationToken);

        return outcome;
    }

    private async Task<ActionOutcome> Execute(
        GameTurnSession session,
        LivePlayerPoses poses,
        Func<LivePlayerPoses, CancellationToken, Task<ActionOutcome>> action,
        CancellationToken cancellationToken
    )
    {
        var outcome = await action(poses, cancellationToken);

        if (outcome.Succeeded)
        {
            if (outcome.AdvancedGameTime is not null)
            {
                await sceneDiffer.EnqueueTimeAdvance(session, cancellationToken);
            }
            else
            {
                await sceneDiffer.EnqueueChange(session, cancellationToken);
            }
        }

        return outcome;
    }
}
