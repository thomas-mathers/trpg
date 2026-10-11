using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Publishing;

namespace TRPG.Application.GameTurns;

internal class RefreshSceneHandler(
    ICommandHandler<PublishPlayerSceneCommand> publishPlayerScene,
    IGameClientEventDispatcher eventDispatcher,
    WorldSimulationCoordinator simulation
)
{
    public async Task Handle(GameTurnSession session, CancellationToken cancellationToken = default)
    {
        await simulation.Request(
            session.WorldId,
            (_, token) =>
                publishPlayerScene.Handle(
                    new PublishPlayerSceneCommand
                    {
                        WorldId = session.WorldId,
                        PlayerId = session.PlayerId,
                    },
                    token
                ),
            cancellationToken
        );

        await eventDispatcher.FlushAsync(session.WorldId, cancellationToken);
    }
}
