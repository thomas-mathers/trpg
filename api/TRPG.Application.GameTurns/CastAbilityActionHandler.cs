using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation;

namespace TRPG.Application.GameTurns;

internal class CastAbilityActionHandler(
    WorldSimulationCoordinator simulation,
    IGameClientEventDispatcher eventDispatcher
)
{
    public async Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken = default
    )
    {
        var result = await simulation.CastAbility(
            session.WorldId,
            session.PlayerId,
            targetId,
            abilityName,
            cancellationToken
        );
        await eventDispatcher.FlushAsync(session.WorldId, cancellationToken);

        if (result.Succeeded)
        {
            return ActionOutcome.Success;
        }

        return ActionOutcome.Failed(
            result.AbilityNotFound
                ? ActionFailure.AbilityNotFound
                : ActionFailure.AbilityUnavailable
        );
    }
}
