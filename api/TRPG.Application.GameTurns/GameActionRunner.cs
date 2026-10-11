using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Domain;

namespace TRPG.Application.GameTurns;

internal class GameActionRunner(
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IActionSceneAnnouncer actionSceneAnnouncer,
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
            await Announce(session, outcome, cancellationToken);
        }

        return outcome;
    }

    private async Task Announce(
        GameTurnSession session,
        ActionOutcome outcome,
        CancellationToken cancellationToken
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        if (outcome.AdvancedGameTime is not null)
        {
            await actionSceneAnnouncer.AnnounceTimeAdvance(
                session.WorldId,
                session.PlayerId,
                gameTime,
                cancellationToken
            );
            return;
        }

        await actionSceneAnnouncer.AnnounceActionChanges(
            session.WorldId,
            session.PlayerId,
            gameTime,
            cancellationToken
        );
    }
}
