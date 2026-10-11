using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;

namespace TRPG.Application.GameTurns;

internal class ToggleSneakingActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<ToggleSneakingCommand, ToggleSneakingCommandResult> toggleSneaking
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => ToggleSneaking(session, ct), cancellationToken);

    private async Task<ActionOutcome> ToggleSneaking(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        await toggleSneaking.Handle(
            new ToggleSneakingCommand { CreatureId = session.PlayerId },
            cancellationToken
        );

        return ActionOutcome.Success;
    }
}
