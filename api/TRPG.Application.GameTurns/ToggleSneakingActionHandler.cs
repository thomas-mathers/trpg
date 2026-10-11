using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.WorldSimulation;

namespace TRPG.Application.GameTurns;

internal class ToggleSneakingActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<ToggleSneakingCommand, ToggleSneakingCommandResult> toggleSneaking,
    WorldSimulationCoordinator simulation
)
{
    public async Task<ActionOutcome> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    )
    {
        await simulation.Request(
            session.WorldId,
            (_, ct) =>
                toggleSneaking.Handle(
                    new ToggleSneakingCommand { CreatureId = session.PlayerId },
                    ct
                ),
            cancellationToken
        );

        return await actionRunner.Run(
            session,
            _ => Task.FromResult(ActionOutcome.Success),
            cancellationToken
        );
    }
}
