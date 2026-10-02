using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class RespawnActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    PlayerRespawner playerRespawner
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        if (player?.Condition != CreatureCondition.Dead)
        {
            return ActionOutcome.Failed(ActionFailure.NotDead);
        }

        await playerRespawner.Respawn(session, cancellationToken);

        return ActionOutcome.Success;
    }
}
